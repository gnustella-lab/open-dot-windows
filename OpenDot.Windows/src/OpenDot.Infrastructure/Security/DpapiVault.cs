using System.Security.Cryptography;
using System.Text;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Infrastructure.Security;

public class DpapiVault : ISecureVault
{
    private readonly IDotRepository _repo;
    private byte[]? _masterKey;
    private readonly object _lock = new();

    public DpapiVault(IDotRepository repo)
    {
        _repo = repo;
    }

    private byte[] GetMasterKey()
    {
        lock (_lock)
        {
            if (_masterKey != null) return _masterKey;

            var keySetting = _repo.GetSetting("vault_master_key");
            if (!string.IsNullOrEmpty(keySetting))
            {
                try
                {
                    var encryptedBytes = Convert.FromBase64String(keySetting);
                    _masterKey = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                    return _masterKey;
                }
                catch
                {
                    // Fallback to regenerating if corrupted
                }
            }

            // Generate fresh 256-bit key and protect via Windows DPAPI
            _masterKey = RandomNumberGenerator.GetBytes(32);
            var protectedKey = ProtectedData.Protect(_masterKey, null, DataProtectionScope.CurrentUser);
            _repo.SetSetting("vault_master_key", Convert.ToBase64String(protectedKey));
            return _masterKey;
        }
    }

    public string Seal(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;

        var key = GetMasterKey();
        var iv = RandomNumberGenerator.GetBytes(12); // 96-bit nonce for GCM
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16]; // 128-bit auth tag

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, plainBytes, cipherBytes, tag);

        return $"{Convert.ToBase64String(iv)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipherBytes)}";
    }

    public string Unseal(string sealedText)
    {
        if (string.IsNullOrWhiteSpace(sealedText)) return string.Empty;

        var parts = sealedText.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException("Invalid sealed payload format.");
        }

        var key = GetMasterKey();
        var iv = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipherBytes = Convert.FromBase64String(parts[2]);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(iv, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public void SavePassword(string site, string username, string password)
    {
        var sealedPassword = Seal(password);
        _repo.InsertPassword(site.Trim(), username.Trim(), sealedPassword);
    }

    public (string Site, string Username, string Password)? CredentialFor(string site)
    {
        var row = _repo.SealedPasswordFor(site);
        if (row == null) return null;

        try
        {
            var unsealed = Unseal(row.Value.SealedSecret);
            return (row.Value.Site, row.Value.Username, unsealed);
        }
        catch
        {
            return null;
        }
    }

    public List<PasswordEntry> ListPasswords()
    {
        return _repo.ListPasswords();
    }

    public void DeletePassword(string id)
    {
        _repo.DeletePassword(id);
    }
}
