#!/usr/bin/env bash
# PreToolUse guard for SleepingCalamity (Bash, Edit, Write, MultiEdit, NotebookEdit).
# Exit 2 blocks the tool call and shows stderr to Claude. Needs only bash, sed, awk, tr and git,
# so it runs in Git Bash on the Windows PC and on the Linux cloud VM (no jq, python or node).
set -f
input=$(cat)

json_field() {
    # JSON string value of key "$1" (unquoted keys only, so text inside other values can't match),
    # unescaped: \\ \" \n \t.
    printf '%s' "$input" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\(\([^\"\\\\]\|\\\\.\)*\)\".*/\1/p" \
        | head -n 1 \
        | sed -e 's/\\\\/\x01/g' -e 's/\\n/\n/g' -e 's/\\t/ /g' -e 's/\\"/"/g' -e 's/\x01/\\/g'
}

block() {
    echo "Blocked by .claude/hooks/guard.sh: $1" >&2
    exit 2
}

tool=$(json_field tool_name)
cwd=$(json_field cwd)
[ -n "$cwd" ] && cd "$cwd" 2>/dev/null

case "$tool" in
    Edit|Write|MultiEdit|NotebookEdit)
        path=$(json_field file_path)
        [ -z "$path" ] && path=$(json_field notebook_path)
        case "$path" in
            *.unity|*.prefab)
                block "don't edit Unity scene/prefab YAML by hand ($path). Every scene and prefab comes from a builder in Assets/_Project/Editor/Builders; change the builder and rebuild with Tools/unity.ps1 build." ;;
        esac
        exit 0 ;;
    Bash) ;;
    *) exit 0 ;;
esac

cmd=$(json_field command)
[ -z "$cmd" ] && exit 0

# Replace quoted strings with Q and drop heredoc bodies, so commit messages and file contents
# are never read as commands. Then split into one simple command per line.
segments=$(printf '%s\n' "$cmd" | awk '
    { s = s $0 "\n" }
    END {
        n = length(s); i = 1; out = ""; hd = ""; pend = ""
        while (i <= n) {
            c = substr(s, i, 1)
            if (hd != "") {
                j = index(substr(s, i), "\n"); if (j == 0) break
                line = substr(s, i, j - 1); sub(/^[ \t]+/, "", line); i += j
                if (line == hd) { hd = ""; out = out "\n" }
                continue
            }
            if (c == "\\") { out = out " "; i += 2; continue }
            if (c == "\047") { j = index(substr(s, i + 1), "\047"); i = (j == 0) ? n + 1 : i + j + 1; out = out " Q "; continue }
            if (c == "\"") {
                k = i + 1
                while (k <= n) { d = substr(s, k, 1); if (d == "\\") { k += 2; continue } if (d == "\"") break; k++ }
                i = k + 1; out = out " Q "; continue
            }
            if (substr(s, i, 2) == "<<") {
                rest = substr(s, i + 2)
                if (match(rest, /^-?[ \t]*["\047]?[A-Za-z_][A-Za-z0-9_]*["\047]?/)) {
                    w = substr(rest, RSTART, RLENGTH); gsub(/[-"\047 \t]/, "", w)
                    pend = w; i += 2 + RLENGTH; continue
                }
            }
            if (c == "\n" && pend != "") { hd = pend; pend = "" }
            out = out c; i++
        }
        print out
    }' | tr ';&|(){}' '\n\n\n\n\n\n\n' \
       | sed -E -e 's/^[[:space:]]+//' -e 's/[[:space:]]+$//' -e 's/^git[[:space:]]+-C[[:space:]]+[^[:space:]]+/git/')

branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
common=$(git rev-parse --git-common-dir 2>/dev/null)

while IFS= read -r seg; do
    [ -z "$seg" ] && continue

    # In-place rewrites of scene/prefab YAML.
    if printf '%s' "$seg" | grep -Eq '^(sed|perl)([[:space:]].*)?[[:space:]]-i' && printf '%s' "$seg" | grep -Eq '\.(unity|prefab)([[:space:]]|$)'; then
        block "don't rewrite .unity/.prefab files with sed/perl; change the generating builder instead."
    fi

    # Launching Unity directly instead of through the wrapper. Process lookups put -projectPath in quotes,
    # which are stripped above, so they don't match.
    if printf '%s' "$seg" | grep -Eq '(^|[[:space:]])-projectPath([[:space:]]|$)' \
       && printf '%s' "$cmd" | grep -Eiq 'unity' && ! printf '%s' "$seg" | grep -Eiq 'unity\.ps1'; then
        block "run Unity through Tools/unity.ps1 (PC) or Tools/cloud/unity.sh (cloud): they check the lock, add -dev-profile sctest, read the log and record the PASS marker."
    fi

    read -r -a words <<< "$seg"
    # Leading VAR=value words (env prefix): remember SC_GATE_SKIP=1, then drop them so words[0] is the command.
    gate_skip=0
    while [ "${#words[@]}" -gt 0 ] && [[ "${words[0]}" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]]; do
        [ "${words[0]}" = "SC_GATE_SKIP=1" ] && gate_skip=1
        words=("${words[@]:1}")
    done
    [ "${words[0]}" = "git" ] || continue
    sub="${words[1]}"
    args=("${words[@]:2}")

    case "$sub" in
        checkout|switch)
            # Track a branch change earlier in the same command line.
            last="${args[${#args[@]}-1]}"
            case " ${args[*]} " in *" -b "*|*" -c "*|*" -- "*) ;; *) [ -n "$last" ] && [ "${last#-}" = "$last" ] && branch="$last" ;; esac
            ;;
        push)
            for word in "${args[@]}"; do
                case "$word" in
                    --force|--force=*|--force-with-lease*|--force-if-includes|+*) block "never force-push (CLAUDE.md)." ;;
                    --*) ;;
                    -*f*) block "never force-push (CLAUDE.md)." ;;
                esac
            done
            # Fast gate: a branch push that changes Unity-imported files needs a passing 'unity gate' (or 'check')
            # for the pushed commit. Pushes to main follow a checked merge; deletes and tag pushes are exempt.
            [ "$gate_skip" = 1 ] && continue
            positional=(); skipgate=0
            for word in "${args[@]}"; do
                case "$word" in
                    --delete|-d|--tags|--mirror|--all) skipgate=1 ;;
                    -*) ;;
                    *) positional+=("$word") ;;
                esac
            done
            [ "$skipgate" = 1 ] && continue
            if [ "${#positional[@]}" -ge 2 ]; then refspec="${positional[${#positional[@]}-1]}"; else refspec=""; fi
            if [ -z "$refspec" ]; then src="HEAD"; dst="$branch"
            else
                refspec="${refspec#+}"
                case "$refspec" in *:*) src="${refspec%%:*}"; dst="${refspec#*:}" ;; *) src="$refspec"; dst="$refspec" ;; esac
                [ "$src" = "HEAD" ] && [ "$dst" = "HEAD" ] && dst="$branch"
            fi
            dst="${dst#refs/heads/}"
            [ -z "$src" ] && continue
            [ "$dst" = "main" ] && continue
            sha=$(git rev-parse --verify --quiet "$src^{commit}" 2>/dev/null) || continue
            unity=$(git diff --name-only "origin/main...$sha" -- Assets Packages ProjectSettings 2>/dev/null) || continue
            [ -z "$unity" ] && continue
            tree=$(git rev-parse --verify --quiet "$sha^{tree}" 2>/dev/null)
            ok=0
            for d in sc-gate-pass sc-check-pass; do
                [ -f "$common/$d/$sha" ] && ok=1
                [ -n "$tree" ] && [ -f "$common/$d/$tree" ] && ok=1
            done
            if [ "$ok" = 0 ]; then
                # A gated ancestor with no Unity-file changes since (docs/STATUS commits on top) still counts.
                for anc in $(git rev-list -n 30 "$sha" 2>/dev/null); do
                    for d in sc-gate-pass sc-check-pass; do
                        [ -f "$common/$d/$anc" ] || continue
                        if [ -z "$(git diff --name-only "$anc" "$sha" -- Assets Packages ProjectSettings 2>/dev/null)" ]; then ok=1; break; fi
                    done
                    [ "$ok" = 1 ] && break
                done
            fi
            [ "$ok" = 1 ] && continue
            block "branch push changes Assets/Packages/ProjectSettings but no fast gate passed for $sha. Run Tools/unity.ps1 gate (PC) or bash Tools/cloud/unity.sh gate (cloud) from this worktree, or prefix SC_GATE_SKIP=1 when no Unity licence is available (say so in the push report)."
            ;;
        merge)
            # Merge gate: merging into main needs a passing Tools/unity.ps1 check on each merged commit,
            # unless the merge changes nothing Unity imports (Assets/, Packages/, ProjectSettings/).
            [ "$branch" = "main" ] || continue
            skip=0
            for word in "${args[@]}"; do
                if [ "$skip" = 1 ]; then skip=0; continue; fi
                case "$word" in
                    --abort|--continue|--quit) continue 2 ;;
                    -m|--message|-F|--file|-s|--strategy|-X|--strategy-option) skip=1; continue ;;
                    -*|Q|main|HEAD|origin/main|FETCH_HEAD) continue ;;
                esac
                sha=$(git rev-parse --verify --quiet "$word^{commit}" 2>/dev/null) || continue
                [ -f "$common/sc-check-pass/$sha" ] && continue
                # Art-only or docs-only merge: no check needed. A failed diff (no merge base in a shallow clone) falls through.
                if unity=$(git diff --name-only "main...$sha" -- Assets Packages ProjectSettings 2>/dev/null) && [ -z "$unity" ]; then
                    continue
                fi
                block "no passing 'Tools/unity.ps1 check' recorded for $word ($sha), and it changes files under Assets/, Packages/ or ProjectSettings/. Check out that commit in C:\\Dev\\SleepingCalamity (not a worktree) and run Tools/unity.ps1 check, or in the cloud run bash Tools/cloud/unity.sh check (both write $common/sc-check-pass/$sha), then merge. If the user explicitly waives the check for this merge, create that marker file by hand and tell them."
            done
            ;;
    esac
done <<< "$segments"

exit 0
