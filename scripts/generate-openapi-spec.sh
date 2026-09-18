#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
host_url="http://localhost:5701"
spec_url="$host_url/openapi/v1.json"
target_file="$repo_root/website/public/openapi/v1.json"
host_pid=""

cleanup() {
  if [ -n "$host_pid" ]; then
    kill "$host_pid" 2>/dev/null || true
    wait "$host_pid" 2>/dev/null || true
  fi
}

trap cleanup EXIT

echo "Building Bearcat host..."
dotnet build "$repo_root/src/Bearcat.Host"

echo "Starting Bearcat host in OpenAPI spec only mode..."
ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="$host_url" \
  Bearcat__OpenApiSpecOnly=true \
  dotnet run --project "$repo_root/src/Bearcat.Host" --no-build --no-launch-profile &
host_pid=$!

echo "Waiting for $spec_url..."
for attempt in $(seq 1 60); do
  if curl --fail --silent --output /dev/null "$spec_url"; then
    break
  fi

  if ! kill -0 "$host_pid" 2>/dev/null; then
    echo "The Bearcat host exited before serving the OpenAPI spec." >&2
    exit 1
  fi

  if [ "$attempt" -eq 60 ]; then
    echo "The Bearcat host did not serve $spec_url in time." >&2
    exit 1
  fi

  sleep 1
done

mkdir -p "$(dirname "$target_file")"
curl --fail --silent --output "$target_file" "$spec_url"

if [ ! -s "$target_file" ]; then
  echo "The generated OpenAPI spec at $target_file is empty." >&2
  exit 1
fi

if ! python3 -c "import json, sys; json.load(open(sys.argv[1]))" "$target_file"; then
  echo "The generated OpenAPI spec at $target_file is not valid JSON." >&2
  exit 1
fi

if ! grep -q '"Bearcat API"' "$target_file"; then
  echo "The generated OpenAPI spec does not contain the expected API title." >&2
  exit 1
fi

echo "Wrote OpenAPI spec to $target_file"
