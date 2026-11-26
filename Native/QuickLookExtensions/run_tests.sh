#!/usr/bin/env bash
# Runs CadPreviewReader over sample drawings and checks what it finds:
#   - every DWG R13+ whose preview section holds a BMP or PNG, every .sc/.scx with a <preview>,
#     and a DXF with a THUMBNAILIMAGE section give an image;
#   - files without one report noPreview (Quick Look then shows the plain icon), never a crash.
# The preview PNGs land in .build/test-output for a look.
# Run with: bash ./run_tests.sh

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE="$(cd "$ROOT_DIR/../../.." && pwd)"
BUILD_DIR="$ROOT_DIR/.build"
OUT="$BUILD_DIR/test-output"
mkdir -p "$BUILD_DIR"
rm -rf "$OUT"

xcrun --sdk macosx swiftc -O -target "$(uname -m)-apple-macos14.0" \
  "$ROOT_DIR/Shared/CadPreviewReader.swift" "$ROOT_DIR/Tests/main.swift" \
  -o "$BUILD_DIR/CadPreviewTest"

EXAMPLES="$WORKSPACE/SmartCad2D/SmartCad2D/Assets/Examples"
SAMPLES="$WORKSPACE/SmartCad2D/SmartCadUnitTest/Resources/ACAD_Files"
FIXTURES="$ROOT_DIR/Tests/Fixtures"

# file|expected: OK (image) or NONE (no preview)
CASES=(
  "$EXAMPLES/Shopping center project.dwg|OK"                # R2000, BMP
  "$EXAMPLES/Canteen.dwg|OK"                                # R2004, BMP
  "$SAMPLES/Floor Plan Sample.dwg|OK"                       # R2007, BMP
  "$SAMPLES/anteen.dwg|OK"                                  # R2013, PNG
  "$SAMPLES/tableExample.dwg|OK"                            # R2018, PNG
  "$SAMPLES/OpenRegression/r12-staircase-detail.dwg|NONE"   # R12: no preview section
  "$SAMPLES/Constraints/gc_fix.dwg|NONE"                 # header block only
  "$FIXTURES/thumbnail.dxf|OK"                              # THUMBNAILIMAGE section (BMP)
  "$FIXTURES/no-thumbnail.dxf|NONE"
  "$FIXTURES/drawing.sc|OK"                                 # <preview> PNG
  "$FIXTURES/no-preview.sc|NONE"
  "$FIXTURES/drawing.scx|OK"                                # encrypted
  "$FIXTURES/truncated.dwg|NONE"
)

failed=0
for entry in "${CASES[@]}"; do
  file="${entry%|*}"
  expected="${entry##*|}"
  if [[ ! -f "$file" ]]; then
    echo "MISSING  $file"
    failed=1
    continue
  fi
  line="$("$BUILD_DIR/CadPreviewTest" "$OUT" "$file" || true)"
  actual="${line%% *}"
  if [[ "$actual" == "$expected" ]]; then
    echo "pass  $line"
  else
    echo "FAIL  expected $expected: $line"
    failed=1
  fi
done

if [[ $failed -ne 0 ]]; then
  echo "Quick Look preview reader tests failed."
  exit 1
fi
echo "Quick Look preview reader tests passed (images in $OUT)."
