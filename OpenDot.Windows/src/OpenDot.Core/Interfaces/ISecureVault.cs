using OpenDot.Core.Models;

namespace OpenDot.Core.Interfaces;

public interface ISecureVault
{
    string Seal(string plaintext);
    string Unseal(string sealedText);
    void SavePassword(string site, string username, string password);
    (string Site, string Username, string Password)? CredentialFor(string site);
    List<PasswordEntry> ListPasswords();
    void DeletePassword(string id);
}
