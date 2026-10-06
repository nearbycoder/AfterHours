#!/usr/bin/env bash
# Runs the Unity 6000.6.2f1 editor against this project.
#
# The editor links against libxml2.so.2 but this distro ships libxml2.so.16, so the editor exits
# immediately unless the loader can find a copy of the old library. `sudo pacman -S libxml2-legacy`
# is the proper fix; until then point LD_LIBRARY_PATH at an extracted copy (AH_UNITY_LIBS).
#
#   Tools/unity.sh                 open the project in the GUI editor
#   Tools/unity.sh resident        headless batch-mode editor that stays up for `unity command`
#   Tools/unity.sh method <Name>   run a static editor method in batch mode and quit
#   Tools/unity.sh build-linux     batch-build Builds/Linux/AfterHours.x86_64
#   Tools/unity.sh build-mac       batch-build Builds/macOS/After Hours.app (universal, unsigned)
#   Tools/unity.sh build-windows   batch-build Builds/Windows/AfterHours.exe (needs the Windows module)
#   Tools/unity.sh test            run EditMode tests, results in Logs/test-results.xml
set -euo pipefail

UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="${AH_UNITY_LIBS:-$HOME/.local/share/ptt-unity-libs}"
[ -d "$LIBS" ] && export LD_LIBRARY_PATH="$LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
mkdir -p "$PROJECT/Logs"

case "${1:-open}" in
  open)
    exec "$UNITY" -projectPath "$PROJECT"
    ;;
  resident)
    exec "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$PROJECT/Logs/resident.log"
    ;;
  method)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -executeMethod "$2" -logFile -
    ;;
  build-linux)
    exec "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
      -executeMethod AfterHours.EditorTools.BuildScript.BuildLinux -logFile -
    ;;
  build-mac)
    exec "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
      -executeMethod AfterHours.EditorTools.BuildScript.BuildMac -logFile -
    ;;
  build-windows)
    exec "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
      -executeMethod AfterHours.EditorTools.BuildScript.BuildWindows -logFile -
    ;;
  test)
    exec "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode \
      -testResults "$PROJECT/Logs/test-results.xml" -logFile -
    ;;
  *)
    echo "usage: $0 [open|resident|method <Name>|build-linux|build-mac|build-windows|test]" >&2
    exit 2
    ;;
esac
