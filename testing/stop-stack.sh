#!/usr/bin/env bash
# Stops what start-stack.sh started (by port), and leaves the database container running.
for port in 8000 5292 5173; do
  pids=$(lsof -ti tcp:$port -sTCP:LISTEN || true)
  [ -n "$pids" ] && kill $pids && echo "stopped :$port"
done
true
