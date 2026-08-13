# Tätigkeitsbericht – Frontend (Online-Ansicht)

React-App (Vite + TypeScript) zur **reinen Ansicht** der Zeiteinträge aus dem Backend. Bearbeitung erfolgt in der Desktop-App; dieses Frontend navigiert nur zwischen Monaten für den übergebenen Mandanten.

## Voraussetzungen

- Node.js 20+ (npm)
- Laufendes Backend (`http://localhost:5108` oder HTTPS-Profil)
- CORS im Backend erlaubt die Frontend-Origin (Standard: `http://localhost:5173`)
- Desktop-App mit gültiger `authentication.toml` (Login + `frontend_url`)

## Start

```powershell
cd Frontend
npm install
npm run dev
```

Standard-URL: **http://localhost:5173**

Produktion-Build:

```powershell
npm run build
npm run preview
```

## Konfiguration

Datei `.env` (Vorlage `.env.example`):

| Variable | Bedeutung | Default |
|----------|-----------|---------|
| `VITE_GRAPHQL_URL` | GraphQL-Endpunkt | `http://localhost:5108/graphql` |

## Aufruf vom Desktop

Der Button **„Online ansehen“** in den Zeiteinträgen:

1. Meldet sich am Backend an, falls noch kein JWT vorliegt (`authentication.toml`).
2. Öffnet den Browser mit URL der Form:

```text
{frontend_url}/?jahr=2026&monat=8&mandantId=1#token=<JWT>&benutzername=<user>&mandanten=<JSON>
```

- **Query:** `jahr`, `monat`, `mandantId` – Monat und Mandantenfilter
- **Hash:** (Browser-Navigation kann keine HTTP-Header setzen; Handoff analog zum Token)
  - `token` – JWT
  - `benutzername` – Login-Name aus Desktop `authentication.toml`
  - `mandanten` – JSON-Array aus Desktop `mandanten.toml` (u. a. `id`, `name`, `kuerzel`, Farben)
- Hash-Werte werden sofort in `sessionStorage` übernommen und aus der Adresszeile entfernt (`session.mandanten`, `session.benutzername`).
- **Browser-Refresh** stellt die Session aus dem `sessionStorage` wieder her (Login und Navigation bleiben erhalten, solange der Tab/die Browsersitzung läuft).
- Ohne Token im Hash **und** ohne gespeicherte Session: Hinweis, die Ansicht über den Desktop zu öffnen.

## Funktionen

- Tabelle der Zeiteinträge des gewählten Monats (Query `zeiteintraege` mit `von`/`bis`/`mandantId`)
- Navigation **Vorheriger / Nächster Monat**
- Bei mehreren Mandanten: Combobox im Header zum Wechseln (`mandantId` in URL + Neuladen)
- Menü rechts neben der Überschrift: Wechsel zwischen **Tabellenansicht** (`/`) und **Hilfe & Disclaimer** (`/disclaimer`)
- Kein Login-Formular, kein Bearbeiten, kein Speichern

## Technik

| | |
|--|--|
| Stack | React 19, TypeScript, Vite, React Router |
| API | `fetch` → GraphQL `POST`, Header `Authorization: Bearer …` |
| Auth | Token nur aus Desktop-Handoff (ohne Token im Hash: keine Session) |
| Routing | `/` Monats-/Tabellenansicht, `/disclaimer` Hilfe & Disclaimer |

## Backend-Abhängigkeiten

- Query `zeiteintraege(von, bis, mandantId)` mit JWT
- CORS-Policy `Frontend` in `Backend/src/Program.cs` / `Cors:Origins` in `appsettings.json`
