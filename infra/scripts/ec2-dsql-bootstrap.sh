#!/usr/bin/env bash
# DSQL-Rolle anlegen + IAM GRANT (idempotent). Kein DROP.
# DDL im Schema public macht das Backend als admin (EF-Migrationen).
# ENV: DSQL_ENDPOINT, EC2_ROLE_ARN, AWS_REGION
# Optional: AWS_BIN (Pfad zur AWS-CLI mit dsql-Subcommand)
set -euo pipefail
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

: "${DSQL_ENDPOINT:?DSQL_ENDPOINT fehlt}"
: "${EC2_ROLE_ARN:?EC2_ROLE_ARN fehlt}"
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"
: "${REGION:?AWS_REGION fehlt}"
AWS_BIN="${AWS_BIN:-/usr/local/bin/aws}"
if [ ! -x "$AWS_BIN" ]; then AWS_BIN="$(command -v aws)"; fi

echo "DSQL bootstrap: endpoint=${DSQL_ENDPOINT} role=${EC2_ROLE_ARN} region=${REGION} aws=${AWS_BIN}"

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
  if echo "$out" | grep -qiE 'already exists|duplicate|already been granted|already a member'; then
    echo "idempotent ok: ${sql}"
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

# In DSQL legt nur admin Objekte in public an. verwaltung = App-Login, kein CREATE auf public.
run_sql "CREATE ROLE verwaltung WITH LOGIN;"
run_sql "AWS IAM GRANT verwaltung TO '${EC2_ROLE_ARN}';"
run_sql "GRANT USAGE ON SCHEMA public TO verwaltung;"

echo "DSQL bootstrap fertig (Tabellen kommen per EF-Migration als admin)."
