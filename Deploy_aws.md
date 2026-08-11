# Deploy auf AWS – konkrete Schritte

Diese Anleitung beschreibt, wie Backend und Frontend in AWS betrieben werden und bei jedem Commit auf `main` aktualisiert werden. Die **Desktop-Applikation bleibt on premises** (lokal auf den PCs der Benutzer, SQLite lokal); sie spricht das Backend nur über das Internet (Login + Abgeben / Online ansehen).

Repository: [Taetigkeitsbericht_Fullstack](https://github.com/carlosbrockmann-dot/Taetigkeitsbericht_Fullstack)

Technische Details zusätzlich: [infra/README.md](./infra/README.md)

---

## Zielbild

| Komponente | Wo | Aufgabe |
|------------|-----|---------|
| **Desktop** | On premises | Zeiterfassung lokal; Upload und „Online ansehen“ gegen AWS-Backend |
| **Backend** | AWS EC2 in VPC | GraphQL-API, Auth, Speichern/Lesen |
| **Frontend** | AWS EC2 in VPC | React-Monatsansicht (Browser) |
| **Datenbank** | AWS Aurora DSQL | Persistenz; Zugriff nur privat (PrivateLink in der VPC) |

```
[ Desktop on premises ] ──HTTPS──► [ EC2 Backend ] ──privat──► [ Aurora DSQL ]
                                        ▲
[ Browser ] ──HTTPS──► [ EC2 Frontend ]─┘ (API-URL des Backends)
```

---

## Voraussetzungen

1. AWS-Konto mit Berechtigung für VPC, EC2, IAM, CloudFormation, SSM, S3, Aurora DSQL.
2. Region wählen, in der **Aurora DSQL** verfügbar ist (z. B. prüfen in der AWS-Konsole); Wert später als `AWS_REGION`.
3. GitHub-Repo mit Branch `main` und den Dateien:
   - `.github/workflows/deploy-aws.yml`
   - `infra/cloudformation/taetigkeitsbericht-aws.yml`
4. AWS CLI lokal optional (zum Nachprüfen), aber Deploy läuft über GitHub Actions.

---

## Schritt 1 – IAM-Benutzer für die Pipeline anlegen

1. In der AWS-Konsole: **IAM → Users → Create user** (z. B. `github-deploy`).
2. Programmatischen Zugriff (Access Key) erzeugen.
3. Richtlinien zuweisen (Start: AdministratorAccess nur für Tests; produktiv: Least Privilege für CloudFormation, EC2, VPC, IAM (Named Roles), DSQL, SSM, S3).
4. Access Key ID und Secret Access Key notieren (nur einmal sichtbar).

---

## Schritt 2 – GitHub Secrets setzen

Im Repo: **Settings → Secrets and variables → Actions → New repository secret**

| Secret | Inhalt |
|--------|--------|
| `AWS_ACCESS_KEY_ID` | Access Key des Deploy-Users |
| `AWS_SECRET_ACCESS_KEY` | Secret Key |
| `AWS_REGION` | z. B. `eu-central-1` (DSQL-fähige Region) |
| `JWT_KEY` | langes zufälliges Secret für Backend-JWT (≥ 32 Zeichen) |
| `EC2_KEY_NAME` | optional: Name eines EC2-Key-Pairs (SSH); Deploy nutzt primär SSM |
| `BACKEND_HOST_PUBLIC` | zunächst leer lassen; nach dem ersten Deploy setzen (siehe Schritt 6) |

**Nicht** als Secret ablegen und nicht committen: festes DB-Passwort `verwaltung`. Aurora DSQL nutzt IAM-Tokens (siehe Schritt 5).

---

## Schritt 3 – Optional: EC2-Key-Pair

1. AWS-Konsole: **EC2 → Key Pairs → Create**.
2. Name z. B. `taetigkeitsbericht`.
3. Denselben Namen als Secret `EC2_KEY_NAME` eintragen.

Für den normalen Deploy über GitHub Actions ist SSH nicht nötig (SSM).

---

## Schritt 4 – Ersten Deploy auslösen

1. Sicherstellen, dass Workflow und CloudFormation im Repo auf `main` liegen.
2. Entweder:
   - **Push** auf `main`, oder
   - **Actions → Deploy AWS (main) → Run workflow**.
3. Workflow-Lauf beobachten. Erfolgreich bedeutet u. a.:
   - Stack `taetigkeitsbericht` existiert/aktualisiert
   - VPC, Security Groups, zwei EC2, DSQL-Cluster, PrivateLink-Endpoint
   - Backend- und Frontend-Artefakte auf den EC2s

4. In der Workflow-**Summary** notieren:
   - Frontend-URL / Public IP
   - Backend-URL / Public IP (`…:5108/graphql`)
   - DSQL-Endpoint
   - EC2-Role-ARN (für `AWS IAM GRANT`)

Alternativ Stack-Outputs:

```powershell
aws cloudformation describe-stacks --stack-name taetigkeitsbericht --query "Stacks[0].Outputs"
```

---

## Schritt 5 – Aurora DSQL: Rolle `verwaltung` und privater Zugriff

Die Pipeline legt den Cluster und den VPC-Endpoint an. DB-Rollen müssen einmalig (oder per Script) eingerichtet werden.

1. Als `admin` mit IAM-Token verbinden (von einem Rechner mit AWS-Credentials und DSQL-Zugriff), siehe [Authentication tokens](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/SECTION_authentication-token.html).
2. SQL aus `infra/scripts/post-dsql-setup.sql` anpassen und ausführen:
   - `CREATE ROLE verwaltung WITH LOGIN;`
   - `AWS IAM GRANT verwaltung TO 'arn:aws:iam::ACCOUNT_ID:role/taetigkeitsbericht-ec2-role';`  
     (ARN = Output `Ec2RoleArn` aus dem Stack)
3. Datenbank-/Schema-Namen laut DSQL-Möglichkeiten setzen (Zielname **Taetigkeitsbericht**; falls `CREATE DATABASE` nicht möglich: Tabellen in der Standard-DB / Schema).
4. EF-Migrationen auf dem Backend ausführen (lokal gegen DSQL mit Token, oder per SSM auf der Backend-EC2), sobald die Verbindung steht.

**Hinweis:** Das Backend muss für Produktionsbetrieb IAM-Tokens als Connection-Password erzeugen (Instance-Role). Ein festes Passwort `verwaltung` funktioniert mit Aurora DSQL nicht.

Private DB-Verbindung: Backend in der VPC → Interface VPC Endpoint → DSQL ([PrivateLink](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/privatelink-managing-clusters.html)).

---

## Schritt 6 – Frontend auf die öffentliche Backend-URL festnageln

1. Öffentliche Backend-Basis-URL festlegen, z. B.  
   `http://<BackendPublicIp>:5108`  
   (später besser Domain + HTTPS/ALB).
2. GitHub Secret `BACKEND_HOST_PUBLIC` auf diese Basis-URL setzen (ohne `/graphql`).
3. Workflow erneut starten (Push oder „Run workflow“), damit der Frontend-Build `VITE_GRAPHQL_URL` korrekt setzt.

---

## Schritt 7 – Desktop on premises anbinden

Die Desktop-App bleibt lokal. Nur die Backend-URL zeigt auf AWS.

1. Auf jedem Desktop-PC: `Desktop/src/authentication.toml` (nicht committen mit echten Secrets):

```toml
[authentication]
username = "..."
username_password = "..."
username_email = "..."

[webapi]
base_url = "http://<BackendPublicIp>:5108"
# später: https://api.ihre-domain.de
frontend_url = "http://<FrontendPublicIp>"
# später: https://app.ihre-domain.de
verify_ssl = true
```

2. Backend in AWS muss erreichbar sein (Security Group Ports 80/443/5108).
3. Benutzer am Backend registrieren / E-Mail bestätigen (wie lokal).
4. Desktop: **Abgeben** (Upload) und **Online ansehen** (öffnet Frontend mit Token).

Lokale SQLite-Daten bleiben on premises; in der Cloud liegen nur die **abgegebenen** Monate.

---

## Schritt 8 – Wiederkehrender Betrieb (jeder Commit auf main)

Automatisch durch `.github/workflows/deploy-aws.yml`:

1. CloudFormation-Stack aktualisieren (Netzwerk bleibt; fehlende Ressourcen werden angelegt).
2. Backend und Frontend neu bauen.
3. Artefakte per SSM auf die EC2s ausrollen (Software erneuern).

**Komplette EC2-Neuinstanzierung:** Actions → Run workflow → `force_instance_refresh = true`.

---

## Checkliste nach dem ersten erfolgreichen Deploy

- [ ] Stack `taetigkeitsbericht` = `CREATE_COMPLETE` / `UPDATE_COMPLETE`
- [ ] Frontend im Browser erreichbar
- [ ] Backend `/graphql` erreichbar (GraphiQL nur in Development)
- [ ] DSQL PrivateLink aktiv; Backend verbindet sich (IAM)
- [ ] Rolle `verwaltung` + `AWS IAM GRANT` erledigt
- [ ] Schema/Migrationen angewendet
- [ ] `BACKEND_HOST_PUBLIC` gesetzt und Frontend neu gebaut
- [ ] Desktop `authentication.toml` zeigt auf AWS-URLs
- [ ] Test: Desktop Abgeben → Einträge in Online-Ansicht sichtbar

---

## Häufige Probleme

| Symptom | Maßnahme |
|---------|----------|
| Workflow scheitert an AWS-Auth | Secrets `AWS_*` und Region prüfen |
| SSM „Instance not Online“ | SSM-Agent / Instance Profile; Instanz neu starten; 2–5 Min warten |
| Frontend lädt keine Daten | CORS im Backend; `VITE_GRAPHQL_URL` / `BACKEND_HOST_PUBLIC`; Token abgelaufen |
| Backend verbindet nicht zur DB | PrivateLink, SG Port 5432, IAM `dsql:DbConnect*`, Token statt Passwort |
| Desktop erreicht Backend nicht | Öffentliche IP/SG, Firewall on premises, `base_url` ohne Tippfehler |

---

## Was bewusst nicht in AWS läuft

- Die **Desktop-App** (Python/PySide6) und die **lokale SQLite**-Datenbank bleiben on premises.
- Es gibt keinen Zwang, den Desktop in die VPC zu legen; er ist ein Client wie ein Browser.
