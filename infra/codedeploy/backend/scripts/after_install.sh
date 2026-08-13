#!/usr/bin/env bash
# Idempotenter DSQL-Bootstrap + Rechte auf das self-contained Binary.
# Kein DROP / Truncate – bestehende Daten bleiben erhalten.
set -euo pipefail

log() { echo "[after_install] $*"; }

chmod +x /opt/taetigkeitsbericht/backend/Taetigkeitsbericht.Backend 2>/dev/null || true
chmod +x /opt/taetigkeitsbericht/scripts/ec2-dsql-bootstrap.sh 2>/dev/null || true

dnf install -y libicu 2>/dev/null || yum install -y libicu || true

if [ -f /etc/taetigkeitsbericht/backend.env ]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/taetigkeitsbericht/backend.env
  set +a
else
  log "WARNUNG: /etc/taetigkeitsbericht/backend.env fehlt (UserData?)"
fi

grep -q '^DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=' /etc/taetigkeitsbericht/backend.env 2>/dev/null \
  || echo 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true' >> /etc/taetigkeitsbericht/backend.env

: "${DSQL_ENDPOINT:=${Database__Host:-}}"
: "${EC2_ROLE_ARN:=}"
: "${AWS_REGION:=${AWS_DEFAULT_REGION:-}}"
export DSQL_ENDPOINT EC2_ROLE_ARN AWS_REGION AWS_DEFAULT_REGION="${AWS_REGION:-}"

if [ -z "${DSQL_ENDPOINT}" ] || [ -z "${EC2_ROLE_ARN}" ] || [ -z "${AWS_REGION}" ]; then
  log "DSQL-Bootstrap übersprungen (Env unvollständig)"
  exit 0
fi

if ! aws dsql help >/dev/null 2>&1; then
  log "AWS CLI ohne DSQL – installiere AWS CLI v2"
  dnf install -y unzip 2>/dev/null || yum install -y unzip || true
  curl -fsSL "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o /tmp/awscliv2.zip
  unzip -qo /tmp/awscliv2.zip -d /tmp
  /tmp/aws/install -u
fi

/opt/taetigkeitsbericht/scripts/ec2-dsql-bootstrap.sh
log "fertig"
