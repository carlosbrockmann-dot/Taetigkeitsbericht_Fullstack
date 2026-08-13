#!/usr/bin/env bash
set -eu
systemctl stop taetigkeitsbericht-backend 2>/dev/null || true
exit 0
