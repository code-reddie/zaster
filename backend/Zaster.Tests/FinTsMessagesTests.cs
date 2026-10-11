using System.Linq;
using Zaster.FinTs;

namespace Zaster.Tests;

public sealed class FinTsMessagesTests
{
    [Theory]
    [InlineData("1234", "1234")]
    [InlineData("a?b+c:d'e@f", "a??b?+c?:d?'e?@f")]
    public void Escape_maskiert_alle_Sonderzeichen(string pin, string expected)
    {
        Assert.Equal(expected, FinTsMessages.Escape(pin));
    }

    [Fact]
    public void EscapeUserId_maskiert_Fragezeichen_und_At()
    {
        Assert.Equal("max??x?@y", FinTsMessages.EscapeUserId("max?x@y"));
    }

    [Fact]
    public void ParseResultMessages_liest_HIRMG_mit_zusaetzlichem_Doppelpunkt()
    {
        // So antwortete die ING, als die PIN unmaskiert war; libfintx hat das nicht erkannt.
        const string raw = "HNHBK:1:3+000000000123+300+0+1'HIRMG:2:2:+9800::Dialog abgebrochen+9030::Die Daten konnten nicht entschlüsselt werden.'HNHBS:3:1+1'";

        var messages = FinTsMessages.ParseResultMessages(raw);

        Assert.Equal(["9800", "9030"], messages.Select(m => m.Code).ToArray());
        Assert.Equal("Die Daten konnten nicht entschlüsselt werden.", messages[1].Message);
        Assert.True(FinTsMessages.IsRejected(messages));
    }

    [Fact]
    public void ParseResultMessages_hebt_Maskierung_auf()
    {
        const string raw = "HIRMS:4:2:3+3010::Umsätze nur 90 Tage?: siehe Hilfe'";

        var message = Assert.Single(FinTsMessages.ParseResultMessages(raw));

        Assert.Equal("3010", message.Code);
        Assert.Equal("Umsätze nur 90 Tage: siehe Hilfe", message.Message);
    }

    [Fact]
    public void Warnungen_sind_keine_Ablehnung()
    {
        var messages = new[] { new FinTsBankMessage("0020", "Auftrag ausgeführt"), new FinTsBankMessage("3010", "Nur 90 Tage") };

        Assert.False(FinTsMessages.IsRejected(messages));
    }

    [Fact]
    public void ResultSegments_liefert_nur_Rueckmeldungen()
    {
        const string raw = "HNHBK:1:3+0+300'HIKAZ:5:7:3+@12@geheime Umsätze'HIRMG:2:2+0010::Nachricht entgegengenommen.'";

        Assert.Equal(["HIRMG:2:2+0010::Nachricht entgegengenommen."], FinTsMessages.ResultSegments(raw).ToArray());
    }
}
