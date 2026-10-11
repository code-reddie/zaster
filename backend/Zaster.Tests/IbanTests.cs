using Zaster.Models;

namespace Zaster.Tests;

public sealed class IbanTests
{
    [Theory]
    [InlineData("DE89 3704 0044 0532 0130 00", "DE89370400440532013000")]
    [InlineData("de89370400440532013000", "DE89370400440532013000")]
    public void Gueltige_IBAN_wird_normalisiert(string input, string expected)
    {
        Assert.Equal(expected, Iban.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("DE89370400440532013001")]
    [InlineData("DE8937040044053201300")]
    [InlineData("1234567890123456")]
    public void Ungueltige_IBAN_ergibt_null(string input)
    {
        Assert.Null(Iban.Normalize(input));
    }
}
