using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;

namespace Taetigkeitsbericht.Backend.Tests.Integration.Repositories;

public class ZeiteintragRepositoryTests : IAsyncLifetime
{
    private readonly SqliteAppDbFixture _fixture = new();
    private ZeiteintragRepository _sut = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _sut = new ZeiteintragRepository(_fixture.Db);
    }

    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task AddRangeAsync_vergibt_ids_und_speichert()
    {
        var mitarbeiterId = _fixture.MitarbeiterId;
        var eintraege = new[]
        {
            new Zeiteintrag
            {
                MitarbeiterId = mitarbeiterId,
                MandantId = 1,
                Datum = new DateOnly(2026, 3, 1),
                Kategorie = ZeiteintragKategorie.Arbeitstag,
                UhrzeitVon = new TimeOnly(8, 0),
                UhrzeitBis = new TimeOnly(16, 0),
                Anmerkung = "Projekt A",
            },
        };

        var gespeichert = await _sut.AddRangeAsync(eintraege);

        gespeichert.Should().HaveCount(1);
        gespeichert[0].Id.Should().NotBe(Guid.Empty);
        (await _fixture.Db.Zeiteintraege.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetByMitarbeiterUndZeitraumAsync_filtert_nach_datum_und_mandant()
    {
        var mitarbeiterId = _fixture.MitarbeiterId;
        await _sut.AddRangeAsync(
        [
            Eintrag(mitarbeiterId, new DateOnly(2026, 2, 1), mandantId: 1),
            Eintrag(mitarbeiterId, new DateOnly(2026, 2, 15), mandantId: 1),
            Eintrag(mitarbeiterId, new DateOnly(2026, 2, 20), mandantId: 2),
            Eintrag(mitarbeiterId, new DateOnly(2026, 3, 1), mandantId: 1),
        ]);

        var gefiltert = await _sut.GetByMitarbeiterUndZeitraumAsync(
            mitarbeiterId,
            von: new DateOnly(2026, 2, 1),
            bis: new DateOnly(2026, 2, 28),
            mandantId: 1);

        gefiltert.Should().HaveCount(2);
        gefiltert.Select(z => z.Datum).Should().BeInAscendingOrder();
        gefiltert.Should().OnlyContain(z => z.MandantId == 1);
    }

    [Fact]
    public async Task ReplaceMonatAsync_ersetzt_nur_zielmonat_und_mandant()
    {
        var mitarbeiterId = _fixture.MitarbeiterId;
        await _sut.AddRangeAsync(
        [
            Eintrag(mitarbeiterId, new DateOnly(2026, 1, 10), mandantId: 1, anmerkung: "alt"),
            Eintrag(mitarbeiterId, new DateOnly(2026, 1, 11), mandantId: 2, anmerkung: "anderer-mandant"),
            Eintrag(mitarbeiterId, new DateOnly(2026, 2, 5), mandantId: 1, anmerkung: "anderer-monat"),
        ]);

        var ersetzt = await _sut.ReplaceMonatAsync(
            mitarbeiterId,
            mandantId: 1,
            jahr: 2026,
            monat: 1,
            [
                Eintrag(mitarbeiterId, new DateOnly(2026, 1, 12), mandantId: 1, anmerkung: "neu"),
            ]);

        ersetzt.Should().HaveCount(1);
        ersetzt[0].Anmerkung.Should().Be("neu");

        var alle = await _fixture.Db.Zeiteintraege.AsNoTracking().ToListAsync();
        alle.Should().HaveCount(3);
        alle.Should().ContainSingle(z => z.Anmerkung == "neu");
        alle.Should().ContainSingle(z => z.Anmerkung == "anderer-mandant");
        alle.Should().ContainSingle(z => z.Anmerkung == "anderer-monat");
        alle.Should().NotContain(z => z.Anmerkung == "alt");
    }

    private static Zeiteintrag Eintrag(
        int mitarbeiterId,
        DateOnly datum,
        int? mandantId,
        string? anmerkung = null) => new()
    {
        MitarbeiterId = mitarbeiterId,
        MandantId = mandantId,
        Datum = datum,
        Kategorie = ZeiteintragKategorie.Arbeitstag,
        UhrzeitVon = new TimeOnly(9, 0),
        UhrzeitBis = new TimeOnly(17, 0),
        Anmerkung = anmerkung,
    };
}
