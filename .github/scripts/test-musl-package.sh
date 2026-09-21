#!/bin/sh
set -eu

version="${1:?Pass the locally built NuGet package version}"
project=tests/PackageSmoke/PackageSmoke.csproj
config=tests/PackageSmoke/NuGet.Config
output=/tmp/c2pa-package-smoke
export NUGET_PACKAGES="$output/packages"

# Source mapping restricts ContentAuthenticity to the local package under test.
dotnet restore "$project" --configfile "$config" -p:ContentAuthenticityPackageVersion="$version"
dotnet build "$project" --no-restore --configuration Release \
    -p:ContentAuthenticityPackageVersion="$version" --output "$output/portable"
test -f "$output/portable/runtimes/linux-musl-x64/native/libc2pa_c.so"
dotnet "$output/portable/PackageSmoke.dll" "$output/portable/runtimes/linux-musl-x64/native/libc2pa_c.so"

dotnet restore "$project" --configfile "$config" --runtime linux-musl-x64 \
    -p:ContentAuthenticityPackageVersion="$version"
dotnet publish "$project" --no-restore --configuration Release \
    --runtime linux-musl-x64 --self-contained false \
    -p:ContentAuthenticityPackageVersion="$version" --output "$output/published"
test -f "$output/published/libc2pa_c.so"
dotnet "$output/published/PackageSmoke.dll" "$output/published/libc2pa_c.so"
