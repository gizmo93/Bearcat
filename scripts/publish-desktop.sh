#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
runtimes=("$@")

if [ "${#runtimes[@]}" -eq 0 ]; then
  runtimes=(osx-arm64 win-x64 win-arm64)
fi

publish_runtime() {
  local runtime="$1"
  local staging_root="$repo_root/artifacts/staging/$runtime"
  local output_dir="$repo_root/artifacts/desktop/$runtime"

  case "$runtime" in
    osx-arm64 | win-x64 | win-arm64) ;;
    *)
      echo "Unsupported runtime: $runtime" >&2
      echo "Supported runtimes: osx-arm64, win-x64, win-arm64" >&2
      exit 1
      ;;
  esac

  rm -rf "$staging_root" "$output_dir"
  mkdir -p "$staging_root/host" "$staging_root/desktop" "$output_dir"

  echo "Restoring runtime assets for $runtime..."
  dotnet restore "$repo_root/src/Bearcat.Host/Bearcat.Host.csproj" --runtime "$runtime"
  dotnet restore "$repo_root/src/Bearcat.Desktop/Bearcat.Desktop.csproj" --runtime "$runtime"

  echo "Publishing Bearcat.Host for $runtime..."
  dotnet publish "$repo_root/src/Bearcat.Host/Bearcat.Host.csproj" \
    --configuration "$configuration" \
    --runtime "$runtime" \
    --self-contained true \
    --no-restore \
    --output "$staging_root/host" \
    -p:PublishSingleFile=false \
    -p:ServerGarbageCollection=false

  echo "Publishing Bearcat.Desktop for $runtime..."
  dotnet publish "$repo_root/src/Bearcat.Desktop/Bearcat.Desktop.csproj" \
    --configuration "$configuration" \
    --runtime "$runtime" \
    --self-contained true \
    --no-restore \
    --output "$staging_root/desktop" \
    -p:PublishSingleFile=false

  cp -a "$staging_root/desktop/." "$output_dir/"
  cp -a "$staging_root/host/." "$output_dir/"
  find "$output_dir" -exec touch {} +

  if [ "$runtime" = "osx-arm64" ]; then
    chmod +x "$output_dir/Bearcat.Desktop" "$output_dir/Bearcat.Host"
    package_macos_app "$staging_root" "$output_dir"
  fi

  echo "Published desktop artifact to $output_dir"

  if command -v stat >/dev/null 2>&1; then
    if [[ "$OSTYPE" == darwin* ]]; then
      stat -f "%Sm %N" -t "%Y-%m-%d %H:%M:%S" \
        "$output_dir/Bearcat.Desktop.dll" \
        "$output_dir/Bearcat.Desktop.exe" \
        "$output_dir/Bearcat.Desktop" 2>/dev/null || true
    else
      stat -c "%y %n" \
        "$output_dir/Bearcat.Desktop.dll" \
        "$output_dir/Bearcat.Desktop.exe" \
        "$output_dir/Bearcat.Desktop" 2>/dev/null || true
    fi
  fi
}

package_macos_app() {
  local staging_root="$1"
  local output_dir="$2"
  local app_root="$output_dir/Bearcat Desktop.app"
  local contents_dir="$app_root/Contents"
  local macos_dir="$contents_dir/MacOS"
  local resources_dir="$contents_dir/Resources"
  local icon_source="$repo_root/src/Bearcat.Desktop/Assets/bearcat-icon.png"
  local iconset="$staging_root/BearcatIcon.iconset"
  local version="${GITHUB_REF_NAME:-0.0.0}"

  version="${version#v}"

  rm -rf "$app_root" "$iconset"
  mkdir -p "$macos_dir" "$resources_dir" "$iconset"

  cp -a "$staging_root/desktop/." "$macos_dir/"
  cp -a "$staging_root/host/." "$macos_dir/"
  chmod +x "$macos_dir/Bearcat.Desktop" "$macos_dir/Bearcat.Host"

  if [ -d "$macos_dir/wwwroot" ]; then
    mv "$macos_dir/wwwroot" "$resources_dir/wwwroot"
    ln -s "../Resources/wwwroot" "$macos_dir/wwwroot"
  fi

  # The Playwright driver ships a nested node binary; nested code under
  # Contents/MacOS breaks codesign ("bundle format unrecognized"). Relocate it
  # into Resources and symlink it back, exactly like wwwroot above.
  if [ -d "$macos_dir/.playwright" ]; then
    mv "$macos_dir/.playwright" "$resources_dir/.playwright"
    ln -s "../Resources/.playwright" "$macos_dir/.playwright"
  fi

  if command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
    sips -z 16 16 "$icon_source" --out "$iconset/icon_16x16.png" >/dev/null
    sips -z 32 32 "$icon_source" --out "$iconset/icon_16x16@2x.png" >/dev/null
    sips -z 32 32 "$icon_source" --out "$iconset/icon_32x32.png" >/dev/null
    sips -z 64 64 "$icon_source" --out "$iconset/icon_32x32@2x.png" >/dev/null
    sips -z 128 128 "$icon_source" --out "$iconset/icon_128x128.png" >/dev/null
    sips -z 256 256 "$icon_source" --out "$iconset/icon_128x128@2x.png" >/dev/null
    sips -z 256 256 "$icon_source" --out "$iconset/icon_256x256.png" >/dev/null
    sips -z 512 512 "$icon_source" --out "$iconset/icon_256x256@2x.png" >/dev/null
    sips -z 512 512 "$icon_source" --out "$iconset/icon_512x512.png" >/dev/null
    cp "$icon_source" "$iconset/icon_512x512@2x.png"
    iconutil -c icns "$iconset" -o "$resources_dir/bearcat-icon.icns"
  fi

  cat > "$contents_dir/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>Bearcat Desktop</string>
    <key>CFBundleExecutable</key>
    <string>Bearcat.Desktop</string>
    <key>CFBundleIconFile</key>
    <string>bearcat-icon</string>
    <key>CFBundleIdentifier</key>
    <string>io.github.gizmo93.bearcat.desktop</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>Bearcat Desktop</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$version</string>
    <key>CFBundleVersion</key>
    <string>$version</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

  if command -v codesign >/dev/null 2>&1; then
    sign_macos_app "$app_root" "$macos_dir"
  fi

  echo "Published macOS app bundle to $app_root"
}

sign_macos_app() {
  local app_root="$1"
  local macos_dir="$2"

  while IFS= read -r file_path; do
    if ! file "$file_path" | grep -q "Mach-O"; then
      codesign --force --sign - "$file_path"
    fi
  done < <(find "$macos_dir" -type f)

  while IFS= read -r file_path; do
    if file "$file_path" | grep -q "Mach-O"; then
      codesign --force --sign - "$file_path"
    fi
  done < <(find "$macos_dir" -type f)

  codesign --force --sign - "$app_root"
  codesign --verify --strict --verbose=2 "$app_root"
}

for runtime in "${runtimes[@]}"; do
  publish_runtime "$runtime"
done
