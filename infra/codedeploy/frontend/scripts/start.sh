#!/usr/bin/env bash
set -euo pipefail
systemctl enable nginx
systemctl reload nginx 2>/dev/null || systemctl restart nginx
systemctl is-active nginx
