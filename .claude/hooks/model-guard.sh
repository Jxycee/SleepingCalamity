#!/usr/bin/env bash
# PreToolUse model guard for SleepingCalamity (Agent, Task, Workflow, start_session).
# Purpose: Fable is the most expensive model and the user can afford little of it, so work a
# session delegates must never silently run on Fable.
#   Every session: no `fork` subagents (a fork always runs on the caller's model and effort).
#   A Fable session additionally may not: pass model "fable" to a subagent, call the `fixer`
#   agent (pinned to Fable), run a Workflow (its agents can inherit the caller's model), or
#   start a session without an explicit non-Fable model (a new session inherits the caller's).
# Pinned agents (.claude/agents/*.md) and CLAUDE_CODE_SUBAGENT_MODEL=sonnet (settings.json)
# cover every other Agent call. The caller's model is read from its own transcript.
# Exit 2 blocks the call and shows stderr to Claude. Needs only bash, sed, grep and tail.
set -f
input=$(cat)

json_field() {
    # Top-level-style JSON string value of key "$1". Keys inside other string values are
    # escaped (\"key\"), so they can't match.
    printf '%s' "$input" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\(\([^\"\\\\]\|\\\\.\)*\)\".*/\1/p" | head -n 1
}

block() {
    echo "Blocked by .claude/hooks/model-guard.sh: $1" >&2
    exit 2
}

tool=$(json_field tool_name)
subagent=$(json_field subagent_type | tr '[:upper:]' '[:lower:]')
model=$(json_field model | tr '[:upper:]' '[:lower:]')

case "$tool" in
    Agent|Task)
        [ "$subagent" = "fork" ] && block "no fork subagents: a fork always runs on this session's model and effort. Use a pinned agent type from .claude/agents/ (sc-delegate)."
        ;;
esac

# Which model is this session running? Last assistant model id in its transcript.
transcript=$(json_field transcript_path | sed 's/\\\\/\//g')
caller=""
if [ -n "$transcript" ] && [ -f "$transcript" ]; then
    caller=$(tail -c 400000 "$transcript" 2>/dev/null | grep -o '"model":"claude-[a-z0-9.-]*"' | tail -n 1)
fi
case "$caller" in
    *fable*|*mythos*) ;;
    *) exit 0 ;;
esac

case "$tool" in
    Agent|Task)
        case "$model" in *fable*|*mythos*) block "this session runs on Fable; subagents must not. Drop the model override or use sonnet/opus/haiku." ;; esac
        [ "$subagent" = "fixer" ] && block "this session already runs on Fable; the fixer agent is pinned to Fable too. Use implementer (Sonnet) or implementer with model opus."
        ;;
    Workflow)
        block "this session runs on Fable; workflow agents can inherit it. Delegate with pinned agent types instead."
        ;;
    mcp__ccd_session__start_session)
        [ -z "$model" ] && block "this session runs on Fable and a new session would inherit it. Pass model: \"claude-sonnet-5-5\" (or claude-opus-5-5) and effort explicitly."
        case "$model" in *fable*|*mythos*) block "a Fable session may not start another Fable session; ask the user." ;; esac
        ;;
esac
exit 0
