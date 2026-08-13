#!/usr/bin/env bash
# Auf der Backend-EC2 (via SSM): DSQL-Rolle anlegen + IAM GRANT (idempotent).
# Kein DROP DATABASE / Truncate / Schema-Reset – bestehende Daten bleiben erhalten.
# Erforderliche ENV: DSQL_ENDPOINT, EC2_ROLE_ARN, AWS_REGION (oder AWS_DEFAULT_REGION)
set -euo pipefail

: "${DSQL_ENDPOINT:?DSQL_ENDPOINT fehlt}"
: "${EC2_ROLE_ARN:?EC2_ROLE_ARN fehlt}"
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"
: "${REGION:?AWS_REGION fehlt}"

echo "DSQL bootstrap: endpoint=${DSQL_ENDPOINT} role=${EC2_ROLE_ARN} region=${REGION}"

if ! command -v psql >/dev/null 2>&1; then
  dnf install -y postgresql15 2>/dev/null || yum install -y postgresql15 || dnf install -y postgresql || true
fi
if ! command -v psql >/dev/null 2>&1; then
  echo "psql nicht installierbar – bitte Client manuell bereitstellen" >&2
  exit 1
fi

ADMIN_TOKEN="$(aws dsql generate-db-connect-admin-auth-token \
  --hostname "${DSQL_ENDPOINT}" \
  --region "${REGION}")"
export PGPASSWORD="${ADMIN_TOKEN}"
export PGSSLMODE=require

run_sql() {
  psql -h "${DSQL_ENDPOINT}" -U admin -d postgres -v ON_ERROR_STOP=0 -c "$1" || true
}

# Rolle (Fehler „already exists“ tolerieren)
run_sql "CREATE ROLE verwaltung WITH LOGIN;"

# IAM-Verknüpfung EC2-Rolle ↔ DB-Rolle (erneut ausführbar)
run_sql "AWS IAM GRANT verwaltung TO '${EC2_ROLE_ARN}';"

run_sql "GRANT CONNECT ON DATABASE postgres TO verwaltung;"
run_sql "GRANT USAGE ON SCHEMA public TO verwaltung;"
run_sql "GRANT CREATE ON SCHEMA public TO verwaltung;"
run_sql "GRANT ALL ON SCHEMA public TO verwaltung;"

echo "DSQL bootstrap fertig."
