#!/usr/bin/env bash
# Idempotenter DSQL-Bootstrap + Rechte auf das self-contained Binary.
# Kein DROP / Truncate – bestehende Daten bleiben erhalten.
set -euo pipefail

chmod +x /opt/taetigkeitsbericht/backend/Taetigkeitsbericht.Backend || true
chmod +x /opt/taetigkeitsbericht/scripts/ec2-dsql-bootstrap.sh || true

if [ -f /etc/taetigkeitsbericht/backend.env ]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/taetigkeitsbericht/backend.env
  set +a
fi

: "${DSQL_ENDPOINT:=${Database__Host:-}}"
: "${EC2_ROLE_ARN:=}"
: "${AWS_REGION:=${AWS_DEFAULT_REGION:-}}"

if [ -n "${DSQL_ENDPOINT}" ] && [ -n "${EC2_ROLE_ARN}" ] && [ -n "${AWS_REGION}" ]; then
  /opt/taetigkeitsbericht/scripts/ec2-dsql-bootstrap.sh
else
  echo "DSQL-Bootstrap übersprungen (DSQL_ENDPOINT/EC2_ROLE_ARN/AWS_REGION fehlen in backend.env)"
fi
