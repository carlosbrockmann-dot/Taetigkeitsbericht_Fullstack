#!/usr/bin/env bash
# Idempotenter DSQL-Bootstrap + systemd-Unit + Binary-Rechte.
# Kein DROP / Truncate – bestehende Daten bleiben erhalten.
set -euo pipefail
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

log() { echo "[after_install] $*"; }

mkdir -p /opt/taetigkeitsbericht/backend /opt/taetigkeitsbericht/scripts /etc/taetigkeitsbericht

# Archive-Wurzel = CWD des CodeDeploy-Hooks
if [ -f config/backend.env ]; then
  install -m 640 config/backend.env /etc/taetigkeitsbericht/backend.env
  log "backend.env aus Bundle übernommen"
fi
if [ -f scripts/taetigkeitsbericht-backend.service ]; then
  install -m 644 scripts/taetigkeitsbericht-backend.service /etc/systemd/system/taetigkeitsbericht-backend.service
  log "systemd-Unit aus Bundle übernommen"
fi
if [ -f scripts/ec2-dsql-bootstrap.sh ]; then
  install -m 755 scripts/ec2-dsql-bootstrap.sh /opt/taetigkeitsbericht/scripts/ec2-dsql-bootstrap.sh
fi

chmod +x /opt/taetigkeitsbericht/backend/Taetigkeitsbericht.Backend 2>/dev/null || true

dnf install -y libicu 2>/dev/null || yum install -y libicu || true

if [ -f /etc/taetigkeitsbericht/backend.env ]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/taetigkeitsbericht/backend.env
  set +a
fi

grep -q '^DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=' /etc/taetigkeitsbericht/backend.env 2>/dev/null \
  || echo 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true' >> /etc/taetigkeitsbericht/backend.env

: "${DSQL_ENDPOINT:=${Database__Host:-}}"
: "${EC2_ROLE_ARN:=}"
: "${AWS_REGION:=${AWS_DEFAULT_REGION:-}}"
export DSQL_ENDPOINT EC2_ROLE_ARN AWS_REGION
export AWS_DEFAULT_REGION="${AWS_REGION:-}"

if [ -z "${DSQL_ENDPOINT}" ] || [ -z "${EC2_ROLE_ARN}" ] || [ -z "${AWS_REGION}" ]; then
  log "DSQL-Bootstrap übersprungen (Env unvollständig)"
  exit 0
fi

AWS_BIN="/usr/local/bin/aws"
if [ ! -x "$AWS_BIN" ]; then AWS_BIN="$(command -v aws)"; fi
if ! "$AWS_BIN" dsql help >/dev/null 2>&1; then
  log "AWS CLI ohne DSQL – installiere AWS CLI v2 nach /usr/local/bin"
  dnf install -y unzip 2>/dev/null || yum install -y unzip || true
  curl -fsSL "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o /tmp/awscliv2.zip
  unzip -qo /tmp/awscliv2.zip -d /tmp
  /tmp/aws/install -u
  AWS_BIN="/usr/local/bin/aws"
fi
export AWS_BIN

/opt/taetigkeitsbericht/scripts/ec2-dsql-bootstrap.sh
log "fertig"
