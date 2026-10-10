using OpenDot.Infrastructure.Persistence;
using OpenDot.Infrastructure.Security;

namespace OpenDot.Tests;

public class DpapiVaultTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteDotRepository _repo;
    private readonly DpapiVault _vault;

    public DpapiVaultTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OpenDotVaultTests_" + Guid.NewGuid().ToString("N"));
        _repo = new SqliteDotRepository(_tempDir);
        _vault = new DpapiVault(_repo);
    }

    [Fact]
    public void SealAndUnseal_PreservesSecretCorrectly()
    {
        var secret = "sk-super-secret-key-123456789";
        var sealedText = _vault.Seal(secret);

        Assert.NotNull(sealedText);
        Assert.NotEqual(secret, sealedText);
        Assert.Contains(".", sealedText);

        var unsealed = _vault.Unseal(sealedText);
        Assert.Equal(secret, unsealed);
    }

    [Fact]
    public void SavePassword_RetrievesDecryptedWithoutPlaintextStorage()
    {
        _vault.SavePassword("github.com", "developer", "P@ssword2026!");

        var list = _vault.ListPasswords();
        Assert.Single(list);
        Assert.Equal("github.com", list[0].Site);
        Assert.Equal("developer", list[0].Username);

        var cred = _vault.CredentialFor("github.com");
        Assert.NotNull(cred);
        Assert.Equal("P@ssword2026!", cred.Value.Password);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }
}
