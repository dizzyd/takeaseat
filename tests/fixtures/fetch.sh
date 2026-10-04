#!/usr/bin/env bash
# Downloads the third-party mods the suite runs against into tests/fixtures/Mods.
# They are other authors' work, so they are fetched rather than kept in this repository.
set -euo pipefail

dir="$(cd "$(dirname "$0")" && pwd)/Mods"
mkdir -p "$dir"

fetch() {
    [ -f "$dir/$1" ] && { echo "have  $1"; return; }
    echo "fetch $1"
    curl -fsSL -o "$dir/$1" "$2"
}

fetch w4rd0sfurniture_1.4.0.zip \
    "https://moddbcdn.vintagestory.at/w4rd0sfurniture_1.4._2d1066f5d2ac2cdd1999f52bd2815799.zip?dl=w4rd0sfurniture_1.4.0.zip"
fetch AttributeRenderingLibrary-v3.2.0.zip \
    "https://moddbcdn.vintagestory.at/AttributeRenderingLi_117a8fe384afa5aaba82adbad4b40bd4.zip?dl=AttributeRenderingLibrary-v3.2.0.zip"
