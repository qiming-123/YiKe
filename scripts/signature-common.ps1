# Verify integrity without changing any Windows certificate/trust store.
function Get-NativeAuthenticodeResult([string]$Path) {
 if(!('YiKeBuild.Authenticode' -as [type])){
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace YiKeBuild {
 public static class Authenticode {
  [StructLayout(LayoutKind.Sequential)] struct FileInfo { public uint Size; public IntPtr Path, File, Subject; }
  [StructLayout(LayoutKind.Sequential)] struct TrustData {
   public uint Size; public IntPtr Policy, Sip; public uint Ui, Revocation, Choice; public IntPtr File;
   public uint Action; public IntPtr State, Url; public uint Flags, Context;
  }
  [DllImport("wintrust.dll", ExactSpelling=true, PreserveSig=true)] static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
  public static uint Check(string path) {
   var name=Marshal.StringToCoTaskMemUni(path); var info=new FileInfo {Size=(uint)Marshal.SizeOf<FileInfo>(),Path=name};
   var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
   var data=new TrustData {Size=(uint)Marshal.SizeOf<TrustData>(),Ui=2,Choice=1,File=pointer,Action=1,Flags=0x1010};
   var action=new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
   try { Marshal.StructureToPtr(info,pointer,false); return unchecked((uint)WinVerifyTrust(new IntPtr(-1),ref action,ref data)); }
   finally { if(data.State!=IntPtr.Zero) { data.Action=2; WinVerifyTrust(new IntPtr(-1),ref action,ref data); } Marshal.FreeHGlobal(pointer); Marshal.FreeCoTaskMem(name); }
  }
 }
}
'@
 }
 return [YiKeBuild.Authenticode]::Check([IO.Path]::GetFullPath($Path))
}
function Get-CertificateSha256($Certificate) {
 return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Certificate.RawData)).ToLowerInvariant()
}
function Get-AuthenticodeAlgorithms([string]$Path) {
 $stream=[IO.File]::OpenRead([IO.Path]::GetFullPath($Path))
 try{
  $pe=[System.Reflection.PortableExecutable.PEReader]::new($stream)
  $directory=$pe.PEHeaders.PEHeader.CertificateTableDirectory
  if($directory.Size -lt 8 -or $directory.Size -gt 1048576 -or $directory.RelativeVirtualAddress -lt 0 -or [long]$directory.RelativeVirtualAddress+$directory.Size -gt $stream.Length){throw 'Invalid PE certificate table'}
  $stream.Position=$directory.RelativeVirtualAddress
  $reader=[IO.BinaryReader]::new($stream)
  $length=$reader.ReadUInt32();$revision=$reader.ReadUInt16();$kind=$reader.ReadUInt16()
  if($length -lt 8 -or $length -gt $directory.Size -or $kind -ne 2 -or $revision -ne 512){throw 'Invalid Authenticode certificate record'}
  $cms=[Security.Cryptography.Pkcs.SignedCms]::new();$cms.Decode($reader.ReadBytes($length-8));$cms.CheckSignature($true)
  if($cms.SignerInfos.Count -ne 1){throw 'Expected one primary signer'}
  $signer=$cms.SignerInfos[0]
  $attributes=@($signer.UnsignedAttributes|Where-Object {$_.Oid.Value -eq '1.3.6.1.4.1.311.3.3.1'})
  if($attributes.Count -ne 1 -or $attributes[0].Values.Count -ne 1){throw 'Missing RFC3161 timestamp token'}
  $stamp=[Security.Cryptography.Pkcs.SignedCms]::new();$stamp.Decode($attributes[0].Values[0].RawData);$stamp.CheckSignature($true)
  if($stamp.ContentInfo.ContentType.Value -ne '1.2.840.113549.1.9.16.1.4' -or $stamp.SignerInfos.Count -ne 1){throw 'Invalid RFC3161 timestamp content'}
  $asn=[System.Formats.Asn1.AsnReader]::new([ReadOnlyMemory[byte]]::new($stamp.ContentInfo.Content),[System.Formats.Asn1.AsnEncodingRules]::DER)
  $sequence=$asn.ReadSequence();$null=$sequence.ReadInteger();$null=$sequence.ReadObjectIdentifier();$imprint=$sequence.ReadSequence();$algorithm=$imprint.ReadSequence().ReadObjectIdentifier();$digest=$imprint.ReadOctetString()
  $sha256='2.16.840.1.101.3.4.2.1'
  if($signer.DigestAlgorithm.Value -ne $sha256 -or $algorithm -ne $sha256 -or $stamp.SignerInfos[0].DigestAlgorithm.Value -ne $sha256){throw 'Signature/timestamp must use SHA256'}
  if([Convert]::ToHexString($digest) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($signer.GetSignature()))){throw 'Timestamp is not bound to the primary signature'}
  return [ordered]@{fileDigest='SHA256';timestampProtocol='RFC3161';timestampDigest='SHA256';timestampSubject=$stamp.SignerInfos[0].Certificate.Subject;timestampBoundToSignature=$true;cmsSignaturesVerified=$true}
 }finally{$stream.Dispose()}
}
function Assert-AuthenticodeFile {
 param([Parameter(Mandatory)][string]$Path,[string]$ExpectedCertificateSha256='',[switch]$AllowSelfSigned)
 $signature=Get-AuthenticodeSignature -LiteralPath $Path -ErrorAction Stop
 if(!$signature.SignerCertificate){throw "Missing Authenticode signature: $Path"}
 $identity=Get-CertificateSha256 $signature.SignerCertificate
 if($ExpectedCertificateSha256 -and $identity -ne $ExpectedCertificateSha256.ToLowerInvariant()){throw "Unexpected signing certificate: $Path"}
 $native=Get-NativeAuthenticodeResult $Path
 if($native -eq 0 -and $signature.Status -eq 'Valid'){$trust='trusted'}
 elseif($AllowSelfSigned -and $ExpectedCertificateSha256 -and $native -eq [Convert]::ToUInt32('800B0109',16) -and $signature.SignerCertificate.Subject -eq $signature.SignerCertificate.Issuer){$trust='self-signed-untrusted'}
 else{throw ('Authenticode verification failed: 0x{0:X8}, status={1} / {2}' -f $native,$signature.Status,$Path)}
 return [ordered]@{status=$signature.Status.ToString();trust=$trust;subject=$signature.SignerCertificate.Subject;certificateSha256=$identity;thumbprint=$signature.SignerCertificate.Thumbprint;timestamped=[bool]$signature.TimeStamperCertificate;nativeResult=('0x{0:X8}' -f $native)}
}
