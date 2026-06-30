#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
schema_path="$repo_root/schemas/c2pa-standard-assertions.schema.json"
out_path="$repo_root/lib/StandardAssertions.cs"

if [[ ! -f "$schema_path" ]]; then
  echo "Schema not found: $schema_path" >&2
  exit 1
fi

# Pin via QUICKTYPE_VERSION if needed (for example: QUICKTYPE_VERSION=23.2.6)
quicktype_pkg="quicktype"
if [[ -n "${QUICKTYPE_VERSION:-}" ]]; then
  quicktype_pkg="quicktype@${QUICKTYPE_VERSION}"
fi

npx -y "$quicktype_pkg" \
  --lang cs \
  --src-lang schema \
  "$schema_path" \
  --framework SystemTextJson \
  --namespace ContentAuthenticity.Schema \
  --top-level C2PaStandardAssertionsSchema \
  -o "$out_path"

echo "Generated $out_path from $schema_path"
