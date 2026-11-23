# Regenerates all physics traces under C:/Dev/scratch/ptraces (web ground truth). Run from anywhere.
$W = 'C:/Dev/SleepingCalamity/.claude/worktrees/agent-a419013169f3039d6'
$T = "$W/Reference/sessions/aetherstrand-combo/tools/web_trace.ts"
$rec = @{ halfup059 = "$W/Reference/sessions/aetherstrand-combo/recipes/halfup059.json" }
foreach ($a in 'asym-cascade','spiky-hero','high-ponytail','sleek-bob','long-cascade','twin-tails') { $rec[$a] = "C:/Dev/scratch/batch/$a/recipe.json" }
Set-Location C:\Dev\scratch\AetherStrand
foreach ($k in $rec.Keys) { foreach ($m in 'idle','walk','turn','gust') { npx tsx $T $rec[$k] "C:/Dev/scratch/ptraces/$k/$m" $m 300 2>&1 | Select-String 'locks=' } }
