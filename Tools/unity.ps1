# Tools/unity.ps1 - SleepingCalamity wrapper for Unity batchmode runs (M1 interface contract section 11).
# ASCII only. Windows PowerShell 5.1 compatible.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\Tools\unity.ps1 <command> [-OutDir <abs dir>] [-Filter <regex>] [-TimeoutMinutes <int>] [-ProjectRoot <abs dir>] [-Method <Class.Method>]
#   <command> = compile | build | test-edit | test-play | check | gate | exec
#   gate = compile + EditMode tests (no PlayMode, no port lock); records a fast-gate marker (sc-gate-pass) that the branch-push guard accepts. check stays the merge gate.
#
# OutDir defaults to $env:SC_OUT. It must be absolute, must not contain '..', and must be outside the repo.
# ProjectRoot defaults to the checkout this script lives in (Tools\..), so <worktree>\Tools\unity.ps1 tests that worktree;
# pass -ProjectRoot to test another one. The check PASS marker lands in the shared git common dir, so the merge guard sees
# it either way. (Until 2026-11-01 it defaulted to C:\Dev\SleepingCalamity, and worktree runs silently tested main's checkout.)
# PlayMode runs (test-play, and check's PlayMode stage) take a machine-wide lock first: NGO tests share UDP port 7790, so two
# at once fail with 'address already in use'. The lock is an open file handle, so the OS frees it if this script dies; the
# wait also covers PlayMode runs started without the lock (an older copy of this script).
# TimeoutMinutes is per stage (default 30). On a timeout the wrapper ends the Unity process it started itself (only that
# one), so a hung run can't hold the port for hours. Never end any other Unity process (the editor, another session's run).
# exec runs -executeMethod <Method> (e.g. SleepingCalamity.EditorTools.FishIconBaker.Bake) and passes on its exit code.
# -Graphics drops -nographics (renders such as Bake Fish Icons need a graphics device).
# Exit codes: 0 PASS, 1 FAIL, 2 LOCKED, 3 TIMEOUT, 4 usage error.
# The command is checked by hand instead of with ValidateSet, because a ValidateSet failure exits 1, not 4.
param(
    [Parameter(Position = 0)]
    [string]$Command = '',

    [string]$OutDir = $env:SC_OUT,

    [string]$Filter = '',

    [int]$TimeoutMinutes = 30,

    [string]$ProjectRoot = '',

    [string]$Method = '',

    [switch]$Graphics
)

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
$UnityExe    = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
$PortLockPath = Join-Path ([IO.Path]::GetTempPath()) 'sc-unity-playmode.lock'
$PortWaitMinutes = 120
$LockFile    = Join-Path $ProjectRoot 'Temp\UnityLockfile'
$ErrPattern  = 'error CS|Exception|Scripts have compiler errors|Compilation failed'
$Commands    = @('compile', 'build', 'test-edit', 'test-play', 'check', 'gate', 'exec')
$UsageLine   = 'usage: unity.ps1 <compile|build|test-edit|test-play|check|gate|exec> -OutDir <absolute scratch dir outside the repo> (or set SC_OUT) [-ProjectRoot <dir>] [-Method <Class.Method>]'

function Say([string]$Text) {
    [Console]::Out.WriteLine($Text)
}

function Exit-Usage([string]$Detail) {
    Say $UsageLine
    if (-not [string]::IsNullOrEmpty($Detail)) { Say ('usage: ' + $Detail) }
    exit 4
}

function Get-LogText([string]$Log) {
    if (-not (Test-Path -LiteralPath $Log)) { return '' }
    $raw = Get-Content -LiteralPath $Log -Raw -Encoding UTF8
    if ($null -eq $raw) { return '' }
    return [string]$raw
}

function Test-UnityLocked {
    if (-not (Test-Path -LiteralPath $LockFile)) { return $false }
    try {
        $stream = [IO.File]::Open($LockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
        return $false
    }
    catch {
        return $true
    }
}

function Show-LogExcerpt([string]$Log, [string]$Pattern) {
    if (-not (Test-Path -LiteralPath $Log)) {
        Say ('SC LOG no log file at ' + $Log)
        return
    }
    $shown = 0
    foreach ($line in (Get-Content -LiteralPath $Log -Encoding UTF8)) {
        if ($line -match $Pattern) {
            Say ('SC LOG ' + $line)
            $shown++
            if ($shown -ge 40) { break }
        }
    }
}

function Test-CompileClean([string]$Log) {
    $text = Get-LogText $Log
    return (($text -match 'Exiting batchmode successfully') -and ($text -notmatch 'error CS'))
}

# Another Unity PlayMode run on this machine (any checkout), not counting $Except.
function Test-OtherPlayMode([int]$Except) {
    $procs = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue
    foreach ($p in $procs) {
        if ($p.ProcessId -eq $Except) { continue }
        if ([string]$p.CommandLine -match '-testPlatform PlayMode') { return $true }
    }
    return $false
}

# Takes the machine-wide PlayMode lock (an exclusively opened file) and waits until no other PlayMode run is alive.
# Returns the open stream (close it to release) or $null after $PortWaitMinutes.
function Get-PortLock {
    $deadline = (Get-Date).AddMinutes($PortWaitMinutes)
    $nextNote = Get-Date
    while ((Get-Date) -lt $deadline) {
        $stream = $null
        try { $stream = [IO.File]::Open($PortLockPath, 'OpenOrCreate', 'ReadWrite', 'None') } catch { $stream = $null }
        if ($null -ne $stream) {
            if (-not (Test-OtherPlayMode 0)) {
                $bytes = [Text.Encoding]::ASCII.GetBytes(('pid ' + $PID + ' ' + $ProjectRoot + ' ' + (Get-Date -Format o)))
                $stream.SetLength(0); $stream.Write($bytes, 0, $bytes.Length); $stream.Flush()
                return $stream
            }
            $stream.Close()
        }
        if ((Get-Date) -ge $nextNote) {
            Say 'SC WAIT PlayMode port busy (another PlayMode run holds it); waiting'
            $nextNote = (Get-Date).AddMinutes(5)
        }
        Start-Sleep -Seconds 20
    }
    return $null
}

function Invoke-UnityRun([string]$Name, [string]$Extra, [switch]$NeedsPort) {
    $log = Join-Path $OutDir ($Name + '.log')
    if (Test-UnityLocked) {
        return [pscustomobject]@{ Status = 'LOCKED'; ProcId = 0; ExitCode = -1; Log = $log }
    }
    $portLock = $null
    if ($NeedsPort) {
        $portLock = Get-PortLock
        if ($null -eq $portLock) {
            Say ('SC LOG PlayMode port still busy after ' + $PortWaitMinutes + ' min')
            return [pscustomobject]@{ Status = 'TIMEOUT'; ProcId = 0; ExitCode = -1; Log = $log }
        }
    }
    try {
        return (Invoke-UnityProcess $log $Extra)
    }
    finally {
        if ($null -ne $portLock) { $portLock.Close() }
    }
}

function Invoke-UnityProcess([string]$log, [string]$Extra) {
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }
    $noGfx = '-nographics '
    if ($Graphics) { $noGfx = '' }
    $unityArgs = '-batchmode ' + $noGfx + '-projectPath "' + $ProjectRoot + '" -logFile "' + $log + '" ' + $Extra
    try {
        $proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -NoNewWindow -ErrorAction Stop
    }
    catch {
        Say ('SC LOG could not start Unity: ' + $_.Exception.Message)
        return [pscustomobject]@{ Status = 'DONE'; ProcId = 0; ExitCode = -1; Log = $log }
    }
    $null = $proc.Handle
    $finished = $proc.WaitForExit($TimeoutMinutes * 60 * 1000)
    if (-not $finished) {
        # Our own child only: a hung run otherwise keeps the PlayMode port (and its project's Library) for hours.
        try { $proc.Kill(); $null = $proc.WaitForExit(30000); Say ('SC LOG ended the timed-out Unity run pid=' + $proc.Id) }
        catch { Say ('SC LOG could not end the timed-out Unity run pid=' + $proc.Id + ': ' + $_.Exception.Message) }
        return [pscustomobject]@{ Status = 'TIMEOUT'; ProcId = $proc.Id; ExitCode = -1; Log = $log }
    }
    return [pscustomobject]@{ Status = 'DONE'; ProcId = $proc.Id; ExitCode = $proc.ExitCode; Log = $log }
}

function Invoke-Compile {
    $run = Invoke-UnityRun 'compile' '-quit'
    switch ($run.Status) {
        'LOCKED'  { Say 'SC RESULT compile LOCKED (close the Unity editor, then rerun)'; return 2 }
        'TIMEOUT' { Say ('SC RESULT compile TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }
    if (($run.ExitCode -eq 0) -and (Test-CompileClean $run.Log)) {
        Say ('SC RESULT compile PASS log=' + $run.Log)
        return 0
    }
    Show-LogExcerpt $run.Log $ErrPattern
    Say ('SC RESULT compile FAIL log=' + $run.Log)
    return 1
}

function Invoke-Build {
    $run = Invoke-UnityRun 'build' '-quit -executeMethod SleepingCalamity.EditorTools.ProjectBuilder.BuildAll'
    switch ($run.Status) {
        'LOCKED'  { Say 'SC RESULT build LOCKED (close the Unity editor, then rerun)'; return 2 }
        'TIMEOUT' { Say ('SC RESULT build TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }
    $text = Get-LogText $run.Log
    if ($text -match 'SC BUILDALL FAIL') {
        foreach ($line in (Get-Content -LiteralPath $run.Log -Encoding UTF8)) {
            if ($line -match '^SC BUILDALL ERROR ') { Say $line }
        }
        Show-LogExcerpt $run.Log $ErrPattern
        Say ('SC RESULT build FAIL log=' + $run.Log)
        return 1
    }
    if (($run.ExitCode -eq 0) -and (Test-CompileClean $run.Log) -and ($text -match 'SC BUILDALL PASS')) {
        Say ('SC RESULT build PASS log=' + $run.Log)
        return 0
    }
    Show-LogExcerpt $run.Log $ErrPattern
    Say ('SC RESULT build FAIL log=' + $run.Log)
    return 1
}

function Invoke-Exec {
    $run = Invoke-UnityRun 'exec' ('-quit -executeMethod ' + $Method)
    switch ($run.Status) {
        'LOCKED'  { Say 'SC RESULT exec LOCKED (close the Unity editor, then rerun)'; return 2 }
        'TIMEOUT' { Say ('SC RESULT exec TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }
    if (($run.ExitCode -eq 0) -and (Test-CompileClean $run.Log)) {
        Say ('SC RESULT exec PASS method=' + $Method + ' log=' + $run.Log)
        return 0
    }
    Show-LogExcerpt $run.Log $ErrPattern
    Say ('SC RESULT exec FAIL method=' + $Method + ' exit=' + $run.ExitCode + ' log=' + $run.Log)
    return 1
}

function Invoke-Tests([string]$Name, [string]$Platform, [string]$XmlName) {
    $xml = Join-Path $OutDir $XmlName
    if (Test-Path -LiteralPath $xml) { Remove-Item -LiteralPath $xml -Force }
    # Never pass -quit with -runTests.
    $extra = '-runTests -testPlatform ' + $Platform + ' -testResults "' + $xml + '" -dev-profile sctest'
    if (-not [string]::IsNullOrEmpty($Filter)) { $extra = $extra + ' -testFilter "' + $Filter + '"' }
    $run = Invoke-UnityRun $Name $extra -NeedsPort:($Platform -eq 'PlayMode')
    switch ($run.Status) {
        'LOCKED'  { Say ('SC RESULT ' + $Name + ' LOCKED (close the Unity editor, then rerun)'); return 2 }
        'TIMEOUT' { Say ('SC RESULT ' + $Name + ' TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }

    $total = 0; $passed = 0; $failed = 0; $skipped = 0
    # One 'SC FAIL <fullname> :: <first 200 chars>' line per failed test, printed after this run's SC RESULT line.
    $failLines = New-Object System.Collections.Generic.List[string]
    if (Test-Path -LiteralPath $xml) {
        try {
            [xml]$doc = Get-Content -LiteralPath $xml -Raw -Encoding UTF8
            $runNode = $doc.SelectSingleNode('/test-run')
            if ($null -ne $runNode) {
                $total = [int]$runNode.GetAttribute('total')
                $passed = [int]$runNode.GetAttribute('passed')
                $failed = [int]$runNode.GetAttribute('failed')
                $skipped = [int]$runNode.GetAttribute('skipped')
                foreach ($case in $doc.SelectNodes('//test-case[@result="Failed"]')) {
                    $message = ''
                    $messageNode = $case.SelectSingleNode('failure/message')
                    if ($null -ne $messageNode) { $message = [string]$messageNode.InnerText }
                    $flat = ($message.Trim() -replace '\s+', ' ')
                    if ($flat.Length -gt 200) { $flat = $flat.Substring(0, 200) }
                    $failLines.Add('SC FAIL ' + $case.GetAttribute('fullname') + ' :: ' + $flat)
                }
            }
        }
        catch {
            Say ('SC LOG could not parse ' + $xml)
        }
    }

    $logText = Get-LogText $run.Log
    $completed = $logText -match 'Test run completed\. Exiting with code 0'
    $completedAny = $logText -match 'Test run completed\. Exiting with code'
    if (($total -gt 0) -and ($failed -eq 0) -and $completed) {
        Say ('SC RESULT ' + $Name + ' PASS total=' + $total + ' passed=' + $passed + ' failed=0 skipped=' + $skipped + ' log=' + $run.Log)
        return 0
    }
    if ($total -eq 0) {
        Show-LogExcerpt $run.Log $ErrPattern
        Say ('SC RESULT ' + $Name + ' FAIL total=0 (no tests matched or compile errors) log=' + $run.Log)
        return 1
    }
    $reason = ''
    if (-not $completedAny) { $reason = ' (no completion line in log)' }
    Say ('SC RESULT ' + $Name + ' FAIL total=' + $total + ' passed=' + $passed + ' failed=' + $failed + ' skipped=' + $skipped + ' log=' + $run.Log + $reason)
    foreach ($line in $failLines) { Say $line }
    return 1
}

# ---- entry point ----
if ($Commands -notcontains $Command) {
    Exit-Usage ("unknown or missing command '" + $Command + "'")
}
if ((-not [IO.Path]::IsPathRooted($ProjectRoot)) -or (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'ProjectSettings')))) {
    Exit-Usage ('ProjectRoot must be an absolute Unity project path: ' + $ProjectRoot)
}
if (($Command -eq 'exec') -and ($Method -notmatch '^[A-Za-z_][A-Za-z0-9_.]*$')) {
    Exit-Usage 'exec needs -Method <Namespace.Class.Method>'
}
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
$LockFile    = Join-Path $ProjectRoot 'Temp\UnityLockfile'
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    Exit-Usage 'no -OutDir given and SC_OUT is not set'
}
if ((-not [IO.Path]::IsPathRooted($OutDir)) -or $OutDir.Contains('..')) {
    Exit-Usage ('OutDir must be absolute and must not contain ..: ' + $OutDir)
}
$outFull  = [IO.Path]::GetFullPath($OutDir).TrimEnd('\')
$rootFull = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
if ($outFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or $outFull.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
    Exit-Usage ('OutDir must be outside the repo: ' + $OutDir)
}
if (-not (Test-Path -LiteralPath $UnityExe)) {
    Exit-Usage ('Unity editor not found at ' + $UnityExe)
}
$OutDir = $outFull
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# A fresh worktree holds LFS pointer stubs and the first Unity run fails on texture import: materialise them first.
try {
    & git -C $ProjectRoot lfs checkout *> $null
    if ($LASTEXITCODE -eq 0) { Say 'SC LFS checkout done' } else { Say 'SC LFS checkout failed (LFS files may be pointers)' }
} catch { Say 'SC LFS checkout skipped (git lfs unavailable)' }

$code = 1
# After a check PASS, record the tested commit for the merge gate (.claude/hooks/guard.sh).
function Write-PassMarker([string]$MarkerDir = 'sc-check-pass') {
    try {
        $sha = & git -C $ProjectRoot rev-parse HEAD 2>$null
        $common = & git -C $ProjectRoot rev-parse --git-common-dir 2>$null
        if ([string]::IsNullOrEmpty($sha) -or [string]::IsNullOrEmpty($common)) {
            Say 'SC MARKER skipped (git rev-parse failed)'
            return
        }
        $sha = ([string]$sha).Trim()
        $common = ([string]$common).Trim()
        if (-not [IO.Path]::IsPathRooted($common)) { $common = Join-Path $ProjectRoot $common }
        $dir = Join-Path $common $MarkerDir
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Set-Content -LiteralPath (Join-Path $dir $sha) -Value (Get-Date -Format o) -Encoding ASCII
        # Also key the marker on the tree under Assets/ Packages/ ProjectSettings/ (the merge guard accepts a content-identical commit).
        $tkey = (& git -C $ProjectRoot ls-tree HEAD -- Assets Packages ProjectSettings 2>$null | & git -C $ProjectRoot hash-object --stdin 2>$null)
        if (-not [string]::IsNullOrEmpty($tkey)) {
            $tdir = Join-Path $common 'sc-check-pass-tree'
            New-Item -ItemType Directory -Force -Path $tdir | Out-Null
            Set-Content -LiteralPath (Join-Path $tdir ([string]$tkey).Trim()) -Value (Get-Date -Format o) -Encoding ASCII
            Say ('SC MARKER tree ' + ([string]$tkey).Trim())
        }
        $dirty = & git -C $ProjectRoot status --porcelain --untracked-files=no 2>$null
        if ($dirty) { Say ('SC MARKER ' + $sha + ' (uncommitted changes were present; the marker covers the commit only)') }
        else { Say ('SC MARKER ' + $sha) }
    }
    catch {
        Say ('SC MARKER failed: ' + $_.Exception.Message)
    }
}

switch ($Command) {
    'compile'   { $code = Invoke-Compile }
    'build'     { $code = Invoke-Build }
    'test-edit' { $code = Invoke-Tests 'test-edit' 'EditMode' 'editmode.xml' }
    'test-play' { $code = Invoke-Tests 'test-play' 'PlayMode' 'playmode.xml' }
    'exec'      { $code = Invoke-Exec }
    'gate' {
        $code = Invoke-Compile
        if ($code -eq 0) { $code = Invoke-Tests 'test-edit' 'EditMode' 'editmode.xml' }
        switch ($code) {
            0       { Say 'SC RESULT gate PASS'; Write-PassMarker 'sc-gate-pass' }
            2       { Say 'SC RESULT gate LOCKED (close the Unity editor, then rerun)' }
            3       { Say 'SC RESULT gate TIMEOUT' }
            default { Say 'SC RESULT gate FAIL' }
        }
    }
    'check' {
        $code = Invoke-Build
        if ($code -eq 0) { $code = Invoke-Tests 'test-edit' 'EditMode' 'editmode.xml' }
        if ($code -eq 0) { $code = Invoke-Tests 'test-play' 'PlayMode' 'playmode.xml' }
        switch ($code) {
            0       { Say 'SC RESULT check PASS'; Write-PassMarker }
            2       { Say 'SC RESULT check LOCKED (close the Unity editor, then rerun)' }
            3       { Say 'SC RESULT check TIMEOUT' }
            default { Say 'SC RESULT check FAIL' }
        }
    }
}
exit $code
