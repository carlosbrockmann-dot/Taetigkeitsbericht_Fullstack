from __future__ import annotations

from datetime import date, time

from Core.Domain.models.models_worktime import Stundenplan
from test.support.factories import feiertag, krank, urlaub, zeiteintrags_dto
from test.support.fakes import dto_anwendung


def test_urlaub_ohne_arbeitszeit_setzt_nur_kategorie():
    montag = date(2025, 3, 10)
    app = dto_anwendung(urlaub=[urlaub(montag, montag)])
    dto = zeiteintrags_dto(datum=montag)
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.anmerkung in (None, "")
    assert dto.kategorie == "U"


def test_urlaub_ohne_von_kommentar_aus_stundenplan_wenn_aktiviert():
    montag = date(2025, 3, 10)
    plan = Stundenplan(
        id=1,
        mandant_id=1,
        wochentag=1,
        uhrzeit_von=time(8, 0),
        uhrzeit_bis=time(12, 0),
        anmerkung="Buero",
    )
    app = dto_anwendung(urlaub=[urlaub(montag, montag)], stundenplan=[plan])
    app.set_beim_urlaub_krank_modus_kommentar_aus_stundenplan(True)
    dto = zeiteintrags_dto(datum=montag)
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.kategorie == "U"
    assert dto.anmerkung == "Buero"


def test_urlaub_ohne_von_kein_stundenplan_kommentar_wenn_deaktiviert():
    montag = date(2025, 3, 10)
    plan = Stundenplan(
        id=1,
        mandant_id=1,
        wochentag=1,
        uhrzeit_von=time(8, 0),
        uhrzeit_bis=time(12, 0),
        anmerkung="Buero",
    )
    app = dto_anwendung(urlaub=[urlaub(montag, montag)], stundenplan=[plan])
    app.set_beim_urlaub_krank_modus_kommentar_aus_stundenplan(False)
    dto = zeiteintrags_dto(datum=montag)
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.kategorie == "U"
    assert dto.anmerkung in (None, "")


def test_urlaub_ohne_von_bestehender_kommentar_bleibt():
    montag = date(2025, 3, 10)
    plan = Stundenplan(
        id=1,
        mandant_id=1,
        wochentag=1,
        uhrzeit_von=time(8, 0),
        uhrzeit_bis=time(12, 0),
        anmerkung="Buero",
    )
    app = dto_anwendung(urlaub=[urlaub(montag, montag)], stundenplan=[plan])
    app.set_beim_urlaub_krank_modus_kommentar_aus_stundenplan(True)
    dto = zeiteintrags_dto(datum=montag, anmerkung="Privat")
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.anmerkung == "Privat"


def test_urlaub_mit_von_kein_stundenplan_kommentar():
    montag = date(2025, 3, 10)
    plan = Stundenplan(
        id=1,
        mandant_id=1,
        wochentag=1,
        uhrzeit_von=time(8, 0),
        uhrzeit_bis=time(12, 0),
        anmerkung="Buero",
    )
    app = dto_anwendung(urlaub=[urlaub(montag, montag)], stundenplan=[plan])
    app.set_beim_urlaub_krank_modus_kommentar_aus_stundenplan(True)
    dto = zeiteintrags_dto(
        datum=montag,
        uhrzeit_von=time(8, 0),
        uhrzeit_bis=time(12, 0),
    )
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.kategorie == "U"
    assert dto.anmerkung in (None, "")


def test_urlaub_mit_arbeitszeit_laesst_kommentar_unveraendert():
    montag = date(2025, 3, 10)
    app = dto_anwendung(urlaub=[urlaub(montag, montag)])
    dto = zeiteintrags_dto(
        datum=montag,
        uhrzeit_von=time(8, 0),
        uhrzeit_bis=time(12, 0),
        anmerkung="Meeting",
    )
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.anmerkung == "Meeting"
    assert dto.kategorie == "U"


def test_ueberstunden_frei_setzt_kommentar_und_geleistet_null():
    montag = date(2025, 3, 10)
    app = dto_anwendung()
    app.set_kommentar_ueberstunden_frei("Frei")
    dto = zeiteintrags_dto(
        datum=montag,
        uhrzeit_von=time(12, 0),
        uhrzeit_bis=time(12, 0),
    )
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.anmerkung == "Frei"
    assert dto.geleistete_stunden == time(0, 0, 0)
    assert dto.kategorie == ""


def test_feiertag_ohne_anmerkung_bekommt_namen():
    tag = date(2025, 1, 1)
    app = dto_anwendung(feiertage=[feiertag(tag, "Neujahr")])
    dto = zeiteintrags_dto(datum=tag)
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.anmerkung == "Neujahr"
    assert dto.ist_feiertag is True
    assert dto.kategorie == ""


def test_urlaub_am_samstag_keine_kategorie():
    samstag = date(2025, 3, 8)
    app = dto_anwendung(urlaub=[urlaub(samstag, samstag)])
    dto = zeiteintrags_dto(datum=samstag)
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.ist_urlaub is True
    assert dto.anmerkung in (None, "")
    assert dto.kategorie == ""


def test_krankheit_werktag_setzt_kategorie_k_ohne_kommentar():
    montag = date(2025, 3, 10)
    app = dto_anwendung(krank=[krank(montag, montag)])
    dto = zeiteintrags_dto(datum=montag)
    app.anreichere_eintraege_fuer_tag([dto])
    assert dto.anmerkung in (None, "")
    assert dto.kategorie == "K"
