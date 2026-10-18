#!/usr/bin/env bash
# Builds release zips: dist/OffroadPro-<version>-<platform>.zip for windows and linux.
# Usage: tools/package.sh <version> [GameDir]
#   GameDir = game folder with BepInEx installed and run once (needed to compile against the interop assemblies).
set -euo pipefail

VERSION="${1:?usage: tools/package.sh <version> [GameDir]}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAMEDIR="${2:-$HOME/.local/share/Steam/steamapps/common/over the hill}"
BEPINEX_BUILD=789
BEPINEX_TAG="6.0.0-be.${BEPINEX_BUILD}%2B6cea286"
CACHE="$ROOT/.cache"
DIST="$ROOT/dist"
mkdir -p "$CACHE" "$DIST"

# Game build the zips are made against: Steam build id from the library's appmanifest (override with GAME_BUILD=...).
MANIFEST="$(dirname "$(dirname "$GAMEDIR")")/appmanifest_2929250.acf"
GAME_BUILD="${GAME_BUILD:-$(sed -n 's/.*"buildid"[[:space:]]*"\([0-9]*\)".*/\1/p' "$MANIFEST" 2>/dev/null | head -1)}"
GAME_BUILD="${GAME_BUILD:-unknown}"

fetch() {   # platform -> cached BepInEx zip
    local plat="$1" out="$CACHE/BepInEx-Unity.IL2CPP-$1-6.0.0-be.${BEPINEX_BUILD}.zip"
    [ -s "$out" ] || curl -fL --retry 8 -o "$out" \
        "https://builds.bepinex.dev/projects/bepinex_be/${BEPINEX_BUILD}/BepInEx-Unity.IL2CPP-${plat}-${BEPINEX_TAG}.zip"
    echo "$out"
}
[ -s "$CACHE/BepInEx-LICENSE.txt" ] || curl -fL -o "$CACHE/BepInEx-LICENSE.txt" \
    https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE

dotnet build "$ROOT/OthDriveSystems.csproj" -c Release -v q -p:GameDir="$GAMEDIR"
DLL="$ROOT/bin/Release/net6.0/OthDriveSystems.dll"

for PLAT in Windows Linux; do
    case "$PLAT" in
        Windows) BEP=$(fetch win-x64)
                 STEP="3. Nothing else to set up on Windows."
                 UNEXTRA=" (winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt, dotnet folder)" ;;
        Linux)   BEP=$(fetch linux-x64)
                 STEP="3. Linux: in Steam > Over the Hill > Properties > Launch Options, enter (with your path):
   \"/path/to/steamapps/common/over the hill/run_bepinex.sh\" %command%"
                 UNEXTRA=" (libdoorstop.so, run_bepinex.sh, .doorstop_version, changelog.txt, dotnet folder) and clear the launch option" ;;
    esac
    STAGE="$(mktemp -d)"
    unzip -q "$BEP" -d "$STAGE"
    if [ "$PLAT" = Linux ]; then
        sed -i 's/^executable_name=""/executable_name="over the hill.x86_64"/' "$STAGE/run_bepinex.sh"
        chmod +x "$STAGE/run_bepinex.sh"
    fi
    mkdir -p "$STAGE/BepInEx/config" "$STAGE/BepInEx/plugins/OthDriveSystems"
    cp "$ROOT/packaging/BepInEx.cfg" "$STAGE/BepInEx/config/BepInEx.cfg"
    cp "$DLL" "$STAGE/BepInEx/plugins/OthDriveSystems/"
    cp "$ROOT/LICENSE" "$STAGE/LICENSE-OffroadPro.txt"
    cp "$CACHE/BepInEx-LICENSE.txt" "$STAGE/LICENSE-BepInEx.txt"
    cp "$ROOT/tools/remove_mod_items.py" "$STAGE/remove_mod_items.py"
    python3 - "$ROOT/packaging/INSTALL.txt" "$STAGE/OffroadPro-INSTALL.txt" "$VERSION" "$STEP" "$UNEXTRA" <<'EOF'
import sys
src, dst, ver, step, extra = sys.argv[1:]
t = open(src).read().replace("{VERSION}", ver).replace("{PLATFORM_STEP}", step).replace("{UNINSTALL_EXTRA}", extra)
if dst.endswith(".txt") and "windows" in step.lower():
    t = t.replace("\n", "\r\n")
open(dst, "w", newline="").write(t)
EOF
    OUT="$DIST/OffroadPro_${VERSION}_${PLAT}_build${GAME_BUILD}.zip"
    rm -f "$OUT"
    python3 - "$STAGE" "$OUT" <<'EOF2'
import os, sys, zipfile
stage, out = sys.argv[1:]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, dirs, files in os.walk(stage):
        dirs.sort()
        for f in sorted(files):
            full = os.path.join(root, f)
            info = zipfile.ZipInfo.from_file(full, os.path.relpath(full, stage))
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(full, "rb") as fh:
                z.writestr(info, fh.read())   # keeps unix mode bits (run_bepinex.sh stays executable)
EOF2
    rm -rf "$STAGE"
    echo "$OUT"
done
