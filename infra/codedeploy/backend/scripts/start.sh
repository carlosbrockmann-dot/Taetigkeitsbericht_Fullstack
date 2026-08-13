#!/usr/bin/env bash
set -euo pipefail
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

PARAM="${JWT_SSM_PARAMETER:-/taetigkeitsbericht/jwt-key}"

if [ -f /etc/taetigkeitsbericht/backend.env ]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/taetigkeitsbericht/backend.env
  set +a
fi
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"
: "${REGION:?AWS_REGION fehlt (backend.env)}"

if [ -f scripts/taetigkeitsbericht-backend.service ]; then
  install -m 644 scripts/taetigkeitsbericht-backend.service /etc/systemd/system/taetigkeitsbericht-backend.service
fi

if ! aws ssm get-parameter --name "$PARAM" --with-decryption --region "$REGION" \
     --query Parameter.Value --output text > /tmp/jwt.val; then
  echo "FEHLER: SSM-Parameter $PARAM nicht lesbar (IAM kms:Decrypt / ssm:GetParameter?)" >&2
  exit 1
fi
umask 077
printf 'Jwt__Key=%s\n' "$(cat /tmp/jwt.val)" > /etc/taetigkeitsbericht/jwt.env
rm -f /tmp/jwt.val
chmod 600 /etc/taetigkeitsbericht/jwt.env

mkdir -p /etc/systemd/system/taetigkeitsbericht-backend.service.d
cat > /etc/systemd/system/taetigkeitsbericht-backend.service.d/override.conf <<'EOF'
[Service]
Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
EOF

systemctl daemon-reload
systemctl enable taetigkeitsbericht-backend
systemctl restart taetigkeitsbericht-backend

ok=0
for i in $(seq 1 60); do
  if systemctl is-active --quiet taetigkeitsbericht-backend; then
    ok=1
    break
  fi
  sleep 5
done

if [ "$ok" != "1" ]; then
  echo "FEHLER: Backend-Dienst nicht active" >&2
  systemctl status taetigkeitsbericht-backend --no-pager -l || true
  journalctl -u taetigkeitsbericht-backend -n 120 --no-pager || true
  ls -la /opt/taetigkeitsbericht/backend | head || true
  exit 1
fi

echo "Backend-Dienst active"
