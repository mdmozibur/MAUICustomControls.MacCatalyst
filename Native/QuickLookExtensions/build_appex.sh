#!/usr/bin/env bash
# Builds the Quick Look extensions of the Mac app (Mac Catalyst, arm64 + x86_64):
#   bin/SmartCadQuickLookThumbnail.appex  Finder thumbnails (icons, gallery, Open/Save panels)
#   bin/SmartCadQuickLookPreview.appex    Space bar / Finder preview pane
# next to a <name>.entitlements for each, which the app build signs them with. The converter copies
# bin/ to Platforms/MacCatalyst/Native/QuickLook, and the template project embeds both in
# Contents/PlugIns (AdditionalAppExtensions) and gives them the app's bundle ID prefix and version.
# Then runs the reader's tests (run_tests.sh) unless --no-tests is passed.
# Run with: bash ./build_appex.sh

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$ROOT_DIR/.build"
OUTPUT_DIR="$ROOT_DIR/bin"
SDK_PATH="$(xcrun --sdk macosx --show-sdk-path)"
SDK_VERSION="$(xcrun --sdk macosx --show-sdk-version)"
SDK_BUILD="$(xcrun --sdk macosx --show-sdk-build-version)"
XCODE_VERSION="$(xcodebuild -version | awk '/^Xcode/ {split($2, v, "."); printf "%d%d%d0", v[1], v[2], (v[3] == "" ? 0 : v[3])}')"
XCODE_BUILD="$(xcodebuild -version | awk '/^Build version/ {print $3}')"
# Same minimum as the app (Mac Catalyst 17 = macOS 14).
CATALYST_MIN="17.0"
# swiftc does not add the Catalyst (iOSSupport) search paths by itself for -sdk macosx.
CATALYST_FRAMEWORKS="$SDK_PATH/System/iOSSupport/System/Library/Frameworks"
CATALYST_SWIFT_LIBS="$SDK_PATH/System/iOSSupport/usr/lib/swift"

rm -rf "$BUILD_DIR" "$OUTPUT_DIR"
mkdir -p "$BUILD_DIR" "$OUTPUT_DIR"

for kind in Thumbnail Preview; do
  name="SmartCadQuickLook$kind"
  appex="$OUTPUT_DIR/$name.appex"
  mkdir -p "$appex/Contents/MacOS"

  for arch in arm64 x86_64; do
    echo "Building $name for $arch"
    xcrun --sdk macosx swiftc \
      -target "$arch-apple-ios$CATALYST_MIN-macabi" \
      -sdk "$SDK_PATH" \
      -Fsystem "$CATALYST_FRAMEWORKS" \
      -I "$CATALYST_SWIFT_LIBS" \
      -L "$CATALYST_SWIFT_LIBS" \
      -swift-version 5 \
      -O -whole-module-optimization \
      -parse-as-library \
      -application-extension \
      -module-name "$name" \
      -Xlinker -e -Xlinker _NSExtensionMain \
      "$ROOT_DIR/Shared/CadPreviewReader.swift" \
      "$ROOT_DIR/$kind/"*.swift \
      -o "$BUILD_DIR/$name-$arch"
  done

  lipo -create "$BUILD_DIR/$name-arm64" "$BUILD_DIR/$name-x86_64" -output "$appex/Contents/MacOS/$name"

  plist="$appex/Contents/Info.plist"
  cp "$ROOT_DIR/$kind/Info.plist" "$plist"
  plutil -insert DTPlatformName -string macosx "$plist"
  plutil -insert DTPlatformVersion -string "$SDK_VERSION" "$plist"
  plutil -insert DTPlatformBuild -string "$SDK_BUILD" "$plist"
  plutil -insert DTSDKName -string "macosx$SDK_VERSION" "$plist"
  plutil -insert DTSDKBuild -string "$SDK_BUILD" "$plist"
  plutil -insert DTXcode -string "$XCODE_VERSION" "$plist"
  plutil -insert DTXcodeBuild -string "$XCODE_BUILD" "$plist"
  # Stays XML: the app build edits it, and XML comes out the same for the arm64 and x64 bundles.
  plutil -convert xml1 "$plist"

  cp "$ROOT_DIR/QuickLookExtension.entitlements" "$OUTPUT_DIR/$name.entitlements"
  # Seal it (ad hoc, sandboxed) so it runs as built; the app build re-signs it with the app's identity.
  codesign --force --sign - --entitlements "$ROOT_DIR/QuickLookExtension.entitlements" "$appex"
  echo "Built $appex"
done

if [[ "${1:-}" != "--no-tests" ]]; then
  bash "$ROOT_DIR/run_tests.sh"
fi
