#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p "${1:-../runtime/linux-x64}"
gcc -Wall -Wextra -Werror -shared -fPIC -O2 -o "${1:-../runtime/linux-x64}/libdrone_native.so" drone_native.c
