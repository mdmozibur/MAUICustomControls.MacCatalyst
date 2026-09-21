#!/usr/bin/env bash
# Builds AppKitBridge.bundle (macOS arm64 + x86_64) and AppKitBridge.bundle.zip, which the app
# project ships as a CompressedPlugIns item (unzipped into Contents/PlugIns and signed by the SDK).
# Run with: bash ./build_bundle.sh

set -euo pipefail

NAME="AppKitBridge"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$ROOT_DIR/.build"
OUTPUT_DIR="$ROOT_DIR/bin"
BUNDLE="$OUTPUT_DIR/$NAME.bundle"
SDK_PATH="$(xcrun --sdk macosx --show-sdk-path)"

rm -rf "$BUILD_DIR" "$BUNDLE" "$OUTPUT_DIR/$NAME.bundle.zip"
mkdir -p "$BUILD_DIR" "$BUNDLE/Contents/MacOS"

for arch in arm64 x86_64; do
  echo "Building $NAME for $arch"
  xcrun --sdk macosx clang \
    -target "$arch-apple-macos12.0" \
    -isysroot "$SDK_PATH" \
    -fobjc-arc \
    -bundle \
    -framework AppKit \
    "$ROOT_DIR/$NAME.m" \
    -o "$BUILD_DIR/$NAME-$arch"
done

lipo -create "$BUILD_DIR/$NAME-arm64" "$BUILD_DIR/$NAME-x86_64" -output "$BUNDLE/Contents/MacOS/$NAME"
cp "$ROOT_DIR/Info.plist" "$BUNDLE/Contents/Info.plist"
# Seal the bundle (ad hoc); the app build re-signs it with the app's identity.
codesign --force --sign - "$BUNDLE"
(cd "$OUTPUT_DIR" && ditto -c -k --keepParent "$NAME.bundle" "$NAME.bundle.zip")
echo "Built $BUNDLE and $NAME.bundle.zip"
