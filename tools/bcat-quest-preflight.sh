#!/usr/bin/env bash
# BCaT Quest Release memory preflight.
#
# Answers, without launching the APK: how much runtime memory should this exact
# Release build need, what is using it, and is it likely to OOM on a Quest 3.
#
# Observation only — it opens the main scene read-only, measures the imported
# Android representation of every dependency, and writes:
#   Builds/Diagnostics/QuestMemoryPreflight.txt   (for people)
#   Builds/Diagnostics/QuestMemoryPreflight.json  (complete asset accounting)
#
# -buildTarget Android matters: the tool measures the IMPORTED representation,
# so the platform the editor last imported for is the platform it reports.
set -uo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_METHOD="BCaT.EditorTools.Diagnostics.QuestMemoryPreflight.Run"
OUT_DIR="$PROJECT_ROOT/Builds/Diagnostics"
LOG="$OUT_DIR/preflight-unity.log"

UNITY_CANDIDATES=(
  "${UNITY_PATH:-}"
  "/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
)
UNITY=""
for candidate in "${UNITY_CANDIDATES[@]}"; do
  [[ -n "$candidate" && -x "$candidate" ]] && UNITY="$candidate" && break
done
[[ -z "$UNITY" ]] && { echo "Unity 6000.4.5f1 not found. Set UNITY_PATH." >&2; exit 1; }

# Batch mode needs the project's Library to itself; an open editor holds the lock.
if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null 2>&1; then
  echo "The Unity editor is running. Close it first — batch mode cannot open a locked project." >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
echo "[preflight] running Unity batch mode (this walks the whole main-scene dependency graph)…"

"$UNITY" -batchmode -quit -nographics \
  -projectPath "$PROJECT_ROOT" \
  -buildTarget Android \
  -executeMethod "$UNITY_METHOD" \
  -logFile "$LOG"
status=$?

if [[ $status -ne 0 ]]; then
  echo "[preflight] Unity exited $status. Tail of $LOG:" >&2
  tail -40 "$LOG" >&2
  exit $status
fi

REPORT="$OUT_DIR/QuestMemoryPreflight.txt"
if [[ ! -f "$REPORT" ]]; then
  echo "[preflight] no report was produced. Tail of $LOG:" >&2
  tail -40 "$LOG" >&2
  exit 1
fi

cat "$REPORT"
echo
echo "[preflight] text: $REPORT"
echo "[preflight] json: $OUT_DIR/QuestMemoryPreflight.json"
echo "[preflight] unity log: $LOG"
