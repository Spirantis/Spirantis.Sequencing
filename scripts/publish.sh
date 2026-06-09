#!/usr/bin/env bash
#
# Packs the Spirantis.Sequencing NuGet packages (+ symbols) and pushes them to nuget.org.
#
# The API key is read from the first argument or the NUGET_API_KEY environment variable
# (so it never has to be hard-coded). Both packages are versioned from src/Directory.Build.props.
#
#   ./scripts/publish.sh <nuget-api-key>
#   NUGET_API_KEY=*** ./scripts/publish.sh
#
# Notes:
# - Spirantis.Sequencing.Abstraction is its own package; Spirantis.Sequencing's ProjectReference
#   to it is converted by `dotnet pack` into a NuGet dependency on the published Abstraction package.
# - nuget.org automatically publishes the matching .snupkg symbol package alongside each .nupkg.
# - --skip-duplicate makes re-runs idempotent if a version is already published.

set -euo pipefail

API_KEY="${1:-${NUGET_API_KEY:-}}"
if [[ -z "$API_KEY" ]]; then
  echo "error: a nuget.org API key is required." >&2
  echo "usage: $0 <api-key>   (or set NUGET_API_KEY)" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOLUTION="$REPO_ROOT/src/Spirantis.Sequencing.slnx"
OUTPUT="$REPO_ROOT/artifacts/packages"
SOURCE="https://api.nuget.org/v3/index.json"

echo "==> Packing (Release) into $OUTPUT"
rm -rf "$OUTPUT"
dotnet pack "$SOLUTION" --configuration Release --output "$OUTPUT"

echo "==> Packages produced:"
ls -1 "$OUTPUT"/*.nupkg

echo "==> Pushing to $SOURCE"
dotnet nuget push "$OUTPUT/*.nupkg" \
  --api-key "$API_KEY" \
  --source "$SOURCE" \
  --skip-duplicate

echo "==> Published."
