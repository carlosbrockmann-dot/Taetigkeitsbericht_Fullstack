"""Parsing und Netto-Arbeitszeit (Von/Bis minus Pause) fuer Zeiteintrag und Stundenplan."""

from __future__ import annotations

import re
from datetime import time


def parse_uhrzeit_minuten(text: str) -> int | None:
    """Minuten seit Mitternacht fuer eine Uhrzeit; unterstuetzt u.a. HH:MM, HH.MM, HH:MM:SS."""
    s = text.strip().replace("\u00a0", " ").strip()
    if not s:
        return None
    s = s.replace(",", ".")
    if re.fullmatch(r"\d{1,2}", s):
        h = int(s)
        if 0 <= h <= 23:
            return h * 60
        return None
    m = re.fullmatch(r"(\d{1,2})[:.](\d{1,2})(?:[:.](\d{2}))?", s)
    if not m:
        return None
    h = int(m.group(1))
    mi = int(m.group(2))
    if not (0 <= h <= 23 and 0 <= mi < 60):
        return None
    # Sekunden werden fuer die Anzeige nicht addiert (Minute-genau).
    return h * 60 + mi


def zeit_aus_text(text: str) -> time | None:
    """Wandelt gueltige Uhrzeit-Strings in datetime.time (ohne Sekunden-Anteil)."""
    minuten = parse_uhrzeit_minuten(text)
    if minuten is None:
        return None
    return time(hour=minuten // 60, minute=minuten % 60)


def netto_arbeitsminuten(
    uhrzeit_von: str,
    uhrzeit_bis: str,
    pause_von: str,
    pause_bis: str,
    pause2_von: str = "",
    pause2_bis: str = "",
) -> int | None:
    """Arbeitszeit netto: (Bis - Von) minus Ueberlappung mit [Pause Von, Pause Bis].

    Die abgezogene Pausendauer ist (Pause Bis - Pause Von), begrenzt auf den Bereich Von…Bis.
    Nur wenn Pause Von und Pause Bis gueltig und Pause Ende nach Pause Beginn liegen, wird abgezogen.
    """
    von_m = parse_uhrzeit_minuten(uhrzeit_von)
    bis_m = parse_uhrzeit_minuten(uhrzeit_bis)
    if von_m is None or bis_m is None:
        return None
    if bis_m <= von_m:
        return None
    brutto = bis_m - von_m
    pausen = (
        (pause_von, pause_bis),
        (pause2_von, pause2_bis),
    )
    for pause_start, pause_ende in pausen:
        pv = parse_uhrzeit_minuten(pause_start)
        pb = parse_uhrzeit_minuten(pause_ende)
        if pv is not None and pb is not None and pb > pv:
            pause_im_block_anfang = max(von_m, pv)
            pause_im_block_ende = min(bis_m, pb)
            if pause_im_block_ende > pause_im_block_anfang:
                brutto -= pause_im_block_ende - pause_im_block_anfang
    if brutto <= 0:
        return None
    return brutto


def minuten_als_hh_mm(minuten: int) -> str:
    if minuten < 0:
        minuten = 0
    h, mi = divmod(minuten, 60)
    return f"{h:02d}:{mi:02d}"
