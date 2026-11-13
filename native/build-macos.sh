#!/usr/bin/env bash
# Builds a universal (Intel + Apple Silicon) library with the Xcode command line tools.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p ../runtime/osx
clang -Wall -Wextra -Werror -dynamiclib -O2 -arch x86_64 -arch arm64 -o ../runtime/osx/libdrone_native.dylib drone_native.c
echo "Built runtime/osx/libdrone_native.dylib"
