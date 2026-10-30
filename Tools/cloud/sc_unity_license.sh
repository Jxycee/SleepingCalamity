#!/usr/bin/env bash
# Tools/cloud/sc_unity_license.sh - put a Unity licence on a cloud container so Tools/cloud/unity.sh can launch the editor in batchmode.
# Licences come from the cloud environment's secrets (environment settings > Environment variables), which the setup script cannot read: the
# SessionStart hook runs `--file-only` and unity.sh runs it in full before each launch.
#
#   bash Tools/cloud/sc_unity_license.sh               install a licence if one is configured and none is present
#   bash Tools/cloud/sc_unity_license.sh --file-only   only write UNITY_LICENSE to disk (fast; never contacts Unity)
#   bash Tools/cloud/sc_unity_license.sh --return      give back this container's activation (do it before a session ends)
#
# Routes, checked in this order:
#   UNITY_EMAIL + UNITY_PASSWORD (+ UNITY_SERIAL for Plus/Pro)   the recommended route. The editor's own licensing client
#                   (Data/Resources/Licensing/Client/Unity.Licensing.Client) activates this container headlessly: --activate-ulf, falling back to
#                   --activate-all --include-personal. Works for Personal. The account must not use two-factor sign-in (the client can't answer
#                   it). Each container is a new machine to Unity, so --return before a session ends keeps the activation count down.
#   UNITY_LICENSE   the contents of a legacy Unity_lic.ulf (plain XML, or base64 of it), written to ~/.local/share/unity3d/Unity/Unity_lic.ulf.
#                   Current Hub versions don't make one (the PC has %LocalAppData%\Unity\licenses\UnityEntitlementLicense.xml instead, which is
#                   bound to the PC's hardware and won't work here), so this is for older setups.
# Exit codes: 0 licence present (or installed), 1 activation failed, 2 nothing configured (not an error for --file-only callers).
set -u
umask 077   # licence files are never readable by others
MODE="${1:-}"
ULF_DIR="$HOME/.local/share/unity3d/Unity"
LIC="$ULF_DIR/Unity_lic.ulf"
ENT_DIR="$HOME/.config/unity3d/Unity/licenses"   # where the licensing client keeps entitlement licences on Linux
UNITY="${SC_UNITY:-$HOME/unity/ed/Editor/Unity}"
ULC="$(dirname "$UNITY")/Data/Resources/Licensing/Client/Unity.Licensing.Client"
LOG="$HOME/.sc-setup/unity-license.log"
mkdir -p "$ULF_DIR" "$HOME/.sc-setup"

have_licence() { [ -s "$LIC" ] || ls "$ENT_DIR"/*.xml >/dev/null 2>&1; }
ulc() { # ulc ARGS...   runs the licensing client with the account's credentials; output to the log (the password never reaches it)
    timeout 180 "$ULC" "$@" --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD" >> "$LOG" 2>&1
}

if [ "$MODE" = --return ]; then
    [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ] || { echo "sc-license: --return needs UNITY_EMAIL and UNITY_PASSWORD"; exit 2; }
    [ -x "$ULC" ] || { echo "sc-license: licensing client not found at $ULC"; exit 1; }
    ulc --return-ulf; ulc --deactivate-all
    rm -f "$LIC" "$ENT_DIR"/*.xml
    echo "sc-license: returned this container's activation (log $LOG)"; exit 0
fi

if [ -n "${UNITY_LICENSE:-}" ]; then
    body="$UNITY_LICENSE"
    case "$body" in *"<"*) ;; *) body="$(printf '%s' "$body" | base64 -d 2>/dev/null)" || { echo "sc-license: UNITY_LICENSE is neither XML nor base64"; exit 1; } ;; esac
    case "$body" in *"<Entitlement"*|*"UnityEntitlementLicense"*)
        echo "sc-license: UNITY_LICENSE holds an entitlement licence (bound to the PC that made it); set UNITY_EMAIL + UNITY_PASSWORD instead" ;;
    *)
        if [ ! -f "$LIC" ] || [ "$(cat "$LIC")" != "$body" ]; then
            printf '%s' "$body" > "$LIC"
            echo "sc-license: wrote UNITY_LICENSE to $LIC"
        fi
        exit 0 ;;
    esac
fi
have_licence && exit 0
[ "$MODE" = --file-only ] && exit 2

if [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
    [ -x "$ULC" ] || { echo "sc-license: Unity editor not installed yet (bash Tools/cloud/setup.sh --wait)"; exit 1; }
    echo "sc-license: activating this container with the Unity account (log $LOG)"
    if [ -n "${UNITY_SERIAL:-}" ]; then ulc --activate-ulf --serial "$UNITY_SERIAL"; else ulc --activate-ulf; fi
    have_licence || ulc --activate-all --include-personal
    have_licence && { echo "sc-license: activated"; exit 0; }
    echo "sc-license: activation failed: $(grep -iE 'error|unauthor|invalid' "$LOG" | tail -1 | cut -c1-200) (check the email and password; two-factor sign-in on the account also blocks this)"
    exit 1
fi
echo "sc-license: no Unity licence configured (set UNITY_EMAIL + UNITY_PASSWORD in the environment's variables; see docs/superpowers/handoff/cloud-environment.md)"
exit 2
