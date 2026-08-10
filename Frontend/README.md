# Tätigkeitsbericht – Frontend

<strong style="color:blue">Geplante React-Applikation (noch nicht implementiert).</strong>

Web-Oberfläche zur Anmeldung und zur Übersicht der geleisteten Stunden. Kommunikation ausschließlich mit dem Backend, nicht mit der lokalen Desktop-SQLite-Datenbank.

<p style="color:red">Status: Planung – siehe <a href="../Planung.md">../Planung.md</a>. Backend-Technik: <a href="../README.md">../README.md</a></p>

## Ziele

- **Login** (Benutzername/Passwort) gegen das Backend; Token speichern und bei Anfragen im Header mitschicken
- **Übersicht** der geleisteten Stunden (Liste/Tabelle, Filter nach Zeitraum, Summen)
- Geschützte Bereiche nur nach erfolgreicher Anmeldung
- Logout und Behandlung abgelaufener Tokens
- Betrieb in **AWS-VPC**: von außen für Benutzer per HTTPS erreichbar; keine direkte DB-Anbindung aus dem Browser

## Geplante Technik

| Thema | Vorschlag |
|-------|-----------|
| UI | React (TypeScript), z. B. Vite |
| API | Client gegen die Backend-API (Basis-URL konfigurierbar) |
| Auth | Token nach Login → Authorization-Header |
| AWS | Deployment in VPC; öffentliche Freigabe nur für die Web-UI (und Aufruf der freigegebenen Backend-API) |

Konkrete Bibliotheken werden bei Implementierung festgelegt. Details zur Backend-API: [../README.md](../README.md), [../Planung.md](../Planung.md).

## Zusammenspiel und AWS

```
Benutzer (Browser)
    │  HTTPS (öffentliche Freigabe)
    ▼
┌──────────── AWS VPC ────────────┐
│  Frontend  →  Backend           │
│                  │              │
│                  ▼              │
│         Datenbank (intern,      │
│         Firewall/SG)            │
└─────────────────────────────────┘
```

- Frontend und Backend in der **VPC**
- Datenbank nur **intern** vom Backend erreichbar (Firewall / Security Groups)
- Benutzer erreichen Frontend (und die freigegebene Backend-API) von **außen**
- Desktop nutzt dasselbe Backend mit Login-Token und **privaten Zertifikatsdateien** (siehe Desktop)

Daten kommen aus Uploads der Desktop-App (JSON-Liste von Zeiteinträgen mit Mitarbeiter-Bezug).

## Abgrenzung

| Dieses Frontend | Backend | Desktop |
|-----------------|---------|---------|
| Anzeige und Login im Browser; öffentlich in VPC | API, Auth, Persistenz; DB nur intern | Lokale Erfassung; Upload mit Token + Zertifikaten |

## Nächste Schritte

Aufgaben und Reihenfolge: **[../Planung.md](../Planung.md)** (Phase 3, Phase 4 VPC). Projektgerüst und Startanleitung folgen mit der Implementierung.
