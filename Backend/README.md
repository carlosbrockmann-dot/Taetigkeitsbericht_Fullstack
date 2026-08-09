# Tätigkeitsbericht – Backend

<strong style="color:blue">Geplantes Python-Backend (noch nicht implementiert).</strong>

Eigenständige GraphQL-Applikation zum Empfang und zur Speicherung hochgeladener Tätigkeitsberichte sowie zur Abfrage geleisteter Stunden. Authentifizierung per Login; bei gültigen Zugangsdaten wird ein Token ausgestellt und von Clients im HTTP-Header mitgeschickt.

<p style="color:red">Status: Planung – siehe <a href="../Planung.md">../Planung.md</a></p>

## Ziele

- Upload einer **Liste von Zeiteinträgen im JSON-Format** (von der Desktop-App)
- Persistenz in einer Tabelle analog Desktop-`Zeiteintrag`, ergänzt um **Mitarbeiter-ID**
- **GraphQL**-API
- **Clean Architecture** und **SOLID**
- ORM (z. B. SQLModel/SQLAlchemy, angelehnt an Desktop)
- Datenbank: **PostgreSQL** lokal, **AWS Aurora DSQL** in der Cloud
- **AWS-VPC:** Backend in der VPC; DB-Zugriff nur intern hinter Firewall/Security Groups
- Von **außen** erreichbare GraphQL-API (HTTPS) für Frontend und Desktop – nur nötige Freigaben
- Desktop-Clients: nach Login **Token** plus **TLS mit privaten Zertifikatsdateien** (mTLS)

## Geplante Clean Architecture

Abhängigkeiten zeigen nach innen zur Domain; Frameworks und DB bleiben außen.

| Schicht | Rolle |
|---------|--------|
| **Domain** | Entitäten (Mitarbeiter, Zeiteintrag), fachliche Regeln, Repository-Interfaces |
| **Application** | Use Cases: Login, Upload-Liste speichern, Übersicht abfragen |
| **Infrastructure** | ORM, PostgreSQL/DSQL, Passwort-Hashing, Token-Ausstellung/-Prüfung, TLS/Client-Zertifikate |
| **Presentation** | GraphQL-Schema (Mutations/Queries), Auth-Kontext aus Header |

## Geplante API-Oberfläche (Skizze)

| Operation | Zweck |
|-----------|--------|
| Login | Benutzername + Passwort → Token |
| Upload Tätigkeitsbericht | JSON-Liste von Einträgen; Token im Header; Desktop zusätzlich private Zertifikate |
| Queries Übersicht | Geleistete Stunden (Filter Zeitraum / Mitarbeiter) |

## Datenbank und AWS-Netzwerk

- **Lokal:** PostgreSQL  
- **AWS:** Aurora DSQL in der **VPC**; Verbindung vom Backend nur über **interne** Security-Group-/Firewall-Regeln – **kein** öffentlicher DB-Zugang  
- Backend-API und (über Edge/ALB) das Frontend von außen für Benutzer freigeben (HTTPS)  
- Kernentität Zeiteintrag: Felder analog Desktop (`datum`, Zeiten, Pausen, Anmerkung, …) plus **`mitarbeiter_id`**

Orientierung Desktop-Modell: [../Desktop/readme_models.md](../Desktop/readme_models.md).  
Netzwerk-Zielbild und Aufgaben: [../Planung.md](../Planung.md) (AWS-Netzwerk, Phase 4).

## Abgrenzung

| Dieses Backend | Desktop | Frontend |
|----------------|---------|----------|
| Zentrale Speicherung und Auth; VPC + DB-Firewall | Lokale Erfassung; Upload mit Token + privaten Zertifikaten | React-Übersicht + Login; öffentlich in VPC erreichbar |

Das Backend bleibt **eigenständig** (eigener Prozess, eigene Abhängigkeiten, keine Vermischung mit Desktop-SQLite).

## Nächste Schritte

Aufgaben und Reihenfolge: **[../Planung.md](../Planung.md)** (Phase 1, Phase 4 VPC). Setup-Anleitung (venv, Migrationen, Startbefehl) folgt mit der Implementierung.
