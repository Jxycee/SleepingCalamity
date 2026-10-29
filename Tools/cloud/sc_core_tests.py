#!/usr/bin/env python3
"""Run SleepingCalamity's pure Core EditMode tests in the cloud, under .NET 8 + NUnitLite (no Unity).

SleepingCalamity.Core has noEngineReferences, so Core and the EditMode test files that only use Core, System and NUnit compile and run as a plain console
app. The script picks those files itself: a test file qualifies when its `using` lines name only System*, NUnit* and SleepingCalamity.Core. It never
launches Unity, and it doesn't cover tests that need UnityEngine, the editor, scenes or the other assemblies (those stay with the PC's check).

Run:  python3 Tools/cloud/sc_core_tests.py [--project <worktree>] [--where "test =~ WeirBoards"] [--list]
Prints NUnitLite's summary; exit 0 when every test passes. The build goes to ~/sc_core_tests (about 20 s cold, a few seconds warm).
Needs `dotnet` (8.x) and NuGet access for NUnit/NUnitLite 3.14.
"""
import argparse, os, re, subprocess, sys

ALLOWED = re.compile(r'^using\s+(static\s+)?(System(\.[\w.]+)?|NUnit(\.[\w.]+)?|SleepingCalamity\.Core)\s*;|^using\s+static\s+SleepingCalamity\.Tests\.EditMode\.\w+\s*;')
USING = re.compile(r'^using\s+(static\s+)?[\w.]+\s*;')


def pure_tests(root):
    out = []
    tests = os.path.join(root, 'Assets', '_Project', 'Tests', 'EditMode')
    for dirpath, _, files in os.walk(tests):
        for f in sorted(files):
            if not f.endswith('.cs'):
                continue
            path = os.path.join(dirpath, f)
            with open(path, encoding='utf-8-sig') as fh:
                lines = [l.strip() for l in fh]
            usings = [l for l in lines if USING.match(l) and '=' not in l]
            if usings and all(ALLOWED.match(l) for l in usings):   # test files and their Core-only helpers (FishingTestData)
                out.append(path)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', default=os.getcwd())
    ap.add_argument('--where', default=None, help='an NUnit --where filter')
    ap.add_argument('--list', action='store_true', help='list the test files picked and stop')
    a = ap.parse_args()
    root = os.path.abspath(a.project)
    files = pure_tests(root)
    if a.list:
        print('\n'.join(os.path.relpath(f, root) for f in files))
        return 0
    work = os.path.expanduser('~/sc_core_tests')
    os.makedirs(work, exist_ok=True)
    core = os.path.join(root, 'Assets', '_Project', 'Core')
    dropped = []
    # A file can pass the using check and still lean on a helper from a non-Core test file in the same namespace (FishingTestData): such files
    # fail to build here; each round drops the files the compiler names and builds again.
    for _ in range(6):
        b = build(work, core, files)
        if b.returncode == 0:
            break
        bad = set(m for m in re.findall(r'(/[^\s(]+\.cs)\(\d+,\d+\): error', b.stdout) if m in files)
        if not bad:
            print(b.stdout[-4000:], b.stderr[-2000:])
            print('FAIL (build)')
            return 1
        dropped += sorted(bad)
        files = [f for f in files if f not in bad]
    else:
        print('FAIL (build)')
        return 1
    if dropped:
        print('skipped (they need non-Core test helpers): ' + ', '.join(os.path.basename(f) for f in dropped))
    dll = os.path.join(work, 'bin', 'Release', 'net8.0', 'FgCoreTests.dll')
    args = ['dotnet', dll, '--noresult', '--noheader']
    if a.where:
        args += ['--where', a.where]
    r = subprocess.run(args, cwd=root, capture_output=True, text=True)   # cwd = the project: tests read Assets/... relative paths
    out = r.stdout
    fails = out[out.find('Errors, Failures and Warnings'):out.find('Run Settings')] if 'Errors, Failures' in out else ''
    summary = out[out.find('Test Run Summary'):] if 'Test Run Summary' in out else out[-2000:]
    print(fails.strip())
    print(summary.strip().split('\n  Start time')[0])
    print('%d test files; %s' % (len(files), 'PASS' if r.returncode == 0 else 'FAIL'))
    return 0 if r.returncode == 0 else 1


def build(work, core, files):
    items = '\n'.join('    <Compile Include="%s" />' % f for f in files)
    proj = f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><Nullable>disable</Nullable><LangVersion>latest</LangVersion>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0162;CS0414;CS0649;CS0169;CS8632</NoWarn></PropertyGroup>
  <ItemGroup>
    <Compile Include="{core}/**/*.cs" />
{items}
    <Compile Include="Program.cs" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnitLite" Version="3.14.0" />
  </ItemGroup>
</Project>
'''
    with open(os.path.join(work, 'FgCoreTests.csproj'), 'w') as fh:
        fh.write(proj)
    with open(os.path.join(work, 'Program.cs'), 'w') as fh:
        fh.write('public static class Program { public static int Main(string[] a) => new NUnitLite.AutoRun(typeof(Program).Assembly).Execute(a); }\n')
    return subprocess.run(['dotnet', 'build', '-c', 'Release', '-nologo', '-v', 'q', work], capture_output=True, text=True)


if __name__ == '__main__':
    sys.exit(main())
