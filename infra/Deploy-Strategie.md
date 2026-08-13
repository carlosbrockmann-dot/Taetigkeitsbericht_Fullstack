# Deploy-Strategie: EC2 + Aurora DSQL (CodeDeploy)

Stand: 2026-08-13 – **umgesetzt** (nicht mehr nur Plan).

Verwandte Dateien:

- Pipeline: [`.github/workflows/deploy-aws.yml`](../.github/workflows/deploy-aws.yml)
- Stack: [`infra/cloudformation/taetigkeitsbericht-aws.yml`](./cloudformation/taetigkeitsbericht-aws.yml)
- Appspec: [`infra/codedeploy/`](./codedeploy/)
- Betriebsschritte: [`../Deploy_aws.md`](../Deploy_aws.md)

---

## Kurzfassung (ist-Zustand)

Infra und App sind getrennt. Apps laufen auf EC2 über **festen S3-Bucket + CodeDeploy**. SSM Run Command ist kein Release-Kanal mehr.

| Push | Was passiert |
|------|----------------|
| Nur `Backend/` / `Frontend/` / `infra/codedeploy/` | CodeDeploy, **keine** neuen EC2 |
| `infra/cloudformation` oder `infra/scripts` | CloudFormation, danach App-Deploy (falls Instanzen ersetzt wurden) |
| Erster Lauf nach dem Template-Wechsel | EC2 einmal neu (UserData mit CodeDeploy-Agent), dann Software per CodeDeploy |

DSQL: Cluster `Retain` + Deletion Protection. Bootstrap idempotent in CodeDeploy AfterInstall. Nur `MigrateAsync`.

Die folgenden Abschnitte 1–2 beschreiben die **alte** SSM-Pipeline (Befund). Abschnitt 3+ ist die umgesetzte Alternative.

---

## 1. Was die bestehenden YAML-Dateien tun

### CloudFormation (`taetigkeitsbericht-aws.yml`)

| Teil | Bewertung |
|------|-----------|
| VPC, öffentliche Subnets, IGW, SGs | in Ordnung |
| Aurora DSQL + Interface-VPC-Endpoint | richtig (kein öffentlicher DB-Port) |
| `DeletionPolicy` / `UpdateReplacePolicy: Retain`, `DeletionProtectionEnabled: true` | richtig – Cluster/Daten bleiben |
| IAM: `AmazonSSMManagedInstanceCore` + `dsql:DbConnect*` | nötig für SSM und DSQL-Tokens |
| Elastic IPs statt `GetAtt Instance.PublicIp` | nötig (sonst Rollback: `Attribute 'PublicIp' does not exist`) |
| Backend in **öffentlichem** Subnet | weicht vom Diagramm in `infra/README.md` ab (dort privat gezeichnet), ist aber für SSM-über-Internet ohne NAT/VPC-Endpoints nachvollziehbar |
| Private Subnets | nur für den DSQL-Endpoint; **kein NAT**, **keine SSM-Endpoints** |

### GitHub Actions (`deploy-aws.yml`)

Ablauf bei jedem Push auf `main`:

1. CloudFormation-Stack anlegen/aktualisieren  
2. Backend/Frontend bauen  
3. Artefakte in einen **temporären S3-Bucket** (pro Run-ID)  
4. Warten, bis SSM `Online`  
5. Lange Shell-Liste per `AWS-RunShellScript` auf der Backend-EC2 (Bootstrap, `dotnet`-Install, systemd, Migrationen)  
6. Ähnlich für Frontend (nginx)  
7. Staging-Bucket löschen  

Das Ziel (EC2 + DSQL, Pipeline) ist erfüllt – die **Mechanik** ist brüchig.

---

## 2. Befund: warum die aktuelle Strategie anfällig ist

### 2.1 SSM Run Command als Deploy-Kanal

`SendCommand` funktioniert nur, wenn die Instanz ein **Managed Node** ist (Agent läuft, IAM-Profil, Netzpfad zu SSM). Bisheriger Fehler:

`InvalidInstanceId` / `Instances not in a valid state for account`

Das bedeutet nicht „falsche ID“, sondern: SSM kennt die Instanz nicht. Ursachen typischerweise:

- Agent nicht gestartet (UserData nur beim **ersten** Launch; bestehene VMs ohne `force_instance_refresh` bleiben alt)
- keine öffentliche IP / kein SSM-VPC-Endpoint (vor den EIPs)
- IAM-Profil erst nach Launch oder Propagation verzögert
- Instanz noch `pending` / gerade ersetzt

Der Wait-Step macht das sichtbar, behebt es aber nicht. Jeder Replace (`ForceInstanceRefresh`) erzeugt dasselbe Zeitfenster erneut.

### 2.2 Infra und App in einem Workflow

Jeder App-Commit führt `cloudformation deploy` aus. Ein harmloses Tag-Update an DSQL hat schon den ganzen Stack in `UPDATE_ROLLBACK_COMPLETE` geschoben (`PublicIp`). App-Deploys sollten **nicht** von Infra-Drift abhängen.

### 2.3 Einmalige EC2 ohne Auto Scaling / ohne AMI

Zwei nackte `AWS::EC2::Instance`:

- UserData läuft nicht bei Software-Updates nach  
- `.NET` wird per `curl | bash` auf der Maschine installiert (langsam, nicht reproduzierbar, bricht SSM-Timeouts)  
- systemd-Unit mit `JWT_KEY` wird per Python-String in SSM zusammengebaut (Quoting, Secrets auf Disk)  
- Frontend-SSM-Schritt bricht den Job bei Fehler **nicht** hart ab (`Status != Success` fehlt)

### 2.4 Temporärer S3-Bucket

`taetigkeitsbericht-deploy-<run_id>-<attempt>`:

- Extra Create/Delete pro Lauf  
- IAM auf der EC2-Rolle nutzt `arn:aws:s3:::${ProjectName}-deploy-*` – Wildcard in der **Mitte** des Bucket-Namens ist in IAM oft **nicht** so gemeint wie gedacht  
- Bucket wird `if: always()` gelöscht, auch wenn das Debuggen der Artefakte nötig wäre

### 2.5 Frontend-URL / CORS / HTTPS

- `VITE_GRAPHQL_URL` ist Build-Zeit; ohne Secret `BACKEND_HOST_PUBLIC` zweiter Deploy nötig  
- Backend `Cors:Origins` kennt lokal nur `localhost:5173`; Produktions-Origin der Frontend-EIP fehlt in der Pipeline  
- `UseHttpsRedirection()` auf HTTP-only EC2 (`:5108`) kann Browser/Desktop stören  
- kein TLS (ALB/ACM)

### 2.6 DSQL-Bootstrap bei jedem App-Deploy

`ec2-dsql-bootstrap.sh` ist idempotent und löscht nichts – gut. Es braucht aber `psql`, Admin-Token und Netz zum Cluster **auf derselben SSM-Session** wie das App-Update. Ein Paket-Install-Fehler (`postgresql15`) stoppt dann fälschlich das App-Release.

---

## 3. Empfohlene Strategie (EC2 bleibt, weniger bewegliche Teile)

Weiterhin: **zwei EC2** (oder eine EC2 mit zwei Services), **Aurora DSQL + PrivateLink**, GitHub Actions. Geändert wird nur, **wie** Software und Infra leben.

```
GitHub Actions
    │
    ├─ Job "infra" (nur bei Änderung unter infra/ oder workflow_dispatch)
    │     CloudFormation: VPC, DSQL, EC2/ASG, IAM, **dauerhafter** S3-Bucket,
    │     CodeDeploy-App, SSM-VPC-Endpoints (optional aber empfohlen)
    │
    └─ Job "app" (jeder Push auf main)
          dotnet publish + npm build
          → s3://taetigkeitsbericht-artifacts/backend.zip | frontend.zip
          → CodeDeploy Deployment (Agent auf EC2 holt Zip, Appspec startet Dienst)
```

DSQL: Cluster einmal anlegen, **nie** ersetzen. Pipeline nur `MigrateAsync` (bereits im Backend vorgesehen).

### 3.1 Infra und App trennen

| Job | Wann | Was |
|-----|------|-----|
| `deploy-infra` | Pfadfilter `infra/**`, `workflow_dispatch` | nur CloudFormation |
| `deploy-apps` | Push `main` (Backend/Frontend) | nur Build + S3 + CodeDeploy |

Kein `cloudformation deploy` mehr bei reinen UI-/API-Commits.

### 3.2 Fester Artifact-Bucket im Stack

CloudFormation-Ressource `AWS::S3::Bucket` (z. B. `taetigkeitsbericht-artifacts-<AccountId>`), Versioning an.

EC2-Rolle: `s3:GetObject` **genau auf diesen Bucket**, nicht auf `deploy-*`.

Pipeline: `aws s3 cp` dorthin, Bucket **nicht** nach dem Run löschen.

### 3.3 CodeDeploy statt SSM-Skriptliste

Auf beiden Instanzen (AL2023 hat den Agent oft schon, sonst UserData einmalig):

- `codedeploy-agent`
- `amazon-ssm-agent` nur noch für **Betrieb/Debug**, nicht für Releases

Repo:

- `infra/codedeploy/backend/appspec.yml` + `scripts/start.sh` / `stop.sh`
- `infra/codedeploy/frontend/appspec.yml` (nginx-Root tauschen)

GitHub: `aws deploy create-deployment --s3-location …` und auf Erfolg warten.

Vorteil: Wiederholbar, Logs in der CodeDeploy-Konsole, kein 22-Zeilen-`send-command`, kein `dotnet-install.sh` bei jedem Deploy.

**.NET Runtime:** einmal in UserData/AMI (`dotnet-runtime-10.0`), nicht in der Pipeline auf der Box nachinstallieren. Publish als **framework-dependent** oder besser **self-contained** (`-r linux-x64 --self-contained true`), dann braucht die EC2 kein SDK.

### 3.4 SSM-VPC-Endpoints (falls Instanzen privat werden)

Wenn das Backend wirklich ins private Subnet soll (wie im README-Diagramm):

- NAT **oder** Interface-Endpoints: `ssm`, `ssmmessages`, `ec2messages`, plus `s3` Gateway-Endpoint  
- sonst kein Patch, kein CodeDeploy-Agent-Callback, kein `yum`

Mit öffentlicher EC2 + EIP reicht der Internetweg – Endpoints sind dann optional, machen SSM aber unabhängig von „hat die Instanz eine Public IP?“.

### 3.5 Auto Scaling Group (1 Instanz) statt nackter `AWS::EC2::Instance`

Launch Template + ASG `MinSize=1, MaxSize=1`:

- Replace bei AMI-/UserData-Änderung ohne CloudFormation-`PublicIp`-GetAtt  
- EIP per `AWS::EC2::EIPAssociation` an die laufende Instance (oder später ALB, dann keine EIP nötig)

Nicht zwingend für den ersten Schritt; CodeDeploy auf den bestehenden zwei Instanzen reicht als Zwischenstufe.

### 3.6 DSQL-Bootstrap einmal, nicht bei jedem Release

- eigenes `workflow_dispatch`-Job „DSQL bootstrap“ **oder**  
- CloudFormation Custom Resource / einmaliges SSM nach `CREATE_COMPLETE`

App-Job setzt nur `Database__MigrateOnStartup=true` (bereits so gedacht). Kein `CREATE ROLE` mehr im Release-Pfad.

### 3.7 Frontend-URL und CORS in denselben App-Job

Nach bekanntem Backend-EIP (Stack-Output):

1. `VITE_GRAPHQL_URL=http://<BackendEip>:5108/graphql` beim `npm run build` (Secret nur noch für Domain/HTTPS)  
2. systemd/Env `Cors__Origins__0=http://<FrontendEip>` (oder festes Secret)

Kein zweiter manueller Workflow nur wegen der URL, sobald die EIPs stabil sind.

### 3.8 HTTPS später, nicht im ersten Wurf

Wenn die Pipeline steht: ein ALB + ACM-Zertifikat vor beide (oder nur Frontend), Backend nur in der VPC. Das reduziert CORS-/Mixed-Content-Probleme. Bis dahin HTTP bewusst lassen und `UseHttpsRedirection` in Production an Env koppeln (`ASPNETCORE_FORWARDHEADERS` / Flag) – das ist eine kleine Backend-Änderung bei der Umsetzung.

---

## 4. Was an den YAML-Dateien bewusst **nicht** weitergeflickt wird

Kleine SSM-Waits, UserData-Agent-Starts und EIP-Outputs sind sinnvoll, ändern aber nicht das Grundproblem (Deploy = ferngesteuerte Shell auf einer Pet-Instanz).

Weitere YAML-Patches an `send-command` würden die Datei noch unleserlicher machen (siehe bereits Python-generierte systemd-Unit in der Workflow-Datei).

---

## 5. Umsetzungsstand

Erledigt im Repo:

1. Artifact-Bucket + IAM im Stack  
2. Self-contained Backend-Publish  
3. CodeDeploy-Apps + Deployment Groups (Tags `Role=backend` / `Role=frontend`)  
4. App-Job: `create-deployment` statt `send-command`  
5. Infra-Job pfadgefiltert  
6. DSQL-Bootstrap in AfterInstall (idempotent), nicht in der GitHub-SSM-Liste  
7. `VITE_GRAPHQL_URL` und CORS aus EIPs  

Optional offen: ASG, ALB/HTTPS, Backend in privatem Subnet + SSM/S3-Endpoints.

---

## 6. DSQL-Daten: unverändert lassen

Unabhängig von CodeDeploy/SSM:

- Cluster nicht löschen, nicht ersetzen (`Retain` + Deletion Protection beibehalten)  
- kein `DROP DATABASE`, kein Schema-Reset in Skripten  
- nur EF `MigrateAsync` auf bestehender DB  

Das ist in den aktuellen Skripten bereits so gedacht und soll so bleiben.

---

## 7. Checkliste vor der Umstellung

- [x] Diese Datei umsetzen (CodeDeploy + Infra/App-Trennung)  
- [ ] Stack `taetigkeitsbericht` in `UPDATE_COMPLETE` / `CREATE_COMPLETE`  
- [ ] DSQL-Cluster-ID unverändert (`Retain`)  
- [ ] Ersten Workflow-Lauf nach dem Merge beobachten (einmaliger EC2-Replace wegen UserData)  
- [ ] CodeDeploy-Deployments in der AWS-Konsole grün  

---

## 8. Alternative, falls EC2-Pflege zu teuer wird

Nur zur Einordnung, **nicht** Zielbild dieser Anforderung (EC2 ist vorgegeben):

| Variante | Vorteil | Nachteil |
|----------|---------|----------|
| ECS Fargate + ALB | kein Agent, Image = Artifact | keine „EC2-VMs“ mehr |
| Frontend auf S3 + CloudFront | kein nginx-Host | Abweichung von „Frontend auf EC2“ |
| Elastic Beanstalk | weniger YAML | weniger Kontrolle, eigene Eigenheiten |

Für dieses Repo: **EC2 behalten, CodeDeploy + fester Bucket + Infra/App-Trennung.**
