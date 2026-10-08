#!/usr/bin/env bash
# Runs the API security collection with Newman (Docker image with the htmlextra reporter, so
# nothing has to be installed). The API and AI service must be running locally.
# Usage: testing/security/run-newman.sh [report-folder] [collection folder]
#   report-folder defaults to reports/security/latest; collection folder is one of
#   "Member 1" .. "Member 4" or "Group" (default: every folder).
set -euo pipefail
testing="$(cd "$(dirname "$0")/.." && pwd)"
out="reports/security/${1:-latest}"
mkdir -p "$testing/$out" && chmod 777 "$testing/$out"
folder=("${2:+--folder}" "${2:-}")
[ -n "${2:-}" ] || folder=()
python3 "$testing/security/build_collection.py" >/dev/null
docker run --rm -v "$testing":/etc/newman dannydainton/htmlextra run \
  security/foundu-security.postman_collection.json -e security/local.postman_environment.json \
  --env-var api=http://host.docker.internal:5292 --env-var ai=http://host.docker.internal:8000 \
  --working-dir security ${folder[@]+"${folder[@]}"} -r cli,htmlextra,junit \
  --reporter-htmlextra-export "$out/newman-report.html" --reporter-junit-export "$out/newman-junit.xml" \
  --reporter-htmlextra-title "FoundU API security tests"
