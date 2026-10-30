#!/usr/bin/env bash
# Tools/cloud/sc_unity_license.sh - put a Unity licence on a cloud container so Tools/cloud/unity.sh can launch the editor in batchmode.
# Licences come from the cloud environment's secrets (environment settings > Environment variables), which the setup script cannot read: the
# SessionStart hook runs `--file-only` and unity.sh runs it in full before each launch.
#
#   bash Tools/cloud/sc_unity_license.sh               install a licence if one is configured and none is present
#   bash Tools/cloud/sc_unity_license.sh --file-only   only write UNITY_LICENSE to disk (fast; never launches Unity)
#   bash Tools/cloud/sc_unity_license.sh --return      return a serial activation's seat (UNITY_SERIAL route only)
#
# Two routes, checked in this order:
#   UNITY_LICENSE   the contents of a Unity_lic.ulf (plain XML, or base64 of it). Personal licences: activate in Unity Hub on the PC and copy
#                   C:\ProgramData\Unity\Unity_lic.ulf. Written to ~/.local/share/unity3d/Unity/Unity_lic.ulf. The file is tied to the PC it came
#                   from; Unity usually accepts a Personal one elsewhere (the GameCI route) but may refuse it, and then the editor exits 198.
#   UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD   Plus/Pro serial: activates with the editor. Every new container takes a seat until returned,
#                   so return it (`--return`) before a session ends or free seats at id.unity.com.
# Exit codes: 0 licence present (or installed), 1 activation failed, 2 nothing configured (not an error for --file-only callers).
set -u
MODE="${1:-}"
LIC_DIR="$HOME/.local/share/unity3d/Unity"
LIC="$LIC_DIR/Unity_lic.ulf"
UNITY="${SC_UNITY:-$HOME/unity/ed/Editor/Unity}"
LOG="$HOME/.sc-setup/unity-license.log"
mkdir -p "$LIC_DIR" "$HOME/.sc-setup"

if [ "$MODE" = --return ]; then
    [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ] || { echo "sc-license: --return needs UNITY_EMAIL and UNITY_PASSWORD"; exit 2; }
    "$UNITY" -batchmode -nographics -quit -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -logFile "$LOG" >/dev/null 2>&1
    rc=$?; echo "sc-license: return exited $rc (log $LOG)"; exit $(( rc == 0 ? 0 : 1 ))
fi

if [ -n "${UNITY_LICENSE:-}" ]; then
    body="$UNITY_LICENSE"
    case "$body" in *"<"*) ;; *) body="$(printf '%s' "$body" | base64 -d 2>/dev/null)" || { echo "sc-license: UNITY_LICENSE is neither XML nor base64"; exit 1; } ;; esac
    if [ ! -f "$LIC" ] || [ "$(cat "$LIC")" != "$body" ]; then
        printf '%s' "$body" > "$LIC" && chmod 600 "$LIC"
        echo "sc-license: wrote UNITY_LICENSE to $LIC"
    fi
    exit 0
fi
[ -f "$LIC" ] && exit 0
[ "$MODE" = --file-only ] && exit 2

if [ -n "${UNITY_SERIAL:-}" ] && [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
    [ -x "$UNITY" ] || { echo "sc-license: Unity editor not installed yet (bash Tools/cloud/setup.sh --wait)"; exit 1; }
    echo "sc-license: activating serial (takes a seat until returned with --return)"
    "$UNITY" -batchmode -nographics -quit -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -logFile "$LOG" >/dev/null 2>&1
    [ -f "$LIC" ] && { echo "sc-license: activated"; exit 0; }
    echo "sc-license: activation failed (log $LOG)"; exit 1
fi
echo "sc-license: no Unity licence configured (set UNITY_LICENSE, or UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD, in the environment's variables)"
exit 2
