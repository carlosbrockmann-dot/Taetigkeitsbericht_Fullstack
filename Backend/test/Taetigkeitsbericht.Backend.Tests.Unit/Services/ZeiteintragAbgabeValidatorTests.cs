using FluentAssertions;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.Tests.Unit.Services;

public class ZeiteintragAbgabeValidatorTests
{
    private static Zeiteintrag Arbeit(
        DateOnly datum,
        TimeOnly von,
        TimeOnly bis,
        int? mandantId = 1) => new()
    {
        Datum = datum,
        MandantId = mandantId,
        Kategorie = ZeiteintragKategorie.Arbeitstag,
        UhrzeitVon = von,
        UhrzeitBis = bis,
    };

    private static Zeiteintrag Urlaub(DateOnly datum, int? mandantId = 1) => new()
    {
        Datum = datum,
        MandantId = mandantId,
        Kategorie = ZeiteintragKategorie.Urlaub,
    };

    [Fact]
    public void Validiere_ok_bei_unterschiedlichen_Arbeitszeiten()
    {
        var tag = new DateOnly(2026, 1, 15);
        var eintraege = new[]
        {
            Arbeit(tag, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            Arbeit(tag, new TimeOnly(13, 0), new TimeOnly(17, 0)),
        };

        ZeiteintragAbgabeValidator.Validiere(eintraege).Should().BeNull();
    }

    [Fact]
    public void Validiere_fehler_bei_fehlenden_Uhrzeiten_am_Arbeitstag()
    {
        var eintraege = new[]
        {
            new Zeiteintrag
            {
                Datum = new DateOnly(2026, 1, 15),
                MandantId = 1,
                Kategorie = ZeiteintragKategorie.Arbeitstag,
            },
        };

        ZeiteintragAbgabeValidator.Validiere(eintraege)
            .Should().Contain("braucht UhrzeitVon und UhrzeitBis");
    }

    [Fact]
    public void Validiere_fehler_bei_doppelten_Arbeitszeiten()
    {
        var tag = new DateOnly(2026, 1, 15);
        var von = new TimeOnly(8, 0);
        var bis = new TimeOnly(16, 0);
        var eintraege = new[]
        {
            Arbeit(tag, von, bis),
            Arbeit(tag, von, bis),
        };

        ZeiteintragAbgabeValidator.Validiere(eintraege)
            .Should().Contain("doppelte Arbeitszeiten");
    }

    [Fact]
    public void Validiere_fehler_bei_mehrfach_Urlaub_Krankheit()
    {
        var tag = new DateOnly(2026, 1, 15);
        var eintraege = new[]
        {
            Urlaub(tag),
            new Zeiteintrag
            {
                Datum = tag,
                MandantId = 1,
                Kategorie = ZeiteintragKategorie.Krankheit,
            },
        };

        ZeiteintragAbgabeValidator.Validiere(eintraege)
            .Should().Contain("Urlaub/Krankheit nur einmal");
    }

    [Fact]
    public void Validiere_fehler_bei_Arbeit_und_Urlaub_am_selben_Tag()
    {
        var tag = new DateOnly(2026, 1, 15);
        var eintraege = new[]
        {
            Arbeit(tag, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            Urlaub(tag),
        };

        ZeiteintragAbgabeValidator.Validiere(eintraege)
            .Should().Contain("gleichzeitig nicht erlaubt");
    }

    [Fact]
    public void Validiere_erlaubt_gleichen_Tag_bei_unterschiedlichem_Mandanten()
    {
        var tag = new DateOnly(2026, 1, 15);
        var eintraege = new[]
        {
            Arbeit(tag, new TimeOnly(8, 0), new TimeOnly(12, 0), mandantId: 1),
            Urlaub(tag, mandantId: 2),
        };

        ZeiteintragAbgabeValidator.Validiere(eintraege).Should().BeNull();
    }

    [Fact]
    public void Validiere_ok_bei_leerer_Liste()
    {
        ZeiteintragAbgabeValidator.Validiere([]).Should().BeNull();
    }
}
