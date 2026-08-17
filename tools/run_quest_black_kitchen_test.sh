#!/usr/bin/env bash
set -euo pipefail

PACKAGE_NAME="org.bcatlab.blackhomeplaces"
APK_RELATIVE="Builds/Quest/Black Homeplaces XR - Quest.apk"
UNITY_METHOD="BCaT.EditorTools.ProductionBuildPipeline.BuildQuest"
BLACK_KITCHEN_KEY="BlackKitchen_MemoryScene"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date +"%Y%m%d_%H%M%S")"
TEST_DIR="$PROJECT_ROOT/QuestTestLogs/BlackKitchen/$TIMESTAMP"
UNITY_LOG="$TEST_DIR/unity-build.log"
FULL_LOG="$TEST_DIR/full-logcat.log"
TRANSITION_LOG="$TEST_DIR/transition-filtered.log"
ERROR_LOG="$TEST_DIR/errors-filtered.log"
STATE_BEFORE="$TEST_DIR/device-state-before-launch.txt"
STATE_AFTER="$TEST_DIR/device-state-after-test.txt"
SUMMARY="$TEST_DIR/test-summary.txt"
APK_PATH="$PROJECT_ROOT/$APK_RELATIVE"
FULL_LOG_PID=""
LIVE_LOG_PID=""
DEVICE_SERIAL=""
UNITY=""
ADB=""
SKIP_BUILD_INSTALL="${BCAT_SKIP_BUILD_INSTALL:-0}"

LIVE_PATTERN='BCAT_QUEST_KITCHEN_TRANSITION|BlackKitchen|LoadingSceneController|Addressables|PlatformRigActivator|ScenePlayerRig|InteractionRouter|Unity|Exception|Error|Fatal|FATAL EXCEPTION|AndroidRuntime|ANR|LaunchCheck|controller|required|Process .*died|Start proc|START u0|Displayed|crash|tombstone'
ERROR_PATTERN='BCAT_QUEST_KITCHEN_TRANSITION|Exception|Error|FATAL EXCEPTION|AndroidRuntime|ANR|crash|Process .*died|SIGSEGV|SIGABRT|LaunchCheck|controller|required|Addressables.*failed|Transition failed'

info() { printf '[quest-bk] %s\n' "$*"; }
warn() { printf '[quest-bk][warning] %s\n' "$*" >&2; }
fail() { printf '[quest-bk][error] %s\n' "$*" >&2; exit 1; }

cleanup() {
  local status=$?
  if [[ -n "${LIVE_LOG_PID:-}" ]] && kill -0 "$LIVE_LOG_PID" 2>/dev/null; then
    kill "$LIVE_LOG_PID" 2>/dev/null || true
    wait "$LIVE_LOG_PID" 2>/dev/null || true
  fi
  if [[ -n "${FULL_LOG_PID:-}" ]] && kill -0 "$FULL_LOG_PID" 2>/dev/null; then
    kill "$FULL_LOG_PID" 2>/dev/null || true
    wait "$FULL_LOG_PID" 2>/dev/null || true
  fi
  if [[ -d "$TEST_DIR" ]]; then
    generate_filtered_logs || true
    if [[ -n "${ADB:-}" && -n "${DEVICE_SERIAL:-}" ]] && adb_has_authorized_device; then
      capture_device_state "$STATE_AFTER" || true
    fi
    generate_summary "$status" || true
    info "Logs saved to: $TEST_DIR"
  fi
  exit "$status"
}
trap cleanup EXIT INT TERM

unity_path() {
  if [[ -n "${UNITY_PATH:-}" && -x "$UNITY_PATH" ]]; then
    printf '%s\n' "$UNITY_PATH"
    return
  fi

  local project_version editor_version candidate
  project_version="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"
  if [[ -f "$project_version" ]]; then
    editor_version="$(awk '/m_EditorVersion:/{print $2; exit}' "$project_version")"
    candidate="/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity"
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return
    fi
  fi

  candidate="$(find /Applications/Unity/Hub/Editor -path '*/Unity.app/Contents/MacOS/Unity' -type f 2>/dev/null | sort -Vr | head -n 1 || true)"
  if [[ -n "$candidate" && -x "$candidate" ]]; then
    printf '%s\n' "$candidate"
    return
  fi

  fail "Unity executable not found. Set UNITY_PATH=/path/to/Unity.app/Contents/MacOS/Unity."
}

adb_path() {
  if [[ -n "${ADB_PATH:-}" && -x "$ADB_PATH" ]]; then
    printf '%s\n' "$ADB_PATH"
    return
  fi

  local unity adb_candidate
  unity="$(unity_path)"
  adb_candidate="$(cd "$(dirname "$unity")/../../.." && pwd)/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
  if [[ -x "$adb_candidate" ]]; then
    printf '%s\n' "$adb_candidate"
    return
  fi

  if command -v adb >/dev/null 2>&1; then
    command -v adb
    return
  fi

  fail "adb not found. Set ADB_PATH=/path/to/adb or install Android platform-tools."
}

run_adb() {
  "$ADB" ${DEVICE_SERIAL:+-s "$DEVICE_SERIAL"} "$@"
}

adb_has_authorized_device() {
  "$ADB" devices | awk 'NR>1 && $2=="device"{found=1} END{exit found ? 0 : 1}'
}

validate_project() {
  [[ -d "$PROJECT_ROOT/Assets" ]] || fail "Unity project path is invalid: $PROJECT_ROOT"
  [[ -f "$PROJECT_ROOT/ProjectSettings/ProjectSettings.asset" ]] || fail "ProjectSettings not found under: $PROJECT_ROOT"
  grep -q "Android: $PACKAGE_NAME" "$PROJECT_ROOT/ProjectSettings/ProjectSettings.asset" ||
    fail "Expected Android package '$PACKAGE_NAME' was not found in ProjectSettings."
}

detect_quest() {
  "$ADB" start-server >/dev/null
  local devices usable unauthorized offline count
  devices="$("$ADB" devices | awk 'NR>1 && NF>=2 {print $1 "\t" $2}')"
  usable="$(printf '%s\n' "$devices" | awk '$2=="device"{print $1}')"
  unauthorized="$(printf '%s\n' "$devices" | awk '$2=="unauthorized"{print $1}')"
  offline="$(printf '%s\n' "$devices" | awk '$2=="offline"{print $1}')"
  count="$(printf '%s\n' "$usable" | sed '/^$/d' | wc -l | tr -d ' ')"

  if [[ -n "$unauthorized" ]]; then
    fail "Quest is connected but unauthorized. Wear the headset and approve USB debugging, then rerun."
  fi
  if [[ -n "$offline" ]]; then
    fail "Quest is connected but offline. Replug USB or restart ADB, then rerun."
  fi
  if [[ "$count" == "0" ]]; then
    fail "No authorized Quest device connected."
  fi
  if [[ "$count" != "1" ]]; then
    fail "Multiple authorized Android devices are connected. Connect exactly one Quest."
  fi

  DEVICE_SERIAL="$(printf '%s\n' "$usable" | sed '/^$/d' | head -n 1)"
  info "Quest device detected: $DEVICE_SERIAL"
  local model
  model="$(run_adb shell getprop ro.product.model 2>/dev/null || true)"
  if [[ -n "$model" ]]; then
    printf '%s\n' "$model" | sed 's/^/[quest-bk] Device model: /'
  else
    warn "Quest authorized, but model query failed. Continuing; USB may be unstable."
  fi
}

wait_for_quest() {
  local seconds="${1:-180}"
  local deadline=$((SECONDS + seconds))
  while (( SECONDS < deadline )); do
    if adb_has_authorized_device; then
      detect_quest
      return 0
    fi
    sleep 2
  done

  fail "No authorized Quest device connected after waiting ${seconds}s. Reconnect USB and approve USB debugging."
}

capture_device_state() {
  local out="$1"
  {
    echo "Captured: $(date)"
    echo "Device: $DEVICE_SERIAL"
    echo
    echo "== get-state =="
    run_adb get-state || true
    echo
    echo "== power =="
    run_adb shell dumpsys power | grep -E 'mWakefulness|Display Power|mInteractive|mHoldingDisplaySuspendBlocker|mHoldingWakeLockSuspendBlocker|mWakeLockSummary|mUserActivitySummary' || true
    echo
    echo "== window/keyguard =="
    run_adb shell dumpsys window | grep -E 'mDreamingLockscreen|mShowingLockscreen|mKeyguard|isStatusBarKeyguard|mAwake|mScreenOn' || true
    echo
    echo "== package installed =="
    run_adb shell pm list packages "$PACKAGE_NAME" || true
    echo
    echo "== focused/resumed activity =="
    run_adb shell dumpsys activity activities | grep -E 'mResumedActivity|ResumedActivity|mFocusedApp|LaunchCheck|blackhomeplaces' || true
    echo
    echo "== recent launch blocking logs =="
    run_adb logcat -d -v time | grep -E 'LaunchCheck|controller|required|blackhomeplaces|REQUIRES_CONTROLLERS' | tail -n 80 || true
  } > "$out"
}

validate_android_addressables() {
  local root="$PROJECT_ROOT/Library/com.unity.addressables/aa/Android"
  local bundle_dir="$root/Android"
  local catalog="$root/catalog.bin"
  local hash="$root/catalog.hash"
  local settings="$root/settings.json"

  [[ -f "$catalog" ]] || fail "Android Addressables catalog missing: $catalog"
  [[ -f "$hash" ]] || fail "Android Addressables hash missing: $hash"
  [[ -f "$settings" ]] || fail "Android Addressables settings missing: $settings"
  [[ -d "$bundle_dir" ]] || fail "Android Addressables bundle directory missing: $bundle_dir"
  ls "$bundle_dir"/blackkitchen_remote_scenes_all_*.bundle >/dev/null 2>&1 ||
    fail "Black Kitchen Android Addressables bundle missing in: $bundle_dir"
  strings "$catalog" | grep -q "$BLACK_KITCHEN_KEY" ||
    fail "Android Addressables catalog does not contain key '$BLACK_KITCHEN_KEY'."

  info "Addressables validation: catalog/hash/settings present; Black Kitchen Android bundle and key found."
}

validate_apk() {
  [[ -f "$APK_PATH" ]] || fail "APK was not produced: $APK_PATH"
  [[ "$APK_AFTER_TS" -gt "$APK_BEFORE_TS" ]] || fail "APK timestamp did not update."
  local entries
  entries="$(unzip -Z1 "$APK_PATH" 'assets/aa/*')"
  printf '%s\n' "$entries" | grep 'assets/aa/catalog.bin' >/dev/null || fail "APK missing Addressables catalog."
  printf '%s\n' "$entries" | grep 'assets/aa/catalog.hash' >/dev/null || fail "APK missing Addressables hash."
  printf '%s\n' "$entries" | grep 'assets/aa/settings.json' >/dev/null || fail "APK missing Addressables settings."
  printf '%s\n' "$entries" | grep 'assets/aa/Android/blackkitchen_remote_scenes_all_.*\.bundle' >/dev/null ||
    fail "APK missing Black Kitchen Android bundle."
  unzip -p "$APK_PATH" assets/aa/catalog.bin | strings | grep "$BLACK_KITCHEN_KEY" >/dev/null ||
    fail "APK catalog does not contain key '$BLACK_KITCHEN_KEY'."
  info "APK validation: Addressables catalog/hash/settings and Black Kitchen key/bundle are present."
}

build_apk() {
  local unity="$1"
  APK_BEFORE_TS=0
  if [[ -f "$APK_PATH" ]]; then
    APK_BEFORE_TS="$(stat -f '%m' "$APK_PATH")"
  fi

  info "Building Quest APK through production pipeline. Log: $UNITY_LOG"
  "$unity" -quit -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android -executeMethod "$UNITY_METHOD" -logFile "$UNITY_LOG"
  APK_AFTER_TS="$(stat -f '%m' "$APK_PATH" 2>/dev/null || echo 0)"

  grep -q 'Addressables: OK' "$UNITY_LOG" || fail "Unity log did not report successful Addressables build."
  grep -q 'Result: Succeeded' "$UNITY_LOG" || fail "Unity log did not report successful player build."
  grep -q 'Errors: 0' "$UNITY_LOG" || fail "Unity build reported errors."
  grep -q 'Quest APK Addressables: catalog/hash/settings present' "$UNITY_LOG" ||
    fail "Unity log did not report successful Quest APK Addressables validation."
  validate_android_addressables
  validate_apk
}

clean_reinstall() {
  if run_adb shell pm list packages "$PACKAGE_NAME" | grep -q "$PACKAGE_NAME"; then
    info "Existing package found; uninstalling cleanly: $PACKAGE_NAME"
    run_adb uninstall "$PACKAGE_NAME" >/dev/null || fail "Failed to uninstall existing package."
  else
    info "Package is not currently installed; performing fresh install."
  fi

  info "Installing APK: $APK_PATH"
  run_adb install -r -d "$APK_PATH" || fail "ADB install failed."
  run_adb shell pm list packages "$PACKAGE_NAME" | grep -q "$PACKAGE_NAME" ||
    fail "Package was not present after install."

  {
    echo "== installed package =="
    run_adb shell dumpsys package "$PACKAGE_NAME" | grep -E 'Package \[|versionCode|versionName|firstInstallTime|lastUpdateTime|primaryCpuAbi|User 0' || true
    echo "APK: $APK_PATH"
  } | tee "$TEST_DIR/install-result.txt"
}

start_loggers() {
  info "Clearing previous Android logs."
  run_adb logcat -c
  info "Starting full log capture: $FULL_LOG"
  run_adb logcat -v time > "$FULL_LOG" &
  FULL_LOG_PID=$!
  sleep 1

  info "Starting live filtered log stream. Press Enter after the headset test to stop capture."
  run_adb logcat -v time | awk -v pattern="$LIVE_PATTERN" '
    $0 ~ pattern { print "[live] " $0; fflush(); }
  ' &
  LIVE_LOG_PID=$!
}

stop_live_logger_only() {
  if [[ -n "${LIVE_LOG_PID:-}" ]] && kill -0 "$LIVE_LOG_PID" 2>/dev/null; then
    kill "$LIVE_LOG_PID" 2>/dev/null || true
    wait "$LIVE_LOG_PID" 2>/dev/null || true
  fi
  LIVE_LOG_PID=""
}

launch_app_once() {
  info "Launching $PACKAGE_NAME."
  run_adb shell monkey -p "$PACKAGE_NAME" -c android.intent.category.LAUNCHER 1 >/dev/null || true
  sleep 8

  local pid focused recent_block
  pid="$(run_adb shell pidof "$PACKAGE_NAME" 2>/dev/null || true)"
  focused="$(run_adb shell dumpsys activity activities | grep -E 'mResumedActivity|ResumedActivity|mFocusedApp' | grep -E 'blackhomeplaces|LaunchCheck|controller|required' || true)"
  recent_block="$(run_adb logcat -d -v time | grep -E 'LaunchCheck|REQUIRES_CONTROLLERS|blackhomeplaces' | tail -n 80 || true)"

  # A running process is proof the launch was not blocked. Check that first:
  # the bare words "controller"/"required" match Unity's own XR startup lines
  # (e.g. "Meta Quest Touch Plus Controller Profile") and previously produced
  # a false "Horizon blocked launch" on a perfectly healthy run.
  if [[ -n "$pid" ]]; then
    info "Application process started and remains running: pid=$pid"
    run_adb logcat -d -v time | grep -E 'Unity|BCaT|BlackKitchen|AndroidRuntime|Exception' | tail -n 80 > "$TEST_DIR/launch-result.txt" || true
    return 0
  fi

  if printf '%s\n%s\n' "$focused" "$recent_block" | grep -Eq 'LaunchCheck|REQUIRES_CONTROLLERS'; then
    warn "Horizon OS appears to have blocked launch because controllers/headset readiness is required."
    printf '%s\n' "$focused" >> "$TEST_DIR/launch-result.txt"
    return 2
  fi

  warn "Application process is not running after launch attempt."
  run_adb logcat -d -v time | grep -E 'blackhomeplaces|Unity|AndroidRuntime|FATAL EXCEPTION|Process .*died|crash|LaunchCheck|controller|required' | tail -n 120 > "$TEST_DIR/launch-result.txt" || true
  return 1
}

launch_with_one_relaunch_if_blocked() {
  local result=0
  launch_app_once || result=$?
  if [[ "$result" == "2" ]]; then
    cat <<'PROMPT'

Horizon blocked the launch because the headset/controllers are not ready.
Put on the headset, wake and unlock it, and confirm both controllers are powered on and tracking.
Press Enter to try one relaunch without rebuilding or reinstalling.
PROMPT
    read -r
    detect_quest
    run_adb logcat -c
    launch_app_once || result=$?
  fi
  return "$result"
}

generate_filtered_logs() {
  [[ -f "$FULL_LOG" ]] || return 0
  grep -E -C 6 "$LIVE_PATTERN" "$FULL_LOG" > "$TRANSITION_LOG" || true
  grep -E -C 12 "$ERROR_PATTERN" "$FULL_LOG" > "$ERROR_LOG" || true
}

current_process_alive() {
  run_adb shell pidof "$PACKAGE_NAME" 2>/dev/null || true
}

generate_summary() {
  local exit_status="${1:-0}"
  local alive resumed last_transition first_failure unity_started addressables_start addressables_done load_start load_done scene_activation fade_back process_death crash_or_anr
  if [[ -n "${ADB:-}" && -n "${DEVICE_SERIAL:-}" ]]; then
    alive="$(current_process_alive)"
    resumed="$(run_adb shell dumpsys activity activities | grep -E 'mResumedActivity|ResumedActivity|mFocusedApp' | head -n 20 || true)"
  else
    alive=""
    resumed="ADB/device was not available."
  fi
  last_transition="$(grep 'BCAT_QUEST_KITCHEN_TRANSITION' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  first_failure="$(grep -E 'stage='\''Transition failed'\''|Addressables.*failed|Exception|FATAL EXCEPTION|AndroidRuntime|ANR|Process .*died' "$FULL_LOG" 2>/dev/null | head -n 1 || true)"
  unity_started="$(grep -E 'Unity version|I/Unity|BCaT|Application\.unityVersion' "$FULL_LOG" 2>/dev/null | head -n 1 || true)"
  addressables_start="$(grep -E 'Addressables initialization start' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  addressables_done="$(grep -E 'Addressables initialization complete|Addressables initialization failed' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  load_start="$(grep -E 'Addressable scene load start|Built-in scene load start|LoadingScene load start' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  load_done="$(grep -E 'Addressable scene load completed callback|Built-in scene activation completed' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  scene_activation="$(grep -E 'Scene activation|activeScene=' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  fade_back="$(grep -E 'Fade-from-black complete|Fade overlay removed|Transition completed' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  process_death="$(grep -E 'Process .*blackhomeplaces.*died|Process .*exited|Killing .*blackhomeplaces' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"
  crash_or_anr="$(grep -E 'FATAL EXCEPTION|AndroidRuntime|ANR|SIGSEGV|SIGABRT' "$FULL_LOG" 2>/dev/null | tail -n 1 || true)"

  {
    echo "Quest Black Kitchen Diagnostic Summary"
    echo "Generated: $(date)"
    echo "Script exit status: $exit_status"
    echo "Test directory: $TEST_DIR"
    echo "Package: $PACKAGE_NAME"
    echo "APK: $APK_PATH"
    echo
    echo "Build result:"
    grep -E 'Addressables: OK|Addressables validation|Result: Succeeded|Errors: 0|Quest APK Addressables' "$UNITY_LOG" 2>/dev/null || true
    echo
    echo "Runtime state:"
    echo "Process alive: ${alive:-no}"
    echo "Resumed/focused activity:"
    printf '%s\n' "$resumed"
    echo
    echo "Evidence markers:"
    echo "Unity started: ${unity_started:-not found}"
    echo "Addressables start: ${addressables_start:-not found}"
    echo "Addressables completion/failure: ${addressables_done:-not found}"
    echo "Scene load start: ${load_start:-not found}"
    echo "Scene load completion/status: ${load_done:-not found}"
    echo "Scene activation/latest active scene marker: ${scene_activation:-not found}"
    echo "Fade-back/completion: ${fade_back:-not found}"
    echo "Last transition marker: ${last_transition:-not found}"
    echo "First failure/crash marker: ${first_failure:-not found}"
    echo "Process death marker: ${process_death:-not found}"
    echo "Crash/ANR marker: ${crash_or_anr:-not found}"
    echo
    echo "Initial interpretation:"
    if [[ -z "$unity_started" ]]; then
      echo "- Unity startup was not observed. If LaunchCheck/controller-required messages are present, treat this as Horizon launch blocking, not a Unity transition failure."
    elif [[ -n "$crash_or_anr" ]]; then
      echo "- Android reported a runtime crash/ANR marker. Inspect errors-filtered.log and full-logcat.log before making transition conclusions."
    elif [[ -n "$last_transition" && -z "$fade_back" ]]; then
      echo "- Transition logs exist but fade-back/completion was not observed. The last transition marker is the current last successful stage."
    elif [[ -n "$fade_back" ]]; then
      echo "- Fade-back/completion markers were observed. Confirm headset-visible result manually before declaring fixed."
    else
      echo "- No complete transition evidence was found. Inspect filtered logs for launch/input state."
    fi
  } > "$SUMMARY"
}

main() {
  mkdir -p "$TEST_DIR"
  UNITY="$(unity_path)"
  ADB="$(adb_path)"
  info "Project: $PROJECT_ROOT"
  info "Unity: $UNITY"
  info "ADB: $ADB"
  info "Test logs: $TEST_DIR"

  validate_project
  detect_quest
  if [[ "$SKIP_BUILD_INSTALL" == "1" ]]; then
    info "BCAT_SKIP_BUILD_INSTALL=1; reusing existing APK/install for log capture."
    [[ -f "$APK_PATH" ]] || fail "Cannot skip build/install because APK is missing: $APK_PATH"
    run_adb shell pm list packages "$PACKAGE_NAME" | grep -q "$PACKAGE_NAME" ||
      fail "Cannot skip install because package is not installed: $PACKAGE_NAME"
  else
    build_apk "$UNITY"
    detect_quest
    clean_reinstall
  fi

  capture_device_state "$STATE_BEFORE"
  cat <<'PROMPT'

Put on the headset, wake and unlock it, and confirm both controllers are powered on and tracking.
Press Enter when ready to clear logs, start capture, and launch the app.
PROMPT
  read -r
  wait_for_quest 180

  start_loggers
  local launch_result=0
  launch_with_one_relaunch_if_blocked || launch_result=$?

  if [[ "$launch_result" == "0" ]]; then
    cat <<'PROMPT'

In the headset:
1. Enter the Main House.
2. Go to the Black Kitchen entrance.
3. Activate it once.
4. Do not press the interaction button repeatedly.
5. Observe whether the kitchen appears, the Main House returns, the screen remains black, or the app exits.
6. Return to Terminal and press Enter to finish the capture.
PROMPT
  else
    warn "Launch did not reach a running app process. Keep logs open if you want to inspect the headset state, then press Enter to finish."
  fi

  read -r
  capture_device_state "$STATE_AFTER"
  stop_live_logger_only
  if [[ -n "${FULL_LOG_PID:-}" ]] && kill -0 "$FULL_LOG_PID" 2>/dev/null; then
    kill "$FULL_LOG_PID" 2>/dev/null || true
    wait "$FULL_LOG_PID" 2>/dev/null || true
    FULL_LOG_PID=""
  fi
  generate_filtered_logs
  generate_summary 0

  info "Complete. Test directory: $TEST_DIR"
  info "Summary: $SUMMARY"
  info "Transition filtered log: $TRANSITION_LOG"
  info "Errors filtered log: $ERROR_LOG"
}

main "$@"
