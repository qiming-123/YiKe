param([string]$Version='1.7.8',[string]$UninstallerPath='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
. "$PSScriptRoot/signature-common.ps1"
. "$PSScriptRoot/release-common.ps1"
$public=[Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $root 'assets/opens-codesign.cer'))
$pin=Get-CertificateSha256 $public
$passed=0;$proof=@()
function Expect-Rejection([string]$Name,[string]$Pattern,[scriptblock]$Action){
 try{& $Action|Out-Null;throw "Accepted invalid signature case: $Name"}catch{if($_.Exception.Message -notlike $Pattern){throw};Write-Host "PASS $Name";$script:passed++}
}
$app=Join-Path $root "artifacts/publish-$Version/YiKe.exe"
$setup=Join-Path $root "dist/YiKe-Setup-$Version-win-x64.exe"
$files=@($app,$setup)
if($UninstallerPath){$files+=(Resolve-Path -LiteralPath $UninstallerPath).Path}
foreach($file in $files){
 $identity=Assert-AuthenticodeFile -Path $file -ExpectedCertificateSha256 $pin -AllowSelfSigned
 $algorithms=Get-AuthenticodeAlgorithms $file
 $proof+=@{file=[IO.Path]::GetFileName($file);sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant();identity=$identity;algorithms=$algorithms}
 Write-Host ('PASS opens identity, file integrity and SHA256/RFC3161 timestamp: '+[IO.Path]::GetFileName($file));$passed++
}
Expect-Rejection 'Self-signed identity requires explicit opt-in' '*0x800B0109*' {Assert-AuthenticodeFile -Path $app -ExpectedCertificateSha256 $pin}
Expect-Rejection 'A different pinned certificate is rejected' '*Unexpected signing certificate*' {Assert-AuthenticodeFile -Path $app -ExpectedCertificateSha256 ('0'*64) -AllowSelfSigned}
$tampered=Join-Path $root ('artifacts/signature-tamper-'+[guid]::NewGuid().ToString('N')+'.exe')
try{
 Copy-Item -LiteralPath $app -Destination $tampered
 $stream=[IO.File]::Open($tampered,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite)
 try{$stream.Position=80;$value=$stream.ReadByte();$stream.Position=80;$stream.WriteByte($value -bxor 1)}finally{$stream.Dispose()}
 Expect-Rejection 'Modified executable digest is rejected' '*0x80096010*' {Assert-AuthenticodeFile -Path $tampered -ExpectedCertificateSha256 $pin -AllowSelfSigned}
}finally{if(Test-Path -LiteralPath $tampered){[IO.File]::Delete($tampered)}}
$memory=[IO.MemoryStream]::new();$zip=[IO.Compression.ZipArchive]::new($memory,[IO.Compression.ZipArchiveMode]::Create,$true)
try{$null=$zip.CreateEntry('signing/private-key.pfx')}finally{$zip.Dispose()};$memory.Position=0
$archive=[IO.Compression.ZipArchive]::new($memory,[IO.Compression.ZipArchiveMode]::Read)
try{Expect-Rejection 'Private-key archives cannot be packaged' '*Unsafe or personal archive entry*' {Assert-SafeZip $archive}}finally{$archive.Dispose();$memory.Dispose()}
$trusted= (Test-Path ('Cert:/CurrentUser/Root/'+$public.Thumbprint)) -or (Test-Path ('Cert:/LocalMachine/Root/'+$public.Thumbprint))
if($trusted){throw 'opens must not be silently installed as a trusted root'}
Write-Host 'PASS opens is not installed as a trusted root';$passed++
$parent=Get-Process -Id $PID
$start=[Diagnostics.ProcessStartInfo]::new($app);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true
foreach($arg in @('--uia-worker',$PID.ToString(),$parent.StartTime.ToUniversalTime().Ticks.ToString())){$start.ArgumentList.Add($arg)}
$worker=[Diagnostics.Process]::Start($start)
try{
 $worker.StandardInput.WriteLine('{"Sequence":1,"Window":0,"ProcessId":0,"X":0,"Y":0,"StartX":0,"StartY":0,"Time":"2026-10-23T00:00:00Z"}');$worker.StandardInput.Flush()
 $reply=$worker.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult()|ConvertFrom-Json
 if($reply.Status -ne 5 -or $reply.Text){throw 'Signed self-contained worker did not return the expected guarded result'}
 $worker.StandardInput.Close();if(!$worker.WaitForExit(5000)){throw 'Signed worker failed to exit after EOF'}
}finally{if(!$worker.HasExited){$worker.Kill();$null=$worker.WaitForExit(5000)};$worker.Dispose()}
Write-Host 'PASS signed self-contained app starts its production UIA worker and exits cleanly';$passed++
[ordered]@{version=$Version;passed=$passed;failed=0;opensTrustedRootInstalled=$trusted;artifacts=$proof;testedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'artifacts/signature-results.json') -Encoding utf8
Write-Host "SIGNATURE RESULT $passed passed, 0 failed"
