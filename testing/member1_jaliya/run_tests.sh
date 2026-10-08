#!/usr/bin/env bash
# Runs Jaliya H. A. W's tests. Arguments: any of api ai web mobile (default: all).
exec "$(dirname "$0")/../run_member_tests.sh" 1 "$@"
