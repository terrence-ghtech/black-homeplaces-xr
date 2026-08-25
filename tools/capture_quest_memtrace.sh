#!/usr/bin/env bash
# TEMPORARY DIAGNOSTIC — Quest main-scene OOM memory attribution capture.
#
# Launches the installed Quest build and records, until the process dies:
#   logcat-full.log      every logcat buffer (includes the [BCAT_MEMTRACE] stream)
#   memtrace.log         just the [BCAT_MEMTRACE] lines, in order
#   statm-200ms.log      /proc/<pid>/statm sampled on-device every 200 ms
#   meminfo-200ms.log    dumpsys meminfo <pid> every 200 ms (the Unknown table)
#   exit-info.txt        ApplicationExitInfo — Android's own reason for the kill
#   summary.txt          the largest RSS steps, correlated with the nearest event
#
# Measured facts about this device (Quest 3, Horizon OS / Android 14, SDK 34):
#   * adb shell CAN read /proc/<pid>/statm of the app (group readproc) — O(1),
#     no page-table walk, so 200 ms sampling is free.
#   * adb shell CANNOT read /proc/<pid>/smaps or smaps_rollup for another uid,
#     and a release APK is not debuggable so run-as is refused. Mapping-level
#     attribution therefore comes from the in-app sampler (MemTraceSampler),
#     which reads its OWN smaps and logs it to the same logcat stream.
#   * dumpsys meminfo costs ~20 ms, so it can be sampled at 200 ms too.
#
# Usage:  tools/capture_quest_memtrace.sh [--serial SERIAL] [--no-launch]
set -uo pipefail

PACKAGE="org.bcatlab.blackhomeplaces"
ACTIVITY="com.unity3d.player.UnityPlayerGameActivity"
SAMPLE_MS=200
OUT_ROOT="$HOME/Desktop"
SERIAL=""
LAUNCH=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --serial) SERIAL="$2"; shift 2 ;;
    --no-launch) LAUNCH=0; shift ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

ADB_CANDIDATES=(
  "${BCAT_ADB:-}"
  "/Applications/Unity/Hub/Editor/6000.4.5f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
)
ADB=""
for candidate in "${ADB_CANDIDATES[@]}"; do
  [[ -n "$candidate" && -x "$candidate" ]] && ADB="$candidate" && break
done
[[ -z "$ADB" ]] && { echo "No adb found. Set BCAT_ADB to Unity's adb." >&2; exit 1; }
# Unity's adb only — the Homebrew build fights this one over the server.

adb_run() {
  if [[ -n "$SERIAL" ]]; then "$ADB" -s "$SERIAL" "$@"; else "$ADB" "$@"; fi
}

STAMP="$(date +%Y%m%d_%H%M%S)"
OUT="$OUT_ROOT/BCAT_MEMTRACE_$STAMP"
mkdir -p "$OUT"
echo "[memtrace] output: $OUT"

adb_run wait-for-device >/dev/null 2>&1
adb_run shell 'echo device-ok' >/dev/null || { echo "device not reachable" >&2; exit 1; }

# Device context worth having next to the numbers.
{
  echo "captured: $(date)"
  echo "package: $PACKAGE"
  adb_run shell getprop ro.product.model
  adb_run shell getprop ro.build.version.release
  adb_run shell getprop ro.build.version.sdk
  adb_run shell 'cat /proc/meminfo | head -5'
  adb_run shell "pm path $PACKAGE"
  adb_run shell "dumpsys package $PACKAGE | grep -E 'versionName|firstInstallTime|lastUpdateTime'"
} > "$OUT/device.txt" 2>&1

summarize() {
  [[ -f "$OUT/logcat-full.log" ]] || return 0
  grep -a "BCAT_MEMTRACE" "$OUT/logcat-full.log" > "$OUT/memtrace.log" 2>/dev/null
  grep -aiE "lowmemorykiller|lmkd|Low on memory|Killing .*$PACKAGE|died" "$OUT/logcat-full.log" \
    > "$OUT/kill-events.log" 2>/dev/null

  {
    echo "BCaT Quest memtrace capture — $STAMP"
    echo "pid: $(cat "$OUT/pid.txt" 2>/dev/null)"
    echo
    echo "--- checkpoints and RSS samples, in order ---"
    awk '{ for (i=1;i<=NF;i++) if ($i ~ /^(t|wall|ev|scene|rss|alloc|progress|detail)=/) printf "%s ", $i; print "" }' \
      "$OUT/memtrace.log" 2>/dev/null
    echo
    echo "--- largest RSS steps seen by the in-app sampler ---"
    grep -a "ev=RSS_SAMPLE" "$OUT/memtrace.log" 2>/dev/null |
      sed -E 's/.*t=([^ ]+).*rss=([0-9.]+)\(\+([0-9.]+)\).*/\3 MB step at t=\1 (rss \2 MB)/' |
      sort -rn | head -20
    echo
    echo "--- smaps categories at each jump ---"
    grep -a "ev=SMAPS_CATEGORIES" "$OUT/memtrace.log" 2>/dev/null
    echo
    echo "--- largest mappings ---"
    grep -a "ev=SMAPS_TOP" "$OUT/memtrace.log" 2>/dev/null
    echo
    echo "--- kill events ---"
    tail -40 "$OUT/kill-events.log" 2>/dev/null
  } > "$OUT/summary.txt" 2>&1
}

PIDS=()
cleanup() {
  local status=$?
  for pid in "${PIDS[@]:-}"; do
    [[ -n "${pid:-}" ]] && kill "$pid" 2>/dev/null
  done
  # $! of a pipeline is its last stage, so the `tail -f` feeding the live view
  # has to be closed by name.
  pkill -f "tail -f $OUT/logcat-full.log" 2>/dev/null
  wait 2>/dev/null
  summarize
  echo "[memtrace] logs saved to: $OUT"
  exit "$status"
}
trap cleanup EXIT INT TERM

# 1. Clear every buffer so the capture starts empty.
adb_run logcat -b all -c 2>/dev/null || adb_run logcat -c

# 2. Full logcat, and the trace stream on its own.
adb_run logcat -b all -v threadtime > "$OUT/logcat-full.log" 2>&1 &
PIDS+=("$!")

# 3. Launch.
if [[ $LAUNCH -eq 1 ]]; then
  adb_run shell "am force-stop $PACKAGE" >/dev/null 2>&1
  sleep 1
  adb_run shell "am start -S -n $PACKAGE/$ACTIVITY" > "$OUT/launch.txt" 2>&1 ||
    adb_run shell "monkey -p $PACKAGE -c android.intent.category.LAUNCHER 1" >> "$OUT/launch.txt" 2>&1
fi

# 4. Resolve the pid (the app may take a few seconds to appear).
APP_PID=""
for _ in $(seq 1 60); do
  APP_PID="$(adb_run shell "pidof $PACKAGE" 2>/dev/null | tr -d '\r' | awk '{print $1}')"
  [[ -n "$APP_PID" ]] && break
  sleep 0.5
done
[[ -z "$APP_PID" ]] && { echo "[memtrace] app never started" >&2; exit 1; }
echo "[memtrace] pid=$APP_PID sampling every ${SAMPLE_MS}ms"
echo "$APP_PID" > "$OUT/pid.txt"

SLEEP_S="$(awk -v ms="$SAMPLE_MS" 'BEGIN{printf "%.3f", ms/1000}')"

# 5. /proc/<pid>/statm every 200 ms. One persistent shell — a new adb shell per
#    sample costs more than the interval. Field 2 is resident pages (x4096 B).
adb_run shell "while [ -d /proc/$APP_PID ]; do \
  echo \"uptime=\$(awk '{print \$1}' /proc/uptime) wall=\$(date +%H:%M:%S.%3N) statm=\$(cat /proc/$APP_PID/statm 2>/dev/null)\"; \
  sleep $SLEEP_S; done" > "$OUT/statm-200ms.log" 2>&1 &
PIDS+=("$!")

# 6. dumpsys meminfo every 200 ms — the categorised table (Native/Graphics/Unknown).
adb_run shell "while [ -d /proc/$APP_PID ]; do \
  echo \"=== SAMPLE uptime=\$(awk '{print \$1}' /proc/uptime) wall=\$(date +%H:%M:%S.%3N)\"; \
  dumpsys meminfo $APP_PID 2>/dev/null; \
  sleep $SLEEP_S; done" > "$OUT/meminfo-200ms.log" 2>&1 &
PIDS+=("$!")

# 7. smaps from the host, in case a future build IS debuggable (run-as) — the
#    release build's own in-app sampler covers the release case.
if adb_run shell "run-as $PACKAGE id" >/dev/null 2>&1; then
  echo "[memtrace] app is debuggable: host-side smaps sampling enabled"
  adb_run shell "while [ -d /proc/$APP_PID ]; do \
    echo \"=== SMAPS_ROLLUP uptime=\$(awk '{print \$1}' /proc/uptime)\"; \
    run-as $PACKAGE cat /proc/$APP_PID/smaps_rollup 2>/dev/null; \
    sleep 1; done" > "$OUT/smaps-rollup-1s.log" 2>&1 &
  PIDS+=("$!")
else
  echo "[memtrace] release (non-debuggable) build: mapping detail comes from the in-app SMAPS_* log lines" \
    > "$OUT/smaps-note.txt"
fi

# 8. Live view of the trace while it runs.
tail -f "$OUT/logcat-full.log" 2>/dev/null | grep --line-buffered -E "BCAT_MEMTRACE|lowmemorykiller|lmkd|Low on memory|died|ActivityManager.*Killing" &
PIDS+=("$!")

# 9. Hold until the process is gone.
while adb_run shell "[ -d /proc/$APP_PID ] && echo alive" 2>/dev/null | grep -q alive; do
  sleep 1
done
echo "[memtrace] pid $APP_PID is gone — collecting post-mortem"
sleep 2

{
  echo "=== ApplicationExitInfo ==="
  adb_run shell "dumpsys activity exit-info $PACKAGE"
  echo
  echo "=== meminfo (system) ==="
  adb_run shell "cat /proc/meminfo"
} > "$OUT/exit-info.txt" 2>&1
