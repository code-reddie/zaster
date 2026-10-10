using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Zaster.FinTs;

/// <summary>
/// Ver- und entschlüsselt die gespeicherte FinTS-PIN.
/// </summary>
public sealed class FinTsPinProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Zaster.FinTs.Pin");

    public string Protect(string pin) => _protector.Protect(pin);

    /// <returns>Die PIN, oder null wenn der Schlüssel fehlt oder der Wert beschädigt ist.</returns>
    public string? Unprotect(string protectedPin)
    {
        try
        {
            return _protector.Unprotect(protectedPin);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
