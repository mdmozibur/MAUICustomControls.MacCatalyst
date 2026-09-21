#!/usr/bin/env bash
# Builds StoreKitBridge.xcframework (Mac Catalyst arm64 + x86_64): a dynamic framework exposing a
# C API over StoreKit 2. The .NET side calls it through [DllImport("__Internal")] in
# Platforms/MacCatalyst/StoreKitBridge.cs once the app references the xcframework as a
# NativeReference. Run with: bash ./build_xcframework.sh

set -euo pipefail

FRAMEWORK_NAME="StoreKitBridge"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$ROOT_DIR/.build"
OUTPUT_DIR="$ROOT_DIR/bin"
OUTPUT_XCFRAMEWORK="$OUTPUT_DIR/$FRAMEWORK_NAME.xcframework"
MIN_CATALYST_VERSION="15.0"
SDK_PATH="$(xcrun --sdk macosx --show-sdk-path)"
# Mac Catalyst builds against the iOS frameworks and Swift overlays shipped inside the macOS SDK.
CATALYST_FRAMEWORKS="$SDK_PATH/System/iOSSupport/System/Library/Frameworks"
CATALYST_SWIFT_LIBS="$SDK_PATH/System/iOSSupport/usr/lib/swift"

function build_slice() {
  local arch="$1"
  local framework_dir="$BUILD_DIR/$arch/$FRAMEWORK_NAME.framework"

  echo "Building $FRAMEWORK_NAME for $arch (Mac Catalyst)"
  rm -rf "$framework_dir"
  mkdir -p "$framework_dir"

  xcrun --sdk macosx swiftc \
    -target "$arch-apple-ios$MIN_CATALYST_VERSION-macabi" \
    -sdk "$SDK_PATH" \
    -Fsystem "$CATALYST_FRAMEWORKS" \
    -I "$CATALYST_SWIFT_LIBS" \
    -L "$CATALYST_SWIFT_LIBS" \
    -swift-version 5 \
    -O \
    -parse-as-library \
    -module-name "$FRAMEWORK_NAME" \
    -emit-library \
    -Xlinker -install_name -Xlinker "@rpath/$FRAMEWORK_NAME.framework/$FRAMEWORK_NAME" \
    -framework StoreKit \
    "$ROOT_DIR/StoreKitBridge.swift" \
    -o "$framework_dir/$FRAMEWORK_NAME"
}

rm -rf "$BUILD_DIR" "$OUTPUT_XCFRAMEWORK"
mkdir -p "$BUILD_DIR" "$OUTPUT_DIR"

build_slice arm64
build_slice x86_64

UNIVERSAL_DIR="$BUILD_DIR/universal/$FRAMEWORK_NAME.framework"
mkdir -p "$UNIVERSAL_DIR"
lipo -create \
  "$BUILD_DIR/arm64/$FRAMEWORK_NAME.framework/$FRAMEWORK_NAME" \
  "$BUILD_DIR/x86_64/$FRAMEWORK_NAME.framework/$FRAMEWORK_NAME" \
  -output "$UNIVERSAL_DIR/$FRAMEWORK_NAME"
cp "$ROOT_DIR/Info.plist" "$UNIVERSAL_DIR/Info.plist"

xcodebuild -create-xcframework -framework "$UNIVERSAL_DIR" -output "$OUTPUT_XCFRAMEWORK"
echo "Built $OUTPUT_XCFRAMEWORK"
