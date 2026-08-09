# Planung: Tätigkeitsbericht – Server, Upload und Übersicht

Dieses Dokument beschreibt die geplanten Aufgaben. **Noch keine Implementierung.**

Ziel: Die vorhandene Desktop-Applikation (`Desktop/`) kann geleistete Zeiten als JSON-Liste an ein eigenständiges Backend hochladen. Ein React-Frontend zeigt die Übersicht. Authentifizierung erfolgt per Login; gültige Anmeldedaten liefern ein Token, das im HTTP-Header mitgeschickt wird. Die API ist GraphQL. Persistenz lokal mit PostgreSQL, in AWS mit Aurora DSQL.

Orientierung für die Server-Tabelle: Desktop-`Zeiteintrag` (siehe `Desktop/readme_models.md`), ergänzt um **Mitarbeiter-ID**.

**AWS-Netzwerk:** Backend und Frontend laufen in einer VPC mit Security Groups / Firewall-Regeln. Die Datenbank ist nur intern erreichbar; Frontend und Backend sind von außen für Benutzer mit den nötigen Freigaben zugänglich. Die Desktop-App spricht nach Login mit dem Backend über TLS und **private Zertifikatsdateien** (Client-Zertifikat / CA).

---

## Übersicht der Komponenten

| Komponente | Ordner | Rolle |
|------------|--------|--------|
| Desktop | `Desktop/` | Lokale Zeiterfassung (SQLite); neu: Login + Upload zum Server (Token + private Zertifikate) |
| Backend | `Backend/` | Eigenständige GraphQL-API, Clean Architecture, ORM, Auth, Speicherung; AWS in VPC |
| Frontend | `Frontend/` | React-App: Login + Übersicht geleisteter Stunden; AWS in VPC, öffentlich erreichbar |

---

## AWS-Netzwerk und Sicherheit (Zielbild)

```
Internet / Benutzer / Desktop
        │
        │  HTTPS (öffentliche Freigaben: Frontend-UI, Backend-API)
        │  Desktop zusätzlich: mTLS / private Zertifikatsdateien + Login-Token
        ▼
┌──────────────────────── VPC ────────────────────────┐
│  Public / Edge          Private App                 │
│  (ALB / Ingress)   →    Frontend, Backend           │
│                              │                      │
│                              │  nur intern          │
│                              ▼                      │
│                         Aurora DSQL                 │
│                    (kein öffentlicher DB-Zugang)    │
└─────────────────────────────────────────────────────┘
```

| Verbindung | Absicherung |
|------------|-------------|
| Benutzer → Frontend | Öffentliche Freigabe (HTTPS), nur benötigte Ports |
| Benutzer / Browser → Backend | Öffentliche Freigabe der GraphQL-API (HTTPS), CORS begrenzt |
| Desktop → Backend | Nach Login: Token im Header **und** TLS mit privaten Zertifikatsdateien |
| Backend → Aurora DSQL | Nur VPC-intern; Security Groups / Firewall: DB nicht aus dem Internet |
| Frontend → Backend (serverseitig, falls relevant) | Innerhalb VPC bzw. über freigegebene API-URL |

---

## Phase 0 – Grundlagen und Abstimmungen

- [ ] **0.1** Datenkontrakt festlegen: JSON-Schema einer Upload-Liste (Felder analog `Zeiteintrag` + `mitarbeiter_id`, Datums-/Zeitformate, Mandantenbezug falls nötig).
- [ ] **0.2** Auth-Kontrakt festlegen: Login-Mutation bzw. Endpoint, Token-Typ (z. B. JWT), Header-Name (z. B. `Authorization: Bearer …`), Ablaufzeit.
- [ ] **0.3** GraphQL-Oberfläche skizzieren: Login, Upload der Zeiteinträge, Abfragen für Übersicht (Filter nach Mitarbeiter, Zeitraum).
- [ ] **0.4** DB-Zielbilder klären: lokales PostgreSQL vs. AWS Aurora DSQL (Treiber, Connection-String, Einschränkungen DSQL).
- [ ] **0.5** Verzeichnisstruktur Backend/Frontend anlegen (nur Gerüst, siehe Phasen 1–2); bestehende Desktop-Struktur nicht unnötig umbauen.
- [ ] **0.6** AWS-VPC-Konzept skizzieren: Subnets (öffentlich/privat), Security Groups, welche Ports von außen (Frontend, Backend-API) vs. nur intern (DB).
- [ ] **0.7** Desktop-Zertifikatsmodell festlegen: private Zertifikatsdateien (Client-Zertifikat, privater Schlüssel, CA), Speicherort/Config, Ausstellung und Rotation.

---

## Phase 1 – Backend (`Backend/`)

Eigenständige Python-Applikation, **Clean Architecture**, **SOLID**. GraphQL-API. ORM (an Desktop angelehnt: SQLAlchemy / SQLModel o. ä.).

### 1.1 Architektur und Projektgerüst

- [ ] **1.1.1** Schichten anlegen: Domain, Application, Infrastructure (Persistence, Auth), Presentation (GraphQL).
- [ ] **1.1.2** Dependency Rule und Dependency Injection: äußere Schichten hängen von inneren ab; Repositories hinter Interfaces.
- [ ] **1.1.3** Konfiguration (Umgebung): DB-URL, Secret für Tokens, CORS für Frontend; Trennung lokal / AWS; TLS-/Zertifikatspfad für Desktop-Clients.
- [ ] **1.1.4** `requirements.txt` / Dev-Abhängigkeiten, README (siehe `Backend/README.md`), lokaler Start (z. B. Uvicorn).
- [ ] **1.1.5** TLS am API-Endpunkt: Server-Zertifikat; optionale/verpflichtende Client-Zertifikatsprüfung für Desktop-Upload (mTLS).

### 1.2 Domäne und Persistenz

- [ ] **1.2.1** Entität **Mitarbeiter** (mindestens ID, Benutzername, Passwort-Hash; weitere Felder nach Bedarf).
- [ ] **1.2.2** Entität **Zeiteintrag** (Server): Felder wie Desktop-`Zeiteintrag` / `ArbeitszeitBasis`, plus **`mitarbeiter_id`** (Pflicht, FK).
- [ ] **1.2.3** ORM-Modelle und Migrationen (einfache Tabelle(n); Index auf `mitarbeiter_id`, ggf. `(mitarbeiter_id, datum)`).
- [ ] **1.2.4** Repository-Interfaces und Implementierungen (Clean Architecture).
- [ ] **1.2.5** Lokale PostgreSQL-Anbindung (Docker optional dokumentieren).
- [ ] **1.2.6** AWS Aurora DSQL: Connection/Adapter, Konfigurationsprofil; Abweichungen zu klassischem PostgreSQL dokumentieren und abfangen.

### 1.3 Authentifizierung

- [ ] **1.3.1** Passwort-Hashing (z. B. Argon2/bcrypt); kein Klartext in der DB.
- [ ] **1.3.2** Login-Anwendungsfall: Benutzername + Passwort prüfen → Token ausstellen.
- [ ] **1.3.3** Token-Validierung für geschützte GraphQL-Operationen (Kontext / Middleware); bei ungültigem/fehlendem Token ablehnen.
- [ ] **1.3.4** Seed oder Admin-Weg für erste Testbenutzer (nur Entwicklung).

### 1.4 GraphQL-API

- [ ] **1.4.1** GraphQL-Framework einbinden (z. B. Strawberry oder Ariadne) passend zum Stack.
- [ ] **1.4.2** Mutation **Login** → Token (+ ggf. Mitarbeiter-Infos).
- [ ] **1.4.3** Mutation **Upload Tätigkeitsbericht**: Body als **Liste von Einträgen im JSON-Format** (Input-Typ / JSON-Scalar); Authentifizierung über Token im Header; `mitarbeiter_id` aus Token ableiten oder gegen Token prüfen.
- [ ] **1.4.4** Validierung der Liste (Pflichtfelder, Zeitfenster/Pausen analog Desktop-Regeln wo sinnvoll).
- [ ] **1.4.5** Queries für Übersicht: geleistete Stunden je Zeitraum / Mitarbeiter (Aggregation oder Rohdaten für Frontend).
- [ ] **1.4.6** Fehler- und Idempotenz-Strategie (Duplikate, erneuter Upload desselben Monats) festlegen und umsetzen.

### 1.5 Qualität Backend

- [ ] **1.5.1** Unit-Tests Domain/Application; Integrationstests Repository + GraphQL (Test-DB).
- [ ] **1.5.2** SOLID prüfen: SRP in Use Cases, DIP über Interfaces, keine Framework-Leaks in die Domain.

---

## Phase 2 – Desktop-Erweiterung (`Desktop/`)

Anbindung der bestehenden lokalen App an das Backend (Upload der geleisteten Zeiten).

- [ ] **2.1** Login-UI (Benutzername/Passwort) vor dem Upload; Speicherung des Tokens nur lokal/sessionbezogen (nicht ins Klartext-Log).
- [ ] **2.2** Client für Backend: Login-Aufruf, danach geschützte Upload-Mutation mit Token im Header.
- [ ] **2.3** Mapping lokaler `Zeiteintrag`-Daten → JSON-Liste gemäß Kontrakt (Phase 0.1); Zeitraum-Auswahl (z. B. Monat) für den Upload.
- [ ] **2.4** Konfiguration der Backend-URL und der **privaten Zertifikatsdateien** (`config.toml` / `external_api.toml`: Pfade zu Client-Zertifikat, Schlüssel, CA).
- [ ] **2.5** HTTPS-Client mit Zertifikaten: TLS-Verbindung zum Backend erst nach Login und mit konfigurierten privaten Zertifikatsdateien.
- [ ] **2.6** Feedback in der UI: Erfolg, Validierungs-/Auth-/Zertifikatsfehler.
- [ ] **2.7** Tests für Mapping und (soweit sinnvoll) Client mit Fake/HTTP-Mock (inkl. Zertifikatspfad-Konfiguration).
- [ ] **2.8** Desktop-README um Upload, Login und Zertifikate ergänzen.

---

## Phase 3 – Frontend (`Frontend/`)

React-Applikation, kommuniziert mit dem Backend.

- [ ] **3.1** Projekt anlegen (Vite + React + TypeScript empfohlen); README (`Frontend/README.md`).
- [ ] **3.2** GraphQL-Client (z. B. Apollo oder urql); Basis-URL konfigurierbar.
- [ ] **3.3** Login-Seite: Benutzername/Passwort → Token speichern (z. B. Speicher + Header-Interceptor).
- [ ] **3.4** Geschützte Routen: ohne Token zur Login-Seite.
- [ ] **3.5** Übersicht geleisteter Stunden: Tabelle/Liste (Filter Zeitraum, Anzeige je Tag/Eintrag, Summen).
- [ ] **3.6** Logout und Token-Ablauf behandeln.
- [ ] **3.7** Basis-Styling und responsive Darstellung; an bestehendes Produkt anbinden, ohne überladenes Dashboard.
- [ ] **3.8** Smoke-Tests / grundlegende Komponenten-Tests nach Bedarf.

---

## Phase 4 – Integration, AWS-VPC und Betrieb

- [ ] **4.1** End-to-End lokal: Desktop-Login → Upload JSON-Liste → Einträge in PostgreSQL → Anzeige im React-Frontend.
- [ ] **4.2** CORS und Auth-Header über alle drei Komponenten abstimmen.
- [ ] **4.3** **VPC anlegen/konfigurieren:** öffentliche und private Subnets; Backend und Frontend in der VPC betreiben.
- [ ] **4.4** **Firewall / Security Groups:** Aurora DSQL nur von Backend (App-Subnet) erreichbar; kein öffentlicher DB-Zugriff.
- [ ] **4.5** **Öffentliche Freigaben:** Frontend (HTTPS) und Backend-API (HTTPS/GraphQL) von außen für Benutzer erreichbar – nur nötige Ports/Quellen.
- [ ] **4.6** Desktop-Zugang: API von außen erreichbar, aber TLS mit privaten Zertifikatsdateien + Login-Token; Zertifikatsausstellung dokumentieren.
- [ ] **4.7** Betriebskonzept Aurora DSQL (Secrets Manager, Netzwerk, Deployment-Skizze Infra-as-Code wo sinnvoll).
- [ ] **4.8** Root-README und Komponenten-READMEs final an den Ist-Stand anpassen.

---

## Offene Punkte (vor Implementierung klären)

1. Exakte Upload-Semantik: nur Einfügen, Upsert, oder Monats-Ersatz?
2. Darf ein Mitarbeiter nur eigene Daten sehen, oder gibt es Rollen (Admin/Auswertung)?
3. Soll `mandant_id` aus dem Desktop mit auf den Server?
4. GraphQL-Bibliothek und ORM final wählen (Vorschlag: SQLModel/SQLAlchemy + Strawberry, konsistent zum Desktop).
5. Aurora-DSQL-Kompatibilität der gewählten ORM-Features (Migrationen, Transaktionen) prüfen.
6. mTLS nur für Desktop, oder auch für Browser-Frontend? (Browser typisch ohne Client-Zertifikat; Desktop mit privaten Zertifikatsdateien.)
7. Wer stellt die privaten Zertifikate aus (interne CA, ACM Private CA, manuell)?

---

## Empfohlene Reihenfolge

1. Phase 0 (Kontrakte inkl. VPC- und Zertifikatskonzept)  
2. Phase 1 Backend (Auth + Tabelle + Upload + Query + TLS)  
3. Phase 2 Desktop (Login + Upload + private Zertifikate)  
4. Phase 3 Frontend (Login + Übersicht)  
5. Phase 4 Integration / AWS-VPC / Firewall-Freigaben  

Keine der Phasen ist in diesem Stand bereits implementiert; dieses Dokument dient nur der Aufgabenplanung.
