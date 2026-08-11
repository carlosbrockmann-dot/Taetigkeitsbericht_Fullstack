from __future__ import annotations

from datetime import date, time

from Core.Domain.models.models_worktime import ZeiteintragsDTO
from External.Presentation.Desktop.zeiteintrag_view_model import ZeiteintragViewModel


def _dto(
    tag: date,
    *,
    mandant_id: int | None = 1,
    kategorie: str = "",
    von: time | None = None,
    bis: time | None = None,
) -> ZeiteintragsDTO:
    return ZeiteintragsDTO(
        datum=tag,
        mandant_id=mandant_id,
        kategorie=kategorie,
        uhrzeit_von=von,
        uhrzeit_bis=bis,
    )


def test_normalisiere_arbeit_mehrere_uhrzeiten_pro_tag():
    tag = date(2025, 3, 10)
    eintraege = [
        _dto(tag, von=time(8, 0), bis=time(12, 0)),
        _dto(tag, von=time(13, 0), bis=time(17, 0)),
        _dto(tag, von=time(8, 0), bis=time(12, 0)),  # Duplikat
    ]
    result = ZeiteintragViewModel._normalisiere_fuer_monatsabgabe(eintraege)
    assert len(result) == 2
    zeiten = {(e.uhrzeit_von, e.uhrzeit_bis) for e in result}
    assert zeiten == {(time(8, 0), time(12, 0)), (time(13, 0), time(17, 0))}


def test_normalisiere_urlaub_nur_einmal_pro_tag_mandant():
    tag = date(2025, 3, 10)
    eintraege = [
        _dto(tag, kategorie="U"),
        _dto(tag, kategorie="U", von=time(8, 0), bis=time(12, 0)),
    ]
    result = ZeiteintragViewModel._normalisiere_fuer_monatsabgabe(eintraege)
    assert len(result) == 1
    assert result[0].kategorie == "U"


def test_normalisiere_krankheit_und_arbeit_verschiedene_mandanten():
    tag = date(2025, 3, 10)
    eintraege = [
        _dto(tag, mandant_id=1, kategorie="K"),
        _dto(tag, mandant_id=2, von=time(9, 0), bis=time(12, 0)),
        _dto(tag, mandant_id=2, von=time(13, 0), bis=time(15, 0)),
    ]
    result = ZeiteintragViewModel._normalisiere_fuer_monatsabgabe(eintraege)
    assert len(result) == 3
