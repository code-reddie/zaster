using System.Linq;
using System.Numerics;
using System.Text;

namespace Zaster.Models;

public static class Iban
{
    /// <summary>
    /// Entfernt Leerzeichen und prüft Länge, Aufbau und Prüfziffer (ISO 13616, Modulo 97).
    /// </summary>
    /// <returns>Die IBAN in Großbuchstaben ohne Leerzeichen, oder null wenn sie ungültig ist.</returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var iban = string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        if (iban.Length is < 15 or > 34
            || !char.IsAsciiLetterUpper(iban[0])
            || !char.IsAsciiLetterUpper(iban[1])
            || !char.IsAsciiDigit(iban[2])
            || !char.IsAsciiDigit(iban[3])
            || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return null;
        }

        var digits = new StringBuilder();
        foreach (var c in iban[4..] + iban[..4])
        {
            digits.Append(char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10);
        }

        return BigInteger.Parse(digits.ToString()) % 97 == 1 ? iban : null;
    }
}
