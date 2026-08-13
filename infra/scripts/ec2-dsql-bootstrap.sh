#!/usr/bin/env bash
# DSQL-Rolle anlegen + IAM GRANT + App-Schema (idempotent). Kein DROP.
# GRANT auf Schema public ist in DSQL verboten (system entity).
# ENV: DSQL_ENDPOINT, EC2_ROLE_ARN, AWS_REGION
set -euo pipefail
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

APP_SCHEMA="${DSQL_APP_SCHEMA:-taetigkeitsbericht}"
APP_ROLE="${DSQL_APP_ROLE:-verwaltung}"

: "${DSQL_ENDPOINT:?DSQL_ENDPOINT fehlt}"
: "${EC2_ROLE_ARN:?EC2_ROLE_ARN fehlt}"
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"
: "${REGION:?AWS_REGION fehlt}"
AWS_BIN="${AWS_BIN:-/usr/local/bin/aws}"
if [ ! -x "$AWS_BIN" ]; then AWS_BIN="$(command -v aws)"; fi

echo "DSQL bootstrap: endpoint=${DSQL_ENDPOINT} role=${EC2_ROLE_ARN} schema=${APP_SCHEMA} region=${REGION} aws=${AWS_BIN}"

if ! command -v psql >/dev/null 2>&1; then
  dnf install -y postgresql15 2>/dev/null || yum install -y postgresql15 || dnf install -y postgresql || true
fi
if ! command -v psql >/dev/null 2>&1; then
  echo "psql nicht installierbar – bitte Client manuell bereitstellen" >&2
  exit 1
fi

ADMIN_TOKEN="$("$AWS_BIN" dsql generate-db-connect-admin-auth-token \
  --hostname "${DSQL_ENDPOINT}" \
  --region "${REGION}")"
export PGPASSWORD="${ADMIN_TOKEN}"
export PGSSLMODE=require

run_sql() {
  local sql="$1"
  local out rc
  set +e
  out="$(psql -h "${DSQL_ENDPOINT}" -U admin -d postgres -v ON_ERROR_STOP=1 -c "${sql}" 2>&1)"
  rc=$?
  set -e
  if [ "$rc" -eq 0 ]; then
    echo "$out"
    return 0
  fi
  if echo "$out" | grep -qiE 'already exists|duplicate|already been granted|already a member|feature not supported on system entity'; then
    echo "übersprungen (idempotent/DSQL): ${sql}"
    echo "$out"
    return 0
  fi
  echo "$out" >&2
  echo "SQL fehlgeschlagen: ${sql}" >&2
  return 1
}

echo "Warte auf DSQL-Verbindung…"
connected=0
for i in $(seq 1 18); do
  if psql -h "${DSQL_ENDPOINT}" -U admin -d postgres -v ON_ERROR_STOP=1 -c "SELECT 1" >/dev/null 2>&1; then
    connected=1
    break
  fi
  echo "  Versuch $i/18"
  sleep 10
  ADMIN_TOKEN="$("$AWS_BIN" dsql generate-db-connect-admin-auth-token \
    --hostname "${DSQL_ENDPOINT}" \
    --region "${REGION}")"
  export PGPASSWORD="${ADMIN_TOKEN}"
done
if [ "$connected" != "1" ]; then
  echo "DSQL nicht erreichbar (admin / PrivateLink / IAM DbConnectAdmin)" >&2
  exit 1
fi

run_sql "CREATE ROLE ${APP_ROLE} WITH LOGIN;"
run_sql "AWS IAM GRANT ${APP_ROLE} TO '${EC2_ROLE_ARN}';"
run_sql "CREATE SCHEMA IF NOT EXISTS ${APP_SCHEMA};"
run_sql "GRANT USAGE ON SCHEMA ${APP_SCHEMA} TO ${APP_ROLE};"

echo "DSQL bootstrap fertig (kein GRANT auf public). Tabellen: EF-Migration als admin im Schema ${APP_SCHEMA}."
