#!/usr/bin/env bash
set -euo pipefail

RID="${1:-linux-x64}"
CONFIGURATION="${2:-Release}"
INSTALL_BROWSERS="${3:-}"

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$PROJECT_DIR/src/GoFibreSpeedTester/GoFibreSpeedTester.csproj"

echo "Publishing $RID ($CONFIGURATION)..."
dotnet publish "$PROJECT" -c "$CONFIGURATION" -r "$RID" --self-contained true

OUT_DIR="$PROJECT_DIR/src/GoFibreSpeedTester/bin/$CONFIGURATION/net8.0/$RID/publish"
echo ""
echo "Publish output:"
echo "  $OUT_DIR"
echo ""
echo "Next step on the target machine (from the publish folder):"
echo "  pwsh ./playwright.ps1 install"
if [[ "$RID" == linux-* ]]; then
  echo "  (Linux) pwsh ./playwright.ps1 install --with-deps"
fi

if [[ "$INSTALL_BROWSERS" == "--install-browsers" ]]; then
  echo ""
  echo "Installing Playwright browsers in publish folder..."
  pushd "$OUT_DIR" >/dev/null
  if [[ "$RID" == linux-* ]]; then
    pwsh ./playwright.ps1 install --with-deps
  else
    pwsh ./playwright.ps1 install
  fi
  popd >/dev/null
fi

