#!/usr/bin/env bash
set -euo pipefail

PARAM="${JWT_SSM_PARAMETER:-/taetigkeitsbericht/jwt-key}"
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"

if [ -f /etc/taetigkeitsbericht/backend.env ]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/taetigkeitsbericht/backend.env
  set +a
fi
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-$REGION}}"

if aws ssm get-parameter --name "$PARAM" --with-decryption --region "$REGION" \
     --query Parameter.Value --output text > /tmp/jwt.val 2>/dev/null; then
  umask 077
  printf 'Jwt__Key=%s\n' "$(cat /tmp/jwt.val)" > /etc/taetigkeitsbericht/jwt.env
  rm -f /tmp/jwt.val
  chmod 600 /etc/taetigkeitsbericht/jwt.env
else
  echo "Warnung: SSM-Parameter $PARAM nicht lesbar – JWT ggf. bereits in jwt.env"
fi

systemctl daemon-reload
systemctl enable taetigkeitsbericht-backend
systemctl restart taetigkeitsbericht-backend
sleep 8
systemctl is-active taetigkeitsbericht-backend
