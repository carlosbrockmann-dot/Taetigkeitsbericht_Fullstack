# Sollstunden in der Zeiterfassung

Die Sollwerte, Tages-Flags (Urlaub, Feiertag, …), **Kategorie** und Kommentarregeln werden in der Anwendungsschicht (`ZeiteintragAnwendungDTO` in `src/Core/Application/zeiteintrag_dto_anwendung.py`) berechnet und als `ZeiteintragsDTO` an die Desktop-Tabelle übergeben. Die GUI mappt DTOs auf Tabellenzeilen und zeigt sie an; bei Datumsänderungen ruft das ViewModel `anreichere_eintraege_fuer_tag` erneut auf.

**Tabellenlayout (Zeiteinträge):** Nach Datum fünf Kennzeichen-Spalten (Feiertag, Urlaub, Krank, Schulferien, Betriebsferien) mit Icons; danach Von/Bis/Pausen, Geleistet, Soll, Vertrag, **Kat.**, Kommentar. Ausgeblendete Hilfsspalten (Kalendertag/Feiertagsname/Schulferienname) dienen dem Excel-Export. Spaltenindices und `cell_spec` sind in `src/config.toml` dokumentiert (Indizes 0–20).

## Sollstunden nach Vertrag

- Quelle: Tabelle `sollstunden_vertrag` in der Datenbank. Beim **ersten Start** (leere Tabelle) Import aus `src/sollstunden_vertrag.toml`. Optional `sollstunden_vertrag_backup_erstellen = true` in `config.toml` → Backup nach `sollstunden_vertrag_backup.toml` und Platzhalter; bei `false` bleibt die TOML unverändert. `ZeiteintragAnwendungDTO` liest Vertragssoll datumsabhängig per `hole_gueltig_fuer_datum`. Wochentag 1 = Montag … 7 = Sonntag.
- Anzeige nur in der **ersten Tabellenzeile** je Kalendertag.
- Kein Wert, wenn für den Wochentag in der Config kein Eintrag steht oder die Zeit `0:00` ist.
- **Feiertage:** Steuert `[sollstunden].sollstunden_an_feiertagen`. Ist der Wert `false` (Standard), entfällt das Vertrags-Soll an Feiertagen; ist er `true`, gilt wie an einem normalen Tag nur der Wochentag aus `wochenstunden`.
- **Urlaub- und Krankheitstage:** Entscheidend ist die Spalte **Kategorie** (`U`/`K`, nur an Mo–Fr mit Vertrags-Soll) – nicht der Kommentartext. Stundenplan-Soll entfällt ohne Arbeitszeit. In Zeile 1 je Tag werden Vertrags-Soll und **geleistete Stunden** gesetzt (gleich Vertrags-Soll bzw. Vertrag + Arbeitszeit). Doppelklick zum Übernehmen aus dem Stundenplan bleibt möglich.

## Kategorie (Spalte Kat.)

- Leer = Arbeitstag; **`U`** = Urlaub; **`K`** = Krankheit (Krank vor Urlaub).
- Nur an **Mo–Fr mit positivem Vertrags-Soll**; Wochenende und Feiertage ohne Kategorie-Kürzel.
- Steuert Geleistet/Soll an U/K-Tagen; erscheint nicht mehr automatisch im Kommentar.

## Kommentar

Logik in `_wende_kommentar_regeln_an` und `_setze_kommentar_aus_stundenplan_bei_urlaub_krank` (`[sollstunden]` in `config.toml`):

- **Kein Auto-U/K** im Kommentar – dafür ist die Spalte **Kat.** zuständig.
- **Überstunden frei** (`kommentar_ueberstunden_frei`), wenn Von = Bis.
- An **Feiertagen** nur Feiertagsname, wenn der Kommentar noch leer ist.
- Optional **`beim_urlaub_krank_modus_kommentar_aus_Stundenplan`**: bei Kat. U/K, leerem **Von** und leerem Kommentar die Anmerkung des passenden Stundenplan-Blocks übernehmen; bei `false` keine Änderung.
- **Doppelklick auf Datum** (GUI): Kommentar aus Stundenplan-Anmerkung, wenn das Feld leer ist oder **genau ein Zeichen** hat (Werktag, kein Feiertag) – unabhängig von der Config-Option oben.

## Sollstunden nach Stundenplan

Die Soll-Arbeitszeit pro Tag ergibt sich aus der **Summe aller Stundenplan-Blöcke** des passenden Wochentags (Nettozeit je Block: Arbeitszeit minus Pausen, wie bei den geleisteten Stunden). Diese Tages-Summe wird auf die Zeiterfassungszeilen eines Tages **verteilt**, damit jede Zeile möglichst zum passenden Stundenplan-Eintrag passt (gleiche Reihenfolge wie beim Befüllen per Doppelklick auf das Datum).

**Ablauf je Kalendertag:**

1. Alle Stundenplan-Einträge des Wochentags werden nach `uhrzeit_von` sortiert (Block 1, Block 2, …).
2. Die Zeiterfassungszeilen des Tages werden in Tabellenreihenfolge nummeriert (Zeile 1, Zeile 2, …).
3. Zeile *i* erhält das Netto-Soll des Stundenplan-Blocks *i* (1:1-Zuordnung nach Index).
4. Gibt es **weniger Zeiterfassungszeilen als Stundenplan-Blöcke**, werden die Sollstunden der nicht zugeordneten Blöcke zur **letzten** Zeile des Tages addiert. So bleibt die Summe der angezeigten Sollstunden gleich der Tages-Summe aus dem Stundenplan.
5. Gibt es **mehr Zeiterfassungszeilen als Blöcke**, erhalten nur die ersten Zeilen einen Sollwert; weitere Zeilen bleiben in dieser Spalte leer.
6. An **Feiertagen** wird kein Stundenplan-Soll gesetzt.

**Beispiel** (Montag, Stundenplan: 08:00–12:00 und 13:00–17:00, je 4 Stunden netto):

| Zeiterfassungszeilen am Tag | Soll Stundenplan Zeile 1 | Soll Stundenplan Zeile 2 | Tages-Summe |
|----------------------------|--------------------------|--------------------------|-------------|
| 1 Zeile                    | 08:00                    | —                        | 08:00       |
| 2 Zeilen                   | 04:00                    | 04:00                    | 08:00       |
| 2 Zeilen, 3 Blöcke im Plan | 04:00                    | 06:00 (4 + 2 Rest)       | 10:00       |

Die Monatssumme in der Oberfläche summiert die Sollwerte **aller Zeilen**; sie entspricht damit der Summe der Stundenplan-Blöcke über alle erfassten Tage (ohne Feiertage).

Implementierung: `_stundenplan_bloecke_fuer_datum`, `_verteile_soll_stunden_nach_stundenplan`, `_setze_soll_felder_fuer_tag`.
