# Tätigkeitsbericht – AWS-Infrastruktur & CI/CD

Technische Kurzreferenz; **konkrete Schritte**: [../Deploy_aws.md](../Deploy_aws.md).

Zielbild: GitHub Actions stellt bei **jedem** Lauf sicher, dass VPC, EC2, DSQL und PrivateLink existieren, und rollt die Apps per CodeDeploy aus. Vorhandene Aurora DSQL wird **nicht** gelöscht.

## Architektur

```
Internet
   │  HTTP :80 / :5108
   ▼
┌──────────────────────── VPC 10.20.0.0/16 ────────────────────────┐
│  Public Subnets (+ EIP)              Private Subnets             │
│  ┌─────────────┐  ┌─────────────┐    ┌─────────────────┐         │
│  │ EC2 Frontend│  │ EC2 Backend │───►│ VPC Endpoint    │         │
│  │ Nginx + SPA │  │ .NET API    │    │ (PrivateLink)   │         │
│  └─────────────┘  └─────────────┘    └────────┬────────┘         │
│                                               ▼                  │
│                                        Aurora DSQL               │
└──────────────────────────────────────────────────────────────────┘
         ▲
         │ CodeDeploy (Zips aus S3 Artifact-Bucket)
    GitHub Actions
```

| Komponente | Rolle |
|------------|--------|
| VPC + Subnets | Öffentlich (App + EIP), privat (DSQL-Endpoint) |
| EC2 Backend / Frontend | Bleiben stehen; Software per CodeDeploy |
| Artifact-Bucket | Dauerhaft, keine Staging-Buckets pro Run |
| Aurora DSQL | IAM-Tokens, Cluster `Retain` |
| GitHub Actions | `deploy-aws.yml`: Job CloudFormation + Job CodeDeploy |

Details und Begründung: [Deploy-Strategie.md](./Deploy-Strategie.md).

Offizielle Hinweise zu PrivateLink: [Managing Aurora DSQL with PrivateLink](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/privatelink-managing-clusters.html).

## Wichtig: Aurora DSQL und Passwörter

**Aurora DSQL verwendet keine klassischen DB-Passwörter.** Authentifizierung läuft über **kurzlebige IAM-Tokens**.

| Wunsch | Realität bei Aurora DSQL |
|--------|---------------------------|
| Benutzer `verwaltung` | PostgreSQL-Rolle `verwaltung` (`CREATE ROLE … WITH LOGIN`) |
| Passwort `verwaltung` | **Nicht** – Verbindung mit IAM-Token |
| Schema | EF-Core-Migrationen (`Database__MigrateOnStartup=true`) |

### Was die Pipeline automatisch macht

1. CloudFormation (jeder Lauf): fehlende Ressourcen neu; vorhandener Cluster bleibt (`Retain`, ggf. Import)  
2. CodeDeploy Backend: `ec2-dsql-bootstrap.sh` (Rolle + `AWS IAM GRANT`; **kein** DROP)  
3. Backend-Start: Migrationen als **admin**, danach DML-GRANT an `verwaltung`; App verbindet als `verwaltung`  
4. DSQL: `DeletionProtectionEnabled` + `Retain` – Pipeline löscht keinen existierenden Cluster

Lokal bleibt `Database:UseDsql=false` und `ConnectionStrings:DefaultConnection` (PostgreSQL).

Wenn Sie zwingend Benutzername+Passwort im Connection-String brauchen, ist **Amazon Aurora PostgreSQL** (nicht DSQL) die passendere Variante.

## Dateien

| Pfad | Inhalt |
|------|--------|
| `.github/workflows/deploy-aws.yml` | Jeder Lauf: Infra-Heal + App-CodeDeploy |
| `infra/cloudformation/taetigkeitsbericht-aws.yml` | VPC, SG, EC2, DSQL, Bucket, CodeDeploy |
| `infra/scripts/ensure-cloudformation-stack.sh` | Fehlende Infra nachziehen; DSQL nie löschen |
| `infra/codedeploy/` | `appspec.yml` + Startskripte |
| `infra/scripts/ec2-dsql-bootstrap.sh` | Rolle `verwaltung` + IAM GRANT (idempotent) |
| `infra/scripts/post-dsql-setup.sql` | Kurz-Dokumentation der SQL-Schritte |

## GitHub Secrets (Repository secrets)

Pfad: Repository → **Settings** → **Secrets and variables** → **Actions** → **Repository secrets**  
(**nicht** Environment secrets – der Workflow hat kein `environment:`).

| Secret | Beispiel / Bedeutung |
|--------|----------------------|
| `AWS_ACCESS_KEY_ID` | Access Key ID des IAM-Deploy-Benutzers |
| `AWS_SECRET_ACCESS_KEY` | Secret Access Key (nur in GitHub Secrets) |
| `AWS_REGION` | z. B. `eu-central-1` (DSQL-Region prüfen; nicht leer lassen) |
| `JWT_KEY` | langes Secret für Backend-JWT |
| `EC2_KEY_NAME` | optional, EC2-Key-Pair (SSH); Deploy über CodeDeploy |
| `BACKEND_HOST_PUBLIC` | optional: eigene Backend-URL für Frontend-Build; sonst Backend-EIP |

**Kein** `AWS_ROLE_ARN` / OIDC – die Pipeline nutzt Access Keys. Details: [Deploy_aws.md](../Deploy_aws.md).

**Keine DB-Passwörter** in Secrets speichern und in YAML hardcoden, solange DSQL IAM nutzt.

## IAM-Benutzer für die Pipeline

Der Benutzer, zu dem die Access Keys gehören, braucht genug Rechte für den Stack-Deploy:

| Sandbox / Lernen | Produktion |
|------------------|------------|
| **`AdministratorAccess`** | Least Privilege: CloudFormation, EC2, VPC, IAM (Named Roles/Profiles), S3, SSM, Aurora DSQL |

Sonst scheitert der Workflow mit `AccessDenied` (z. B. `cloudformation:DescribeStacks`).

## Einmalig / manuell

1. AWS-Konto, Region mit Aurora DSQL.
2. IAM-Deploy-Benutzer mit Rechten (Sandbox: `AdministratorAccess`) + Access Keys.
3. **Repository secrets** in GitHub setzen.
4. Optional EC2 Key Pair anlegen.
5. Push auf `main` → Infra vollständig (fehlendes wird angelegt) und App-CodeDeploy.
6. Frontend-URL kommt aus der Backend-EIP; `BACKEND_HOST_PUBLIC` nur bei eigener Domain.

## Lokal Stack testen

```powershell
aws cloudformation deploy `
  --stack-name taetigkeitsbericht `
  --template-file infra/cloudformation/taetigkeitsbericht-aws.yml `
  --capabilities CAPABILITY_NAMED_IAM `
  --parameter-overrides ProjectName=taetigkeitsbericht
```

## EC2 „erneuern“

App-Push ersetzt **keine** Instanzen – nur CodeDeploy. Instance-Replace: Workflow **Run workflow** mit `force_instance_refresh=true` (UserData-Marker). Danach rollt der App-Job automatisch wieder aus, wenn Infra mitgelaufen ist.
