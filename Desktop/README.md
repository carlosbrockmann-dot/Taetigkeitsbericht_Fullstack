# Tätigkeitsbericht

<strong style="color:blue">Dies ist ein Python-Projekt.</strong>

Die Zeitaufschreibung erfolgt nach dem Muster **Clean Architecture**, damit Kernlogik, Oberfläche und Datenbank getrennt bleiben und einzelne Teile wiederverwendbar sind.

Das Programm soll sich die Feiertage aus einer bekannten JSON-Adresse von Google herunter laden können, 
und die entsprechende Tage damit markieren.

<p style="color:red"> Das Programm ist noch in der Entwicklungsphase</p>

## Präsentation

Überblick über Funktionen und Oberflächen des Programms (Screenshots): **[Praesentation.ppt](./Praesentation.ppt)**.

## Clean Architecture

**Clean Architecture** (Robert C. Martin) ordnet Code in **Schichten** mit **nach innen gerichteten Abhängigkeiten**: äußere Schichten dürfen innere kennen, nicht umgekehrt. So bleiben Geschäftsregeln unabhängig von UI, Datenbank oder Frameworks.

| Schicht | Rolle | In diesem Projekt (`src/`) |
|---------|--------|----------------------------|
| **Domain** | Entitäten, Validierung, fachliche Regeln ohne Technik | `Core/Domain/` (z. B. `models/entities/`, `services/`, `interfaces/`) |
| **Application** | Anwendungsfälle, Orchestrierung der Domain | `Core/Application/` |
| **Presentation** | Benutzeroberfläche, Eingaben, Anzeige | `External/Presentation/Desktop/` |
| **Persistence** | Speicherung (SQLite, Repositories) | `External/Infrastructure/` |

Die **Dependency Rule** bedeutet: Pfeile zeigen immer zur **Domain** — Presentation und Persistence hängen von Application/Domain ab, nicht umgekehrt. Konkrete Datenbank- oder Qt-Details stehen daher nicht im Kern; sie werden über **Interfaces** (Repositories) und **Dependency Injection** (`App/bootstrap.py`) eingebunden.

<img src="./Clean_Architecture.jpg" alt="Schichtenmodell: Domain, Application, Presentation, Persistence" width="490" height="487" />

Die UI ist im Presentation Layer zwar im Schichtenmodell implementiert, aber nicht ganz im strengen Sinne nach Clean Architecture aufgebaut. Hierzu müsste man die UI per DI an den Application-Layer übergeben sollen, so dass alles von dort aus gesteuert wird. Vielleicht stelle ich das noch um. 


## Model

Domain-Modelle (Felder, Validierung, DTO): siehe [readme_models.md](./readme_models.md).

## Sollstunden in der Zeiterfassung

Beschreibung der Sollstunden-Berechnung (nach Vertrag und nach Stundenplan), Kategorie U/K, Kommentarregeln und Tages-Flags: siehe [readme_sollstunden.md](./readme_sollstunden.md).

## Desktop-Frontend

Diese Anwendung soll eine plattformübergreifendes Desktop-Frontend ergeben, das die Zeitaufschreibung in eine SQLite-Datenbank speichert. Die Auswahl der Spalten, die nach Excel exportiert werden sollen, ist in einer zentralen Config-Datei einstellbar (`src/config.toml`).

**Zeiteinträge (Kurzüberblick):**
- Spalten u. a. Kennzeichen (F/U/K/Sf/Bf), Von/Bis/Pausen, Geleistet, Soll, Vertrag, **Kat.** (leer/`U`/`K`), Kommentar; Export-Hilfsspalten können ausgeblendet sein.
- **Doppelklick auf Datum:** Zeiten aus dem Stundenplan; Kommentar aus der Stundenplan-Anmerkung, wenn leer oder genau ein Zeichen (Werktag, kein Feiertag).
- **Urlaub/Krankheit ohne Von:** optional Kommentar aus Stundenplan (`beim_urlaub_krank_modus_kommentar_aus_Stundenplan` in `config.toml`).
- **Abgeben:** Monat an das Backend (siehe unten); **Für Excel kopieren:** Zwischenablage laut `cell_spec`.

Hilfe im Programm: Reiter Zeiteinträge → siehe auch `src/External/Presentation/Desktop/hilfe/zeiteintraege.md`.

## Geplanter Server-Upload (siehe Repository-Root)

Registrierung und Monats-Abgabe an das Backend sind in der Desktop-UI angebunden:

- **Hamburger-Menü** (links oben) → „Am Backend registrieren…“ (modales Formular)
- Reiter **Zeiteinträge** → Button **„Abgeben“** (rechts neben „Für Excel kopieren“; Login + GraphQL-Upload)
- Reiter **Zeiteinträge** → Button **„Online ansehen“** (Login falls nötig, öffnet die React-Online-Ansicht im Standardbrowser; Monat, Mandant, Benutzername und Mandantenliste aus `mandanten.toml`)
- Backend-Fehler erscheinen in der **roten Leiste am unteren Fensterrand**

Konfiguration: `src/authentication.toml` (Vorlage: `authentication.example.toml`) mit `base_url` (HTTP `http://localhost:5108` oder HTTPS `https://localhost:7022`), `frontend_url` (React, Standard `http://localhost:5173`), Benutzername, Passwort und E-Mail. Bei lokalem HTTPS: Dev-Zertifikat vertrauen (`dotnet dev-certs https --trust`) oder `verify_ssl = false`.

Voraussetzungen: Backend läuft; für Online-Ansicht zusätzlich Frontend (`cd Frontend && npm run dev`); Konto ist registriert und E-Mail bestätigt.

Details: [../Planung.md](../Planung.md), [../Backend/README.md](../Backend/README.md), [../Frontend/README.md](../Frontend/README.md).

## Geplanter PDF-Export zum Ausdrucken

Geplant (noch nicht implementiert): Aus der Desktop-App sollen die Tätigkeiten / Zeiteinträge als **PDF** exportiert werden können, geeignet zum **Ausdrucken** (z. B. Monatsübersicht mit geleisteten Zeiten, optional dieselben Spalten wie beim Excel-Export bzw. über Config steuerbar).

**Reihenfolge:** Diese Funktion steht **ganz zum Schluss** – erst nach Server-Upload, Frontend-Übersicht und AWS-Integration. Aufgabenliste: [../ToDo.csv](../ToDo.csv) (Phase 5).

## Anbindung zur Datenbank

Die Persistenz erfolgt über SQLite. Die Anbindung geschieht über ein ORM (SQLModel auf Basis von SQLAlchemy); per Dependency Injection und Repository-Pattern bleiben die Aufrufer von der konkreten Speicherung entkoppelt. 

## Tests

Automatisierte Tests (pytest, In-Memory-SQLite, Schichten unter `test/`): siehe [test/README_tests.md](./test/README_tests.md).

## Python Setup

Empfohlen ist eine lokale virtuelle Umgebung (`venv`), damit Abhaengigkeiten isoliert sind.

### Linux (Bash)

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
pip install -r requirements.txt
pip install -r requirements-dev.txt
```

Zum Verlassen der Umgebung:

```bash
deactivate
```

Tests ausführen: `python -m pytest test/` — siehe [test/README_tests.md](./test/README_tests.md).

### Windows (PowerShell)

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
pip install -r requirements-dev.txt
```

Zum Verlassen der Umgebung:

```powershell
deactivate
```

Tests ausführen (nach Aktivierung der venv): `py -m pytest test/` — Details in [test/README_tests.md](./test/README_tests.md).

### Bemerkung zum Setup

Fall zusätzliche Pakete benötigt werden, sind sie im "pip install -r requirements.txt" mit drin... 