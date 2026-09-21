#!/usr/bin/env bash
set -euo pipefail

configuration="${1:?Pass Debug or Release}"
cargo_args=()
case "$configuration" in
    Debug) ;;
    Release) cargo_args+=(--release) ;;
    *) echo "Unsupported musl build configuration: $configuration" >&2; exit 1 ;;
esac

if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
    echo "The musl cross-build currently requires an x64 Linux build host." >&2
    exit 1
fi

for tool in cargo x86_64-buildroot-linux-musl-gcc x86_64-buildroot-linux-musl-g++ x86_64-buildroot-linux-musl-ar; do
    if ! command -v "$tool" >/dev/null; then
        echo "Missing $tool. See README for the musl cross-toolchain prerequisites." >&2
        exit 1
    fi
done

export CARGO_TARGET_X86_64_UNKNOWN_LINUX_MUSL_LINKER=x86_64-buildroot-linux-musl-gcc
export CC_x86_64_unknown_linux_musl=x86_64-buildroot-linux-musl-gcc
export CXX_x86_64_unknown_linux_musl=x86_64-buildroot-linux-musl-g++
export AR_x86_64_unknown_linux_musl=x86_64-buildroot-linux-musl-ar

# Cargo gives encoded flags priority over RUSTFLAGS. Preserve whichever is effective.
if [[ -n "${CARGO_ENCODED_RUSTFLAGS:-}" ]]; then
    export CARGO_ENCODED_RUSTFLAGS="${CARGO_ENCODED_RUSTFLAGS}"$'\x1f-C\x1ftarget-feature=-crt-static'
else
    unset CARGO_ENCODED_RUSTFLAGS
    export RUSTFLAGS="${RUSTFLAGS:+$RUSTFLAGS }-C target-feature=-crt-static"
fi

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo_root/c2pa-rs"
# TODO: Add musl ARM64 with its matching toolchain and native Alpine consumer gate.
cargo build --locked "${cargo_args[@]}" --target-dir target --target x86_64-unknown-linux-musl \
    --no-default-features --features rust_native_crypto,http,add_thumbnails -p c2pa-c-ffi
