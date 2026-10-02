#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <MigrationName>" >&2
  exit 1
fi

migration_name="$1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
providers=(Postgres Sqlite)

for provider in "${providers[@]}"; do
  project="$repo_root/src/Bearcat.Infrastructure.Migrations.$provider"

  echo "Adding migration $migration_name for $provider..."
  dotnet ef migrations add "$migration_name" \
    --project "$project" \
    --startup-project "$project" \
    --output-dir Migrations \
    --namespace "Bearcat.Infrastructure.Migrations.$provider"
done

echo "Formatting migrations..."
dotnet csharpier format \
  "$repo_root/src/Bearcat.Infrastructure.Migrations.Postgres/Migrations" \
  "$repo_root/src/Bearcat.Infrastructure.Migrations.Sqlite/Migrations"

echo "Added migration $migration_name for ${providers[*]}"
