#!/usr/bin/env bash
set -euo pipefail

destination="${1:?Pass an empty installation directory}"
if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
    echo "The musl cross-toolchain requires an x64 Linux build host." >&2
    exit 1
fi

mkdir -p "$destination"
destination="$(cd "$destination" && pwd)"
if [[ -n "$(ls -A "$destination")" ]]; then
    echo "Installation directory must be empty: $destination" >&2
    exit 1
fi

# GCC, binutils, musl headers/libc, and musl-built libgcc must come from one sysroot.
release=x86-64--musl--stable-2025.08-1
sha256=09fca3aa89540f1b01b5f4210d488cbeb00f522044c53e9989b1dd8a38076912
archive="$(mktemp)"
trap 'rm -f "$archive"' EXIT
curl --fail --location --retry 3 \
    "https://toolchains.bootlin.com/downloads/releases/toolchains/x86-64/tarballs/$release.tar.xz" \
    --output "$archive"
printf '%s  %s\n' "$sha256" "$archive" | sha256sum --check
tar --extract --xz --file "$archive" --directory "$destination" --strip-components=1
"$destination/relocate-sdk.sh"
"$destination/bin/x86_64-buildroot-linux-musl-gcc" --version
echo "Add $destination/bin to PATH before enabling BuildLinuxMusl."
