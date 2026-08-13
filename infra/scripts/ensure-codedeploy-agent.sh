#!/usr/bin/env bash
# Auf der EC2: CodeDeploy-Agent installieren/starten (idempotent).
# Kein dnf update – das blockiert oder rebootet die Instanz.
set -euo pipefail
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

TOKEN="$(curl -fsS -X PUT "http://169.254.169.254/latest/api/token" \
  -H "X-aws-ec2-metadata-token-ttl-seconds: 21600" || true)"
if [ -n "$TOKEN" ]; then
  REGION="$(curl -fsS -H "X-aws-ec2-metadata-token: $TOKEN" \
    http://169.254.169.254/latest/meta-data/placement/region)"
else
  REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-eu-central-1}}"
fi

systemctl enable amazon-ssm-agent 2>/dev/null || true
systemctl start amazon-ssm-agent 2>/dev/null || true

if systemctl is-active --quiet codedeploy-agent 2>/dev/null; then
  echo "codedeploy-agent läuft bereits"
  systemctl is-active codedeploy-agent
  exit 0
fi

dnf install -y ruby wget tar gzip 2>/dev/null || yum install -y ruby wget tar gzip
cd /tmp
wget -q "https://aws-codedeploy-${REGION}.s3.${REGION}.amazonaws.com/latest/install" -O /tmp/codedeploy-install \
  || wget -q "https://aws-codedeploy-${REGION}.s3.amazonaws.com/latest/install" -O /tmp/codedeploy-install
chmod +x /tmp/codedeploy-install
/tmp/codedeploy-install auto
systemctl enable codedeploy-agent
systemctl restart codedeploy-agent
sleep 3
systemctl is-active codedeploy-agent
echo "codedeploy-agent bereit"
