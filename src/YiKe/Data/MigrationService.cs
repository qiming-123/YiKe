using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YiKe.Core;

namespace YiKe.Data;

public static class MigrationService
{
    public const int Iterations = 600000;
    public const int MaximumFileBytes = 4194304;
    sealed record Envelope(int Version, int Iterations, byte[] Salt, byte[] Nonce, byte[] Ciphertext, byte[] Tag);
    public sealed record Payload(AppSettings Settings, string? ApiKey, Dictionary<Guid, string>? ProfileKeys = null);
    static readonly byte[] Aad = Encoding.UTF8.GetBytes("YiKe.migration.v1");
    public static byte[] Encrypt(AppSettings settings, string? apiKey, string password, Dictionary<Guid, string>? profileKeys = null)
    {
        if (password.Length is < 10 or > 1024) throw new ArgumentException("迁移密码应为 10–1,024 个字符，请妥善保管。");
        if (apiKey is not null) apiKey = CredentialStore.ValidateSecret(apiKey);
        settings = OnlineProfiles.Capture(settings);
        ValidateKeys(settings, profileKeys);
        byte[] salt = RandomNumberGenerator.GetBytes(16), nonce = RandomNumberGenerator.GetBytes(12);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new Payload(settings, apiKey, profileKeys), SettingsStore.Json);
        byte[] cipher = new byte[plain.Length], tag = new byte[16];
        try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, Aad); return JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, Iterations, salt, nonce, cipher, tag)); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public static Payload Decrypt(byte[] file, string password)
    {
        if (file.Length > MaximumFileBytes) throw new InvalidDataException("迁移包过大。");
        byte[]? key = null, plain = null;
        try
        {
            if (password.Length > 1024) throw new InvalidDataException();
            var e = JsonSerializer.Deserialize<Envelope>(file) ?? throw new InvalidDataException();
            if (e.Version != 1 || e.Iterations != Iterations || e.Salt.Length != 16 || e.Nonce.Length != 12 || e.Tag.Length != 16) throw new InvalidDataException();
            key = Rfc2898DeriveBytes.Pbkdf2(password, e.Salt, e.Iterations, HashAlgorithmName.SHA256, 32); plain = new byte[e.Ciphertext.Length];
            using var aes = new AesGcm(key, 16); aes.Decrypt(e.Nonce, e.Ciphertext, e.Tag, plain, Aad);
            var result = JsonSerializer.Deserialize<Payload>(plain, SettingsStore.Json) ?? throw new InvalidDataException();
            var settings = OnlineProfiles.Rebind(result.Settings.Validate());
            ValidateKeys(settings, result.ProfileKeys);
            return result with { Settings = settings, ApiKey = result.ApiKey is null ? null : CredentialStore.ValidateSecret(result.ApiKey) };
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or InvalidDataException or ArgumentException or NullReferenceException) { throw new InvalidDataException("密码错误，或迁移包已损坏、被修改；当前配置未改变。", ex); }
        finally { if (key is not null) CryptographicOperations.ZeroMemory(key); if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }
    static void ValidateKeys(AppSettings settings, Dictionary<Guid, string>? keys)
    {
        if (keys is null) return;
        if (keys.Count > 32 || keys.Keys.Any(id => !settings.OnlineProfiles.Any(p => p.Id == id))) throw new ArgumentException("迁移密钥与 API 配置不匹配。");
        foreach (var value in keys.Values) CredentialStore.ValidateSecret(value);
    }
}
