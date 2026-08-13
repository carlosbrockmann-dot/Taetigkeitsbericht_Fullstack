# Deploy auf AWS – konkrete Schritte (Access Keys)

Diese Anleitung beschreibt **ausführlich**, wie Backend und Frontend in AWS betrieben und bei jedem Commit auf `main` aktualisiert werden – über den **traditionellen Weg mit IAM-Benutzer und Access Keys**.

Die **Desktop-Applikation bleibt on premises** (lokal auf den PCs der Benutzer, SQLite lokal). Sie spricht das Backend nur über das Internet (Login, **Abgeben**, **Online ansehen**).

| | |
|--|--|
| Repository | [Taetigkeitsbericht_Fullstack](https://github.com/carlosbrockmann-dot/Taetigkeitsbericht_Fullstack) |
| Kurzreferenz Infra | [infra/README.md](./infra/README.md) |
| Pipeline | [`.github/workflows/deploy-aws.yml`](./.github/workflows/deploy-aws.yml) |
| App-Stack | [`infra/cloudformation/taetigkeitsbericht-aws.yml`](./infra/cloudformation/taetigkeitsbericht-aws.yml) |

Die Pipeline ist bereits auf **Access Keys** (`AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`) konfiguriert.

---

## Inhaltsverzeichnis

1. [Zielbild](#zielbild)
2. [Was Sie brauchen / nicht brauchen](#was-sie-brauchen--nicht-brauchen)
3. [Voraussetzungen](#voraussetzungen)
4. [Schritt 0 – AWS-Konsole öffnen und Region wählen](#schritt-0--aws-konsole-öffnen-und-region-wählen)
5. [Schritt 1 – IAM-Benutzer und Access Keys](#schritt-1--iam-benutzer-und-access-keys)
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

## Was Sie brauchen / nicht brauchen

| Brauchen | Warum |
|----------|--------|
| **IAM-Benutzer** für Deploy | Pipeline authentifiziert sich mit dessen Access Keys |
| **Access Key ID + Secret Access Key** | Traditionelle programmatische Anmeldung in GitHub Actions |

| Nicht nötig | Warum |
|-------------|--------|
| OIDC-Provider / `github-oidc.yml` | Hier keine Federated Role |
| Desktop in AWS installieren | Desktop bleibt on premises |
| Öffentlicher DB-Port | DSQL nur über PrivateLink |

---

## Voraussetzungen

1. AWS-Konto oder **Sandbox** mit Rechten für: VPC, EC2, IAM, CloudFormation, SSM, S3, Aurora DSQL.
2. In der Konsole **Access Keys anlegen dürfen** (Security credentials → Create access key).
3. GitHub-Zugriff auf das Repo (Secrets schreiben, Actions starten).
4. Region, in der **Aurora DSQL** verfügbar ist (in der Konsole unter Aurora DSQL prüfen).
5. Dateien im Repo vorhanden (nach Pull/Push von `main`):
   - `.github/workflows/deploy-aws.yml` (Access Keys)
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

## Schritt 1 – IAM-Benutzer und Access Keys

Ziel: GitHub Actions meldet sich mit **Access Key ID** und **Secret Access Key** eines IAM-Benutzers an.

### 1.1 IAM-Benutzer anlegen

1. AWS-Konsole → Service **IAM**.
2. **Users** → **Create user**.
3. User name z. B. `taetigkeitsbericht-github-deploy` (oder `verwaltung_admin`).
4. **Next** → Berechtigungen anhängen (siehe unten) → **Create user**.

#### Nötige Rechte (IAM-Policies)

Der Deploy-Benutzer muss die AWS-APIs der Pipeline aufrufen dürfen (CloudFormation, EC2, VPC, IAM, S3, SSM, Aurora DSQL, …).

| Umgebung | Empfehlung |
|----------|------------|
| **Sandbox / Lernen** | Managed Policy **`AdministratorAccess`** direkt am Benutzer (oder über eine Gruppe) |
| **Produktion** | Least Privilege: nur die Services der Pipeline (CloudFormation, EC2, VPC, IAM für Named Roles/Profiles, S3, SSM, DSQL, ggf. STS) |

Ohne ausreichende Rechte schlägt der Workflow fehl, z. B. mit `AccessDenied` bei `cloudformation:DescribeStacks` / `CreateChangeSet`.

**Prüfen:** IAM → User → Reiter **Permissions** – dort muss mindestens eine passende Policy sichtbar sein.

### 1.2 Access Key erzeugen

1. Den neuen Benutzer öffnen → Reiter **Security credentials**.
2. **Create access key**.
3. Use case: **Application running outside AWS** (oder vergleichbar „CLI / CI“) → **Next**.
4. Optional Beschreibung → **Create access key**.
5. **Access key ID** und **Secret access key** sofort notieren bzw. CSV speichern.  
   Der Secret wird **nur einmal** angezeigt.

### 1.3 Sicherheit

- Keys nur in **GitHub Secrets** hinterlegen, nie ins Repo committen.
- Bei Kompromittierung: Key in IAM deaktivieren/löschen und neuen anlegen, Secrets aktualisieren.
- Rotieren Sie Keys regelmäßig.

### 1.4 Workflow (bereits Access Keys)

`.github/workflows/deploy-aws.yml` konfiguriert AWS so:

```yaml
- name: Configure AWS credentials (Access Keys)
  uses: aws-actions/configure-aws-credentials@v6
  with:
    aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
    aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
    aws-region: ${{ secrets.AWS_REGION }}
```

`permissions` enthält nur `contents: read` (kein `id-token: write`).

---

## Schritt 2 – GitHub Secrets setzen

Die Pipeline liest **`secrets.*` ohne `environment:`** in der Workflow-Datei. Deshalb müssen die Werte unter **Repository secrets** liegen – **nicht** unter Environment secrets.

1. GitHub → Repo öffnen.
2. **Settings** → **Secrets and variables** → **Actions**.
3. Reiter **Secrets** → Abschnitt **Repository secrets** (nicht „Environments“ / Environment secrets).
4. **New repository secret** für jeden Eintrag:

| Secret | Pflicht | Inhalt |
|--------|---------|--------|
| `AWS_ACCESS_KEY_ID` | ja | Access key ID (AWS: „Zugriffsschlüssel“) aus Schritt 1.2 |
| `AWS_SECRET_ACCESS_KEY` | ja | Secret access key (AWS: „Geheimer Zugriffsschlüssel“) |
| `AWS_REGION` | ja | z. B. `eu-central-1` (gleiche Region wie in der Konsole; darf nicht leer sein) |
| `JWT_KEY` | ja | Langes Zufallsgeheimnis (≥ 32 Zeichen) für Backend-JWT |
| `EC2_KEY_NAME` | nein | Name eines EC2-Key-Pairs, falls angelegt |
| `BACKEND_HOST_PUBLIC` | später | z. B. `http://3.120.x.x:5108` – **nach** dem ersten Deploy setzen |

### Repository secrets vs. Environment secrets

| Ort in GitHub | Wird von `deploy-aws.yml` gelesen? |
|---------------|-------------------------------------|
| **Repository secrets** | **Ja** |
| Environment secrets (unter einem Environment) | **Nein** – der Workflow setzt kein `environment:` |
| Repository variables | Nur Fallback für `AWS_REGION` (`vars.AWS_REGION`) |

Environment secrets sind unsichtbar für diesen Workflow, auch wenn Name und Werte stimmen. Secrets nach dem Speichern nicht mehr einsehbar – bei Unsicherheit Wert neu setzen.

### JWT_KEY erzeugen (PowerShell)

```powershell
-join ((48..57 + 65..90 + 97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
```

### Nicht anlegen

- `AWS_ROLE_ARN` – die Pipeline nutzt Access Keys, keine OIDC-Rolle.
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

- Workflow `deploy-aws.yml` mit Access-Key-Credentials
- CloudFormation-Template `taetigkeitsbericht-aws.yml`
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

Mit AWS CLI (Access Keys lokal konfiguriert, z. B. `aws configure`):

```powershell
aws cloudformation describe-stacks --stack-name taetigkeitsbericht --query "Stacks[0].Outputs"
```

Ohne CLI reicht die Konsole.

---

## Schritt 5 – Was die Pipeline konkret macht

Reihenfolge bei jedem Lauf:

1. **Access Keys:** GitHub Actions lädt `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` und spricht die AWS-API damit an.
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

## Schritt 6 – Aurora DSQL (automatisch in der Pipeline)

Die Pipeline erledigt nach dem Stack-Deploy auf der Backend-EC2:

1. **`ec2-dsql-bootstrap.sh`**: Admin-IAM-Token → `CREATE ROLE verwaltung` → `AWS IAM GRANT` auf die EC2-Rolle → Schema-Rechte (idempotent, **kein** DROP)  
2. **Backend-Start** mit `Database__UseDsql=true` (Paket `Amazon.AuroraDsql.*`, Token-Refresh automatisch)  
3. **`Database__MigrateOnStartup=true`**: nur **ausstehende** EF-Core-Migrationen (`MigrateAsync`) – **keine** Neuanlage/Löschung der Datenbank  

**Besteht der DSQL-Cluster bereits**, bleibt er erhalten (CloudFormation: `DeletionPolicy`/`UpdateReplacePolicy: Retain`, `DeletionProtectionEnabled: true`). Redeploys überschreiben keine Anwendungsdaten; Schema-Änderungen laufen nur über Migrationen.

Manuelles SQL (nur Fallback): siehe `infra/scripts/post-dsql-setup.sql` / `ec2-dsql-bootstrap.sh`.

### 6.1 Hinweise

- DB-Name: Standard `postgres` (DSQL).  
- Kein festes Passwort `verwaltung` im Connection-String.  
- DSQL-DDL weicht von PostgreSQL ab; der EF-Adapter (`Amazon.AuroraDsql.EntityFrameworkCore`) passt Migrationen an.  
- Docs: [Authentication tokens](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/SECTION_authentication-token.html), [PrivateLink](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/privatelink-managing-clusters.html).

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

**Sandbox abgelaufen:** Alle Ressourcen weg. IAM-Benutzer/Keys prüfen (ggf. neu anlegen), App-Stack erneut deployen (Schritt 4), Secrets aktualisieren.

---

## Checkliste nach dem ersten erfolgreichen Deploy

- [ ] IAM-Benutzer angelegt, Policy (Sandbox: `AdministratorAccess`) angehängt, Access Keys erzeugt
- [ ] **Repository secrets** `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `JWT_KEY` gesetzt (nicht Environment secrets)
- [ ] Workflow **Deploy AWS (main)** grün
- [ ] Stack `taetigkeitsbericht` = `CREATE_COMPLETE` / `UPDATE_COMPLETE`
- [ ] Frontend `http://<FrontendPublicIp>/` lädt
- [ ] Backend `http://<BackendPublicIp>:5108` erreichbar
- [ ] DSQL + PrivateLink vorhanden
- [ ] Pipeline: DSQL-Bootstrap + Backend aktiv (Migrationen im Journal sichtbar)
- [ ] Frontend erreichbar
- [ ] `BACKEND_HOST_PUBLIC` gesetzt, zweiter Deploy für Frontend
- [ ] CORS erlaubt Frontend-Origin
- [ ] Desktop `authentication.toml` auf AWS-URLs
- [ ] Test: Abgeben → Online ansehen zeigt Daten

---

## Häufige Probleme

| Symptom | Maßnahme |
|---------|----------|
| `AccessDenied` bei CloudFormation/EC2/SSM | IAM-Benutzer: Policy prüfen – Sandbox: **`AdministratorAccess`** |
| Secrets „fehlen“ obwohl angelegt | Werte unter **Repository secrets** anlegen; Environment secrets werden ignoriert |
| `AWS_REGION` / leeres Secret | Secret neu setzen (leerer Wert = „nicht gesetzt“); Schreibweise exakt `AWS_REGION` |
| `InvalidClientTokenId` / `SignatureDoesNotMatch` | Falsche oder abgelaufene Keys; Secrets neu setzen; Key in IAM aktiv? |
| Kein Access-Key-Button in der Konsole | Sandbox/Konto blockiert Keys – IAM-Rechte oder Konto-Typ prüfen |
| Workflow nutzt noch `role-to-assume` / `AWS_ROLE_ARN` | Aktuellen Stand von `deploy-aws.yml` ziehen (Access Keys) |
| CloudFormation deploy failed, Events in GitHub leer | In AWS-Konsole → CloudFormation → Stack `taetigkeitsbericht` → **Events**: Zeile mit `…_FAILED` und **Status reason** lesen. Häufig: Stack in `ROLLBACK_COMPLETE`/`UPDATE_ROLLBACK_FAILED` (Stack löschen und neu), IAM-Rolle `taetigkeitsbericht-ec2-role` existiert noch, DSQL/VPC-Endpoint-Fehler, fehlende Rechte |
| CloudFormation IAM capability | Haken „acknowledge IAM resources“ setzen (falls manuell) |
| SSM Instance not Online | 2–5 Min warten; Instance Profile prüfen; Instanz neu starten |
| Frontend ohne Daten | `BACKEND_HOST_PUBLIC`, CORS, JWT/Token, Browser-Konsole/Netzwerk-Tab |
| Backend ohne DB | PrivateLink, SG 5432, IAM `dsql:DbConnect*`, Token statt Passwort |
| Desktop erreicht Backend nicht | Öffentliche IP, SG, Firmen-Firewall, `base_url` Tippfehler, HTTP vs HTTPS |
| Sandbox leer nach Lab-Ende | Stacks und ggf. IAM-Keys/Secrets neu aufsetzen |

---

## Sandbox-Hinweise

- Ressourcen und Daten sind **zeitlich begrenzt**.
- Notieren Sie sich Outputs (IPs ändern sich bei neuem Stack).
- `AdministratorAccess` am Deploy-Benutzer nur zum Lernen; in echten Accounts einschränken.
- Access Keys sind langlebig – sorgfältig verwahren und bei Lab-Ende in IAM löschen.
- Desktop-Daten in SQLite sichern Sie weiter lokal – die Cloud ist die Abgabe-/Ansichtsseite.

---

## Kurz: Reihenfolge zum Mitlesen

1. Konsole öffnen, Region wählen  
2. IAM-Benutzer + Access Keys anlegen  
3. GitHub Secrets setzen  
4. Workflow auf `main` starten  
5. IPs/Outputs notieren  
6. Pipeline: DSQL-Bootstrap + Migrationen (automatisch)  
7. `BACKEND_HOST_PUBLIC` + zweiter Deploy  
9. Desktop `authentication.toml` auf AWS zeigen  
10. Abgeben / Online ansehen testen  
