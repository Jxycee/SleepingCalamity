#!/usr/bin/env bash
# Tools/cloud/unity.sh - SleepingCalamity wrapper for Unity batchmode runs on LINUX (cloud sessions). The same interface and checks as Tools/unity.ps1 (Windows).
#
#   bash Tools/cloud/unity.sh <compile|build|test-edit|test-play|check|gate|soak|exec> [--out DIR] [--filter REGEX] [--timeout MIN] [--project DIR] [--method Class.Method] [--graphics]
#
# test-edit, gate and check run the fast EditMode tier (-testCategory '!Soak'); `soak` runs only [Category("Soak")] (the 1000+ seed sweeps), prints its own SC RESULT soak line and records no merge marker.
#
# --out defaults to $SC_OUT; it must be absolute, contain no '..' and sit outside the project (use the session scratchpad).
# --project defaults to the repo this script is in. --graphics drops -nographics (renders need a graphics device; runs under xvfb-run).
# Editor: ~/unity/ed/Editor/Unity (Tools/cloud/setup.sh / sc_cloud_compile.py --setup put it there; the download needs no licence, but launching it does: 2026-10-29 first Linux run exited 198, 'No valid Unity Editor license found'). Override with SC_UNITY.
# Exit codes: 0 PASS, 1 FAIL, 2 LOCKED (another Unity has this project open), 3 TIMEOUT, 4 usage error.
# Prints `SC RESULT <command> PASS|FAIL|...` lines like unity.ps1, and after a `check` PASS records the commit for the merge gate (<git-common-dir>/sc-check-pass/<sha>).
# First Linux run 2026-10-29: the editor exited 198 (no valid licence) before any project work; the script itself has not been tested past launch.
set -u
USAGE='usage: unity.sh <compile|build|test-edit|test-play|check|gate|soak|exec> --out <absolute scratch dir outside the project> (or set SC_OUT) [--project DIR] [--filter REGEX] [--timeout MIN] [--method Class.Method] [--graphics]'
say() { printf '%s\n' "$*"; }
usage() { say "$USAGE"; [ -n "${1:-}" ] && say "usage: $1"; exit 4; }

COMMAND="${1:-}"; [ $# -gt 0 ] && shift
OUT="${SC_OUT:-}"; FILTER=""; TIMEOUT_MIN=""; METHOD=""; GRAPHICS=0
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/../.." && pwd)"
while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT="${2:-}"; shift 2 ;;
        --filter) FILTER="${2:-}"; shift 2 ;;
        --timeout) TIMEOUT_MIN="${2:-}"; shift 2 ;;
        --project) PROJECT="${2:-}"; shift 2 ;;
        --method) METHOD="${2:-}"; shift 2 ;;
        --graphics) GRAPHICS=1; shift ;;
        *) usage "unknown argument $1" ;;
    esac
done
case "$COMMAND" in compile|build|test-edit|test-play|check|gate|soak|exec) ;; *) usage "unknown or missing command '$COMMAND'" ;; esac
UNITY="${SC_UNITY:-$HOME/unity/ed/Editor/Unity}"
[ -d "$PROJECT/ProjectSettings" ] || usage "project must be an absolute Unity project path: $PROJECT"
PROJECT="$(cd "$PROJECT" && pwd)"
# a fresh container has no Library/: the first launch imports the whole project (~7 min measured), so the default grows
if [ -z "$TIMEOUT_MIN" ]; then
    if [ -f "$PROJECT/Library/ArtifactDB" ]; then TIMEOUT_MIN=5; else TIMEOUT_MIN=20; say "SC NOTE no Library/ yet: first run imports the project, timeout raised to 20 min"; fi
fi
[ "$COMMAND" = exec ] && { printf '%s' "$METHOD" | grep -Eq '^[A-Za-z_][A-Za-z0-9_.]*$' || usage 'exec needs --method <Namespace.Class.Method>'; }
[ -n "$OUT" ] || usage 'no --out given and SC_OUT is not set'
case "$OUT" in /*) ;; *) usage "out must be absolute: $OUT" ;; esac
case "$OUT" in *..*) usage "out must not contain ..: $OUT" ;; esac
case "$OUT/" in "$PROJECT"/*) usage "out must be outside the project: $OUT" ;; esac
if [ "$(cat "$HOME/.sc-setup/unity.status" 2>/dev/null)" = RUNNING ] && [ -z "${SC_UNITY:-}" ]; then
    usage "Unity editor still downloading or extracting in the background (bash Tools/cloud/setup.sh --wait)"
fi
if [ ! -x "$UNITY" ]; then
    [ "$(cat "$HOME/.sc-setup/unity.status" 2>/dev/null)" = RUNNING ] && usage "Unity editor still downloading in the background (bash Tools/cloud/setup.sh --wait)"
    usage "Unity editor not found at $UNITY (run: bash Tools/cloud/setup.sh --wait)"
fi
# licence from the environment's secrets (UNITY_EMAIL + UNITY_PASSWORD, activated by sc_unity_license.sh); without one the editor exits 198
SC_UNITY="$UNITY" bash "$(dirname "${BASH_SOURCE[0]:-$0}")/sc_unity_license.sh" || say "SC WARN no Unity licence installed; the launch will likely exit 198"
mkdir -p "$OUT"

ERR_PATTERN='error CS|Exception|Scripts have compiler errors|Compilation failed'
locked() { pgrep -f -- "-projectPath[ =]\"?$PROJECT\"?( |$)" >/dev/null 2>&1; }

excerpt() { [ -f "$1" ] && grep -E "$2" "$1" | head -40 | sed 's/^/SC LOG /' || say "SC LOG no log file at $1"; }
compile_clean() { [ -f "$1" ] && grep -q 'Exiting batchmode successfully' "$1" && ! grep -q 'error CS' "$1"; }

# run NAME EXTRA...   sets RUN_STATUS (DONE|LOCKED|TIMEOUT), RUN_EXIT, RUN_LOG
run_unity() {
    local name="$1"; shift
    RUN_LOG="$OUT/$name.log"; RUN_EXIT=-1
    if locked; then RUN_STATUS=LOCKED; return; fi
    rm -f "$RUN_LOG"
    local gfx=(-nographics) pre=()
    if [ "$GRAPHICS" = 1 ]; then gfx=(); command -v xvfb-run >/dev/null 2>&1 && pre=(xvfb-run -a); fi
    "${pre[@]}" timeout --signal=TERM --kill-after=30 "$((TIMEOUT_MIN * 60))" "$UNITY" -batchmode "${gfx[@]}" -projectPath "$PROJECT" -logFile "$RUN_LOG" "$@" >/dev/null 2>&1
    RUN_EXIT=$?
    if [ "$RUN_EXIT" = 124 ] || [ "$RUN_EXIT" = 137 ]; then RUN_STATUS=TIMEOUT; else RUN_STATUS=DONE; fi
}
gate() { # NAME  -> returns 2/3 for LOCKED/TIMEOUT after printing, 0 otherwise
    case "$RUN_STATUS" in
        LOCKED) say "SC RESULT $1 LOCKED (another Unity has this project open)"; return 2 ;;
        TIMEOUT) say "SC RESULT $1 TIMEOUT log=$RUN_LOG"; return 3 ;;
    esac
    return 0
}

do_compile() {
    run_unity compile -quit; gate compile || return $?
    if [ "$RUN_EXIT" = 0 ] && compile_clean "$RUN_LOG"; then say "SC RESULT compile PASS log=$RUN_LOG"; return 0; fi
    excerpt "$RUN_LOG" "$ERR_PATTERN"; say "SC RESULT compile FAIL log=$RUN_LOG"; return 1
}
do_build() {
    run_unity build -quit -executeMethod SleepingCalamity.EditorTools.ProjectBuilder.BuildAll; gate build || return $?
    if grep -q 'SC BUILDALL FAIL' "$RUN_LOG" 2>/dev/null; then
        grep '^SC BUILDALL ERROR ' "$RUN_LOG"; excerpt "$RUN_LOG" "$ERR_PATTERN"; say "SC RESULT build FAIL log=$RUN_LOG"; return 1
    fi
    if [ "$RUN_EXIT" = 0 ] && compile_clean "$RUN_LOG" && grep -q 'SC BUILDALL PASS' "$RUN_LOG"; then say "SC RESULT build PASS log=$RUN_LOG"; return 0; fi
    excerpt "$RUN_LOG" "$ERR_PATTERN"; say "SC RESULT build FAIL log=$RUN_LOG"; return 1
}
do_exec() {
    run_unity exec -quit -executeMethod "$METHOD"; gate exec || return $?
    if [ "$RUN_EXIT" = 0 ] && compile_clean "$RUN_LOG"; then say "SC RESULT exec PASS method=$METHOD log=$RUN_LOG"; return 0; fi
    excerpt "$RUN_LOG" "$ERR_PATTERN"; say "SC RESULT exec FAIL method=$METHOD exit=$RUN_EXIT log=$RUN_LOG"; return 1
}
do_tests() { # NAME PLATFORM XML [CATEGORY]
    local name="$1" platform="$2" xml="$OUT/$3" category="${4:-}"
    rm -f "$xml"
    local extra=(-runTests -testPlatform "$platform" -testResults "$xml" -dev-profile sctest)   # never -quit with -runTests
    [ -n "$FILTER" ] && extra+=(-testFilter "$FILTER")
    [ -n "$category" ] && extra+=(-testCategory "$category")
    run_unity "$name" "${extra[@]}"; gate "$name" || return $?
    local total=0 passed=0 failed=0 skipped=0 faillines=""
    if [ -f "$xml" ]; then
        read -r total passed failed skipped < <(python3 - "$xml" <<'PY'
import sys, xml.etree.ElementTree as ET
try:
    r = ET.parse(sys.argv[1]).getroot()
    print(r.get('total', 0), r.get('passed', 0), r.get('failed', 0), r.get('skipped', 0))
except Exception:
    print(0, 0, 0, 0)
PY
)
        faillines="$(python3 - "$xml" <<'PY'
import sys, re, xml.etree.ElementTree as ET
try:
    for c in ET.parse(sys.argv[1]).getroot().iter('test-case'):
        if c.get('result') == 'Failed':
            m = c.find('failure/message'); t = re.sub(r'\s+', ' ', (m.text or '') if m is not None else '').strip()[:200]
            print('SC FAIL %s :: %s' % (c.get('fullname'), t))
except Exception:
    pass
PY
)"
    fi
    local completed=0 completed_any=0
    grep -q 'Test run completed\. Exiting with code 0' "$RUN_LOG" 2>/dev/null && completed=1
    grep -q 'Test run completed\. Exiting with code' "$RUN_LOG" 2>/dev/null && completed_any=1
    if [ "${total:-0}" -gt 0 ] && [ "${failed:-0}" = 0 ] && [ "$completed" = 1 ]; then
        say "SC RESULT $name PASS total=$total passed=$passed failed=0 skipped=$skipped log=$RUN_LOG"; return 0
    fi
    if [ "${total:-0}" = 0 ]; then
        excerpt "$RUN_LOG" "$ERR_PATTERN"; say "SC RESULT $name FAIL total=0 (no tests matched or compile errors) log=$RUN_LOG"; return 1
    fi
    local reason=""; [ "$completed_any" = 0 ] && reason=' (no completion line in log)'
    say "SC RESULT $name FAIL total=$total passed=$passed failed=$failed skipped=$skipped log=$RUN_LOG$reason"
    [ -n "$faillines" ] && say "$faillines"
    return 1
}
# Tree key of a commit's Unity content: the Assets, Packages and ProjectSettings tree ids joined with '-'
# (git rev-parse <rev>:Assets <rev>:Packages <rev>:ProjectSettings). Pipe-free; must match unity.ps1 and .claude/hooks/guard.sh. Empty when unavailable.
unity_tree_key() {
    local out; out="$(git -C "$PROJECT" rev-parse "$1:Assets" "$1:Packages" "$1:ProjectSettings" 2>/dev/null | tr -d '\r')" || return 0
    [ "$(printf '%s\n' "$out" | wc -l)" = 3 ] || return 0
    printf '%s\n' "$out" | grep -Eqx '[0-9a-f]{40}' || return 0
    [ "$(printf '%s\n' "$out" | grep -Ecx '[0-9a-f]{40}')" = 3 ] || return 0
    printf '%s' "$out" | tr '\n' '-'
}
write_marker() {
    local sha common dir mdir="${1:-sc-check-pass}"
    sha="$(git -C "$PROJECT" rev-parse HEAD 2>/dev/null)"; common="$(git -C "$PROJECT" rev-parse --git-common-dir 2>/dev/null)"
    [ -n "$sha" ] && [ -n "$common" ] || { say 'SC MARKER skipped (git rev-parse failed)'; return; }
    case "$common" in /*) ;; *) common="$PROJECT/$common" ;; esac
    dir="$common/$mdir"; mkdir -p "$dir" && date -Iseconds > "$dir/$sha"
    # Also key the marker on the Unity trees, so the guards accept a content-identical commit.
    local tkey; tkey="$(unity_tree_key HEAD)"
    [ -n "$tkey" ] && { mkdir -p "$common/$mdir-tree" && date -Iseconds > "$common/$mdir-tree/$tkey"; say "SC MARKER tree $tkey"; }
    if [ -n "$(git -C "$PROJECT" status --porcelain --untracked-files=no 2>/dev/null)" ]; then
        say "SC MARKER $sha (uncommitted changes were present; the marker covers the commit only)"
    else
        say "SC MARKER $sha"
    fi
}

# A fresh worktree holds LFS pointer stubs and the first Unity run fails on texture import: materialise them first.
if command -v git-lfs >/dev/null 2>&1 && git -C "$PROJECT" lfs ls-files >/dev/null 2>&1; then
    git -C "$PROJECT" lfs checkout >/dev/null 2>&1 && say 'SC LFS checkout done' || say 'SC LFS checkout failed (LFS files may be pointers)'
fi

code=1
case "$COMMAND" in
    compile) do_compile; code=$? ;;
    build) do_build; code=$? ;;
    test-edit) do_tests test-edit EditMode editmode.xml '!Soak'; code=$? ;;
    soak) do_tests soak EditMode soak.xml Soak; code=$? ;;   # only the [Category("Soak")] seed sweeps; no merge marker
    test-play) do_tests test-play PlayMode playmode.xml; code=$? ;;
    exec) do_exec; code=$? ;;
    gate)
        do_compile; code=$?
        [ "$code" = 0 ] && { do_tests test-edit EditMode editmode.xml '!Soak'; code=$?; }
        case "$code" in
            0) say 'SC RESULT gate PASS'; write_marker sc-gate-pass ;;
            2) say 'SC RESULT gate LOCKED' ;;
            3) say 'SC RESULT gate TIMEOUT' ;;
            *) say 'SC RESULT gate FAIL' ;;
        esac ;;
    check)
        do_build; code=$?
        [ "$code" = 0 ] && { do_tests test-edit EditMode editmode.xml '!Soak'; code=$?; }
        [ "$code" = 0 ] && { do_tests test-play PlayMode playmode.xml; code=$?; }
        case "$code" in
            0) say 'SC RESULT check PASS'; write_marker ;;
            2) say 'SC RESULT check LOCKED' ;;
            3) say 'SC RESULT check TIMEOUT' ;;
            *) say 'SC RESULT check FAIL' ;;
        esac ;;
esac
exit "$code"
