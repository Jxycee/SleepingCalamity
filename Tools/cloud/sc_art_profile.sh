#!/usr/bin/env bash
# Tools/cloud/sc_art_profile.sh on|off|status - the cloud art profile: lets the live `blender` MCP server load in this checkout by dropping blender
# from .claude/settings.json's deniedMcpServers (CLAUDE.md: Blender stays denied except in art sessions; never commit the edit).
# skip-worktree keeps the edit out of `git add`/`status`, but git then refuses merges, pulls and checkouts that touch the file: run `off` before
# them and `on` after (sc-merge does). setup.sh --profile art records ~/.sc-setup/profile=art and the SessionStart hook re-applies `on`.
set -u
REPO="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/../.." && pwd)"
F=.claude/settings.json
cd "$REPO" || exit 1
case "${1:-status}" in
    on)
        git ls-files -v "$F" | grep -q '^S' && { echo "art profile: already on"; exit 0; }
        python3 - "$F" <<'PY' || exit 1
import json, sys
p = sys.argv[1]
d = json.load(open(p))
d["deniedMcpServers"] = [e for e in d.get("deniedMcpServers", []) if e.get("serverName", "").lower() != "blender"]
with open(p, "w") as f:
    json.dump(d, f, indent=2)
    f.write("\n")
PY
        git update-index --skip-worktree "$F" && echo "art profile: on (blender MCP allowed in this checkout; restart the session to load it)" ;;
    off)
        git update-index --no-skip-worktree "$F" && git checkout -- "$F" && echo "art profile: off (settings.json back to the committed version)" ;;
    status)
        git ls-files -v "$F" | grep -q '^S' && echo on || echo off ;;
    *) echo "usage: sc_art_profile.sh on|off|status" >&2; exit 4 ;;
esac
