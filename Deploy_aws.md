# Deploy auf AWS – konkrete Schritte

Diese Anleitung beschreibt **ausführlich**, wie Backend und Frontend in AWS betrieben und bei jedem Commit auf `main` aktualisiert werden.

Die **Desktop-Applikation bleibt on premises** (lokal auf den PCs der Benutzer, SQLite lokal). Sie spricht das Backend nur über das Internet (Login, **Abgeben**, **Online ansehen**).

| | |
|--|--|
| Repository | [Taetigkeitsbericht_Fullstack](https://github.com/carlosbrockmann-dot/Taetigkeitsbericht_Fullstack) |
| Kurzreferenz Infra | [infra/README.md](./infra/README.md) |
| Pipeline | [`.github/workflows/deploy-aws.yml`](./.github/workflows/deploy-aws.yml) |
| App-Stack | [`infra/cloudformation/taetigkeitsbericht-aws.yml`](./infra/cloudformation/taetigkeitsbericht-aws.yml) |
| OIDC-Stack | [`infra/cloudformation/github-oidc.yml`](./infra/cloudformation/github-oidc.yml) |

---

## Inhaltsverzeichnis

1. [Zielbild](#zielbild)
2. [Was Sie nicht brauchen](#was-sie-nicht-brauchen)
3. [Voraussetzungen](#voraussetzungen)
4. [Schritt 0 – AWS-Konsole öffnen und Region wählen](#schritt-0--aws-konsole-öffnen-und-region-wählen)
5. [Schritt 1 – OIDC-Rolle anlegen (ohne Access Keys)](#schritt-1--oidc-rolle-anlegen-ohne-access-keys)
6. [Schritt 2 – GitHub Secrets setzen](#schritt-2--github-secrets-setzen)
7. [Schritt 3 – Optional: EC2-Key-Pair](#schritt-3--optional-ec2-key-pair)
8. [Schritt 4 – Code auf main und ersten Deploy](#schritt-4--code-auf-main-und-ersten-deploy)
9. [Schritt 5 – Was die Pipeline konkret macht](#schritt-5--was-die-pipeline-konkret-macht)
10. [Schritt 6 – Aurora DSQL einrichten](#schritt-6--aurora-dsql-einrichten)
11. [Schritt 7 – Frontend-URL festlegen und erneut deployen](#schritt-7--frontend-url-festlegen-und-erneut-deployen)
12. [Schritt 8 – Desktop on premises anbinden](#schritt-8--desktop-on-premises-anbinden)
13. [Schritt 9 – Wiederkehrender Betrieb](#schritt-9--wiederkehrender-betrieb)
14. [Checkliste](#checkliste-nach-dem-ersten-erfolgreichen-deploy)
15. [Häufige Probleme](#häufige-probleme)
16. [Sandbox-Hinweise](#sandbox-hinweise)

---

## Zielbild

| Komponente | Wo | Aufgabe |
|------------|-----|---------|
| **Desktop** | On premises | Zeiterfassung in SQLite; Upload und Browser-Ansicht gegen AWS |
| **Backend** | AWS EC2 (öffentlich erreichbar) | GraphQL-API, JWT-Auth, Speichern/Lesen |
| **Frontend** | AWS EC2 (öffentlich erreichbar) | React-Monatsansicht; ruft Backend-URL auf |
| **Datenbank** | AWS Aurora DSQL | Persistenz; **kein** öffentlicher DB-Zugang |
| **Netzwerk** | VPC + PrivateLink | Backend → DSQL nur privat in der VPC |
| **CI/CD** | GitHub Actions | Bei Push auf `main`: Infra aktualisieren + Apps ausrollen |

```
                    Internet
                       │
     ┌─────────────────┼─────────────────┐
     │                 │                 │
     ▼                 ▼                 ▼
[ Desktop ]      [ Browser ]      (optional SSH/SSM
 on premises)    Online-Ansicht    nur Admin)
     │                 │
     │ HTTPS           │ HTTPS
     └────────┬────────┘
              ▼
     ┌──────────────────── VPC ────────────────────┐
     │  EC2 Frontend (Nginx + SPA)                 │
     │  EC2 Backend  (.NET GraphQL :5108)            │
     │         │                                     │
     │         │ Port 5432 nur intern                │
     │         ▼                                     │
     │  VPC Endpoint (PrivateLink) → Aurora DSQL     │
     └───────────────────────────────────────────────┘
```

**Datenfluss Desktop:** Lokal erfassen → optional **Abgeben** → Daten liegen in DSQL → **Online ansehen** zeigt die Cloud-Daten. Die lokale SQLite bleibt unberührt von der Cloud-Löschung (Sandbox-Ende).

---

## Was Sie nicht brauchen

| Nicht nötig | Warum |
|-------------|--------|
| Eigenen **IAM-Benutzer** nur für Deploy anlegen | Pipeline nutzt eine **IAM-Rolle** per OIDC |
| **Access Keys** (programmatischer Zugriff) | Viele Sandboxes bieten keinen Key-Button – das ist ok |
| Desktop in AWS installieren | Desktop bleibt on premises |
| Öffentlicher DB-Port | DSQL nur über PrivateLink |

---

## Voraussetzungen

1. AWS-Konto oder **Sandbox** mit Rechten für: VPC, EC2, IAM, CloudFormation, SSM, S3, Aurora DSQL.
2. Login in die AWS-Konsole möglich (**Konsolenpasswort** reicht).
3. GitHub-Zugriff auf das Repo (Secrets schreiben, Actions starten).
4. Region, in der **Aurora DSQL** verfügbar ist (in der Konsole unter Aurora DSQL prüfen).
5. Dateien im Repo vorhanden (nach Pull/Push von `main`):
   - `.github/workflows/deploy-aws.yml`
   - `infra/cloudformation/github-oidc.yml`
   - `infra/cloudformation/taetigkeitsbericht-aws.yml`
   - `infra/scripts/post-dsql-setup.sql`

---

## Schritt 0 – AWS-Konsole öffnen und Region wählen

### Konsole aus PowerShell öffnen

```powershell
Start-Process "https://console.aws.amazon.com/"
```

Falls Ihre Sandbox eine **eigene Lab-URL** hat, diese verwenden (steht in der Sandbox-Doku / im Lab-Portal).

### Region einstellen

Oben rechts in der AWS-Konsole die Region wählen (z. B. `eu-central-1`, `us-east-1`).  
**Dieselbe Region** später als GitHub-Secret `AWS_REGION` eintragen. CloudFormation-, EC2- und DSQL-Ressourcen müssen in dieser Region liegen.

Account-ID notieren (oben rechts unter dem Kontomenü) – wird für `AWS IAM GRANT` bei DSQL gebraucht.

---

## Schritt 1 – OIDC-Rolle anlegen (ohne Access Keys)

Ziel: GitHub Actions darf kurzzeitig eine IAM-Rolle übernehmen, **ohne** Access Key.

### 1.1 Template lokal bereithalten

Datei im Repo: `infra/cloudformation/github-oidc.yml`

Parameter später:

| Parameter | Beispiel |
|-----------|----------|
| `GitHubOrg` | `carlosbrockmann-dot` (GitHub-Benutzer oder Organisation) |
| `GitHubRepo` | `Taetigkeitsbericht_Fullstack` |
| `RoleName` | `taetigkeitsbericht-github-deploy` (Default) |

### 1.2 CloudFormation-Stack in der Konsole

1. AWS-Konsole → Service **CloudFormation**.
2. **Create stack** → **With new resources (standard)**.
3. **Template is ready** → **Upload a template file** → Datei `github-oidc.yml` wählen → **Next**.
4. Stack name z. B. `taetigkeitsbericht-github-oidc`.
5. Parameter `GitHubOrg` / `GitHubRepo` **exakt** wie im GitHub-Repo-Pfad eintragen → **Next**.
6. Optional Tags → **Next**.
7. Ganz unten: Haken bei  
   **I acknowledge that AWS CloudFormation might create IAM resources with custom names.**  
8. **Submit** / **Create stack**.
9. Warten bis Status **`CREATE_COMPLETE`**.
10. Reiter **Outputs** → Wert **`RoleArn`** kopieren  
    (Form: `arn:aws:iam::ACCOUNT_ID:role/taetigkeitsbericht-github-deploy`).

### 1.3 Fehler: OIDC-Provider existiert schon

Meldung ähnlich `EntityAlreadyExists` / Provider bereits vorhanden:

1. **IAM → Identity providers** prüfen, ob `token.actions.githubusercontent.com` existiert.
2. Option A: Bestehenden Provider behalten und nur eine Role manuell anlegen (Trust: dieser Provider, Condition `repo:ORG/REPO:*`).
3. Option B: In der YAML die Resource `GitHubOidcProvider` entfernen und `Federated` auf den bestehenden Provider-ARN setzen, Stack erneut anlegen.

### 1.4 Was die Role darf

In der Sandbox-Vorlage hängt an der Role vorerst **`AdministratorAccess`** (einfaches Testen).  
Für Produktion später auf Least Privilege reduzieren (CloudFormation, EC2, VPC, DSQL, SSM, S3, IAM nur soweit nötig).

---

## Schritt 2 – GitHub Secrets setzen

1. GitHub → Repo öffnen.
2. **Settings** → **Secrets and variables** → **Actions**.
3. **New repository secret** für jeden Eintrag:

| Secret | Pflicht | Inhalt |
|--------|---------|--------|
| `AWS_ROLE_ARN` | ja | Output `RoleArn` aus Schritt 1 |
| `AWS_REGION` | ja | z. B. `eu-central-1` (gleiche Region wie in der Konsole) |
| `JWT_KEY` | ja | Langes Zufallsgeheimnis (≥ 32 Zeichen) für Backend-JWT |
| `EC2_KEY_NAME` | nein | Name eines EC2-Key-Pairs, falls angelegt |
| `BACKEND_HOST_PUBLIC` | später | z. B. `http://3.120.x.x:5108` – **nach** dem ersten Deploy setzen |

### JWT_KEY erzeugen (PowerShell)

```powershell
-join ((48..57 + 65..90 + 97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
```

### Nicht anlegen

- `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` – bei OIDC unnötig und in Ihrer Sandbox oft unmöglich.
- DB-Passwort `verwaltung` – Aurora DSQL arbeitet mit IAM-Tokens.

---

## Schritt 3 – Optional: EC2-Key-Pair

Nur nötig, wenn Sie per SSH auf die Instanzen möchten. Der Deploy läuft über **SSM** (ohne SSH).

1. AWS-Konsole → **EC2** → **Key Pairs** → **Create key pair**.
2. Name z. B. `taetigkeitsbericht`, Typ `.pem` speichern.
3. Denselben Namen als Secret `EC2_KEY_NAME` eintragen.

---

## Schritt 4 – Code auf main und ersten Deploy

### 4.1 Codestand

Sicherstellen, dass auf `main` liegen:

- Workflow `deploy-aws.yml`
- beide CloudFormation-Templates
- Backend- und Frontend-Quellcode

### 4.2 Workflow starten

**Variante A – Push**

```powershell
git push origin main
```

**Variante B – manuell**

1. GitHub → **Actions**.
2. Workflow **Deploy AWS (main)** wählen.
3. **Run workflow** → Branch `main` → Run.

### 4.3 Lauf beobachten

1. Job **CloudFormation VPC + EC2 + DSQL** muss grün werden.
2. Job **Build & deploy Backend + Frontend auf EC2** danach.
3. Am Ende die **Summary** des Runs lesen (IPs, DSQL-Endpoint, EC2-Role-ARN).

Dauer: oft 10–20+ Minuten (EC2, DSQL, SSM-Wartezeit).

### 4.4 Outputs nachschlagen (Konsole)

CloudFormation → Stack **`taetigkeitsbericht`** → **Outputs**:

| Output | Bedeutung |
|--------|-----------|
| `FrontendPublicIp` | Browser-URL: `http://<IP>/` |
| `BackendPublicIp` | API: `http://<IP>:5108/graphql` |
| `DsqlEndpoint` | Hostname für DB-Verbindungen |
| `Ec2RoleArn` | Für `AWS IAM GRANT` der DB-Rolle `verwaltung` |
| `BackendInstanceId` / `FrontendInstanceId` | SSM / EC2-Konsole |

Wenn die AWS CLI in der Sandbox nutzbar ist (selten ohne Keys):

```powershell
aws cloudformation describe-stacks --stack-name taetigkeitsbericht --query "Stacks[0].Outputs"
```

Ohne CLI reicht die Konsole.

---

## Schritt 5 – Was die Pipeline konkret macht

Reihenfolge bei jedem Lauf:

1. **OIDC:** GitHub holt ein Identitäts-Token und übernimmt `AWS_ROLE_ARN`.
2. **CloudFormation deploy** von `taetigkeitsbericht-aws.yml`:
   - VPC `10.20.0.0/16`, öffentliche und private Subnets
   - Internet Gateway, Routing
   - Security Groups (Frontend 80/443, Backend 80/443/5108, DSQL-Endpoint 5432 nur vom Backend)
   - IAM Instance Profile für EC2 (SSM + `dsql:DbConnect*`)
   - Aurora DSQL Cluster
   - Interface VPC Endpoint (PrivateLink) mit Private DNS
   - zwei EC2-Instanzen (Backend, Frontend)
3. **Build:** `dotnet publish` (Backend), `npm run build` (Frontend).
4. **S3:** Artefakte kurz in einen Staging-Bucket legen.
5. **SSM Run Command:** auf beiden EC2s entpacken, systemd/nginx starten.
6. Staging-Bucket wieder löschen.

Beim **ersten** Lauf wird das Netzwerk erzeugt; bei weiteren Läufen meist nur aktualisiert bzw. die App neu ausgerollt.

---

## Schritt 6 – Aurora DSQL einrichten

### 6.1 Wichtige Einschränkung

Aurora DSQL kennt **keine klassischen DB-Passwörter**.  
Rolle `verwaltung` = PostgreSQL-Login-Rolle; „Passwort“ bei der Verbindung = **kurzlebiges IAM-Token**.

Offizielle Docs: [Authentication tokens](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/SECTION_authentication-token.html), [PrivateLink](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/privatelink-managing-clusters.html).

### 6.2 Rolle und IAM-Verknüpfung

Vorlage: `infra/scripts/post-dsql-setup.sql`

Einmalig (als DSQL-`admin` mit Token) ausführen:

```sql
CREATE ROLE verwaltung WITH LOGIN;

-- ACCOUNT_ID und Rollennamen aus den Stack-Outputs ersetzen:
AWS IAM GRANT verwaltung TO 'arn:aws:iam::ACCOUNT_ID:role/taetigkeitsbericht-ec2-role';
```

`taetigkeitsbericht-ec2-role` entspricht dem Output **`Ec2RoleArn`** (nur der Rollenname bzw. volle ARN laut Doku).

### 6.3 Datenbankname Taetigkeitsbericht

- Wenn `CREATE DATABASE "Taetigkeitsbericht";` in DSQL möglich ist: ausführen und Connection darauf zeigen.
- Sonst: Tabellen/Schema in der Standarddatenbank belassen und Connection-String anpassen.

### 6.4 Schema / Migrationen

Sobald das Backend die DB erreichen kann:

- EF Core: `dotnet ef database update` (von einer Umgebung mit Netzpfad zur DB / Token), oder
- Migrationsbefehl per SSM auf der Backend-EC2.

### 6.5 Backend und Tokens

Für Dauerbetrieb muss das Backend Tokens aus der **EC2-Instance-Role** erzeugen und als Connection-Password nutzen. Ein festes Passwort `verwaltung` im Connection-String reicht bei DSQL **nicht**.

---

## Schritt 7 – Frontend-URL festlegen und erneut deployen

Nach dem ersten erfolgreichen Deploy:

1. `BackendPublicIp` aus den Outputs nehmen.
2. GitHub Secret **`BACKEND_HOST_PUBLIC`** setzen auf:  
   `http://<BackendPublicIp>:5108`  
   (ohne `/graphql`, ohne Slash am Ende oder mit – der Workflow normalisiert).
3. Workflow **erneut** starten (Push oder Run workflow).
4. Frontend wird mit `VITE_GRAPHQL_URL=http://…:5108/graphql` gebaut.

Später: eigene Domain + HTTPS (ALB/Certificate Manager) und Secret entsprechend aktualisieren.

Zusätzlich im Backend CORS: Origins müssen die Frontend-URL erlauben (`Cors:Origins` in `appsettings` bzw. Umgebungsvariablen auf der EC2).

---

## Schritt 8 – Desktop on premises anbinden

### 8.1 Prinzip

- Installation und Datenbank der Desktop-App bleiben **lokal**.
- Nur `authentication.toml` zeigt auf die **öffentlichen** AWS-URLs.

### 8.2 Datei anpassen

Pfad: `Desktop/src/authentication.toml`  
(Vorlage: `authentication.example.toml` – echte Datei nicht mit Passwörtern committen.)

```toml
[authentication]
username = "ihr.benutzer"
username_password = "ihr-passwort"
username_email = "ihr@example.com"

[webapi]
base_url = "http://<BackendPublicIp>:5108"
frontend_url = "http://<FrontendPublicIp>"
token_expires_hours = 24
verify_ssl = true
```

Bei späterem HTTPS und gültigem Zertifikat: `verify_ssl = true` belassen.  
Nur zum Testen mit Self-Signed: `verify_ssl = false` (nicht für Produktion).

### 8.3 Funktionstest Desktop

1. Backend in AWS erreichbar? Browser: `http://<BackendPublicIp>:5108/` bzw. GraphQL.
2. Am Backend registrieren / E-Mail bestätigen (wie lokal dokumentiert).
3. Desktop starten → Monat erfassen → speichern (lokal).
4. **Abgeben** → Daten in DSQL.
5. **Online ansehen** → Browser mit Token; Monat sichtbar.

### 8.4 Was lokal bleibt

| Lokal (on premises) | In AWS |
|---------------------|--------|
| Desktop-App | Backend, Frontend |
| `taetigkeitsbericht.db` (SQLite) | Aurora DSQL (abgegebene Daten) |
| Stundenplan, Urlaub, … lokal | Nur hochgeladene Zeiteinträge |

---

## Schritt 9 – Wiederkehrender Betrieb

Bei **jedem Push auf `main`** (oder manuellem Run):

1. Infra-Stack aktualisieren (bestehende VPC bleibt in der Regel erhalten).
2. Neu bauen und per SSM auf EC2 ausrollen.

**Komplette EC2-Neuinstanzen:**  
Actions → Run workflow → Option **`force_instance_refresh`** = true.

**Sandbox abgelaufen:** Alle Ressourcen weg. OIDC-Stack und App-Stack erneut anlegen (Schritte 1 und 4), Secrets falls Account-ID/Role-ARN neu sind aktualisieren.

---

## Checkliste nach dem ersten erfolgreichen Deploy

- [ ] Stack `taetigkeitsbericht-github-oidc` = `CREATE_COMPLETE`, `RoleArn` in GitHub
- [ ] Secrets `AWS_ROLE_ARN`, `AWS_REGION`, `JWT_KEY` gesetzt
- [ ] Workflow **Deploy AWS (main)** grün
- [ ] Stack `taetigkeitsbericht` = `CREATE_COMPLETE` / `UPDATE_COMPLETE`
- [ ] Frontend `http://<FrontendPublicIp>/` lädt
- [ ] Backend `http://<BackendPublicIp>:5108` erreichbar
- [ ] DSQL + PrivateLink vorhanden
- [ ] Rolle `verwaltung` + `AWS IAM GRANT` erledigt
- [ ] Schema/Migrationen angewendet
- [ ] `BACKEND_HOST_PUBLIC` gesetzt, zweiter Deploy für Frontend
- [ ] CORS erlaubt Frontend-Origin
- [ ] Desktop `authentication.toml` auf AWS-URLs
- [ ] Test: Abgeben → Online ansehen zeigt Daten

---

## Häufige Probleme

| Symptom | Maßnahme |
|---------|----------|
| Kein Access-Key-Button, nur Konsolenpasswort | Erwartet → OIDC Schritt 1, kein IAM-Deploy-User nötig |
| Muss ich einen IAM-Benutzer anlegen? | **Nein** für die Pipeline – nur OIDC-Role |
| `AssumeRoleWithWebIdentity` denied | `AWS_ROLE_ARN` falsch; `GitHubOrg`/`GitHubRepo` passen nicht; Workflow braucht `permissions: id-token: write` |
| OIDC-Provider already exists | Bestehenden Provider nutzen, siehe Schritt 1.3 |
| CloudFormation IAM capability | Haken „acknowledge IAM resources“ setzen |
| SSM Instance not Online | 2–5 Min warten; Instance Profile prüfen; Instanz neu starten |
| Frontend ohne Daten | `BACKEND_HOST_PUBLIC`, CORS, JWT/Token, Browser-Konsole/Netzwerk-Tab |
| Backend ohne DB | PrivateLink, SG 5432, IAM `dsql:DbConnect*`, Token statt Passwort |
| Desktop erreicht Backend nicht | Öffentliche IP, SG, Firmen-Firewall, `base_url` Tippfehler, HTTP vs HTTPS |
| Sandbox leer nach Lab-Ende | Stacks und Secrets (Role ARN) neu aufsetzen |

---

## Sandbox-Hinweise

- Ressourcen und Daten sind **zeitlich begrenzt**.
- Notieren Sie sich Outputs (IPs ändern sich bei neuem Stack).
- `AdministratorAccess` an der GitHub-Role nur zum Lernen; in echten Accounts einschränken.
- Desktop-Daten in SQLite sichern Sie weiter lokal – die Cloud ist die Abgabe-/Ansichtsseite.

---

## Kurz: Reihenfolge zum Mitlesen

1. Konsole öffnen, Region wählen  
2. OIDC-Stack deployen → `RoleArn`  
3. GitHub Secrets setzen  
4. Workflow auf `main` starten  
5. IPs/Outputs notieren  
6. DSQL-Rolle `verwaltung` + IAM GRANT  
7. Migrationen  
8. `BACKEND_HOST_PUBLIC` + zweiter Deploy  
9. Desktop `authentication.toml` auf AWS zeigen  
10. Abgeben / Online ansehen testen  
