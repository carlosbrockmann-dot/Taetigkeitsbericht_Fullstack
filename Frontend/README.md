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
{frontend_url}/?jahr=2026&monat=8&mandantId=1#token=<JWT>
```

- **Query:** `jahr`, `monat`, `mandantId` – Monat und Mandantenfilter
- **Hash:** `token` – JWT (wird sofort in `sessionStorage` übernommen und aus der Adresszeile entfernt)

Ohne Token zeigt die Seite einen Hinweis, die Ansicht über den Desktop zu öffnen.

## Funktionen

- Tabelle der Zeiteinträge des gewählten Monats (Query `zeiteintraege` mit `von`/`bis`/`mandantId`)
- Navigation **Vorheriger / Nächster Monat**
- Kein Login-Formular, kein Bearbeiten, kein Speichern

## Technik

| | |
|--|--|
| Stack | React 19, TypeScript, Vite |
| API | `fetch` → GraphQL `POST`, Header `Authorization: Bearer …` |
| Auth | Token nur aus Desktop-Handoff / `sessionStorage` |

## Backend-Abhängigkeiten

- Query `zeiteintraege(von, bis, mandantId)` mit JWT
- CORS-Policy `Frontend` in `Backend/src/Program.cs` / `Cors:Origins` in `appsettings.json`
