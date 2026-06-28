#!/usr/bin/env bash
set -euo pipefail

WEBSITE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../src/Bearcat.Website" && pwd)"
cd "$WEBSITE_DIR"

TAILWIND_VERSION="v4.3.1"

case "$(uname -s)" in
    Darwin) OS="macos" ;;
    Linux) OS="linux" ;;
    *) OS="windows" ;;
esac

case "$(uname -m)" in
    arm64 | aarch64) ARCH="arm64" ;;
    *) ARCH="x64" ;;
esac

CLI="obj/tailwind/${TAILWIND_VERSION}/tailwindcss-${OS}-${ARCH}"

if [ ! -x "$CLI" ]; then
    echo "Tailwind CLI not found ($CLI). Build Bearcat.Website once without the skip to download it:" >&2
    echo "  dotnet build src/Bearcat.Website -p:SkipTailwindBuild=false" >&2
    exit 1
fi

echo "Watching Tailwind sources -> wwwroot/css/app.css (Ctrl+C to stop)"
exec "$CLI" \
    --input wwwroot/css/app-input.css \
    --output wwwroot/css/app.css \
    --watch
