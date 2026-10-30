#!/usr/bin/env python3
"""Cloud C# compile check for SleepingCalamity: compiles the project's assemblies the way the Unity editor would, on Linux, without running Unity.

The Unity Linux editor download (no licence or sign-in needed to fetch it) ships everything a compile needs: the UnityEngine/UnityEditor
reference DLLs, the built-in packages and the framework reference assemblies. Unity itself is never launched (the project's guard hook
blocks that outside Tools/unity.ps1, and it would need a licence and a full import). This script resolves every
.asmdef the project needs (Assets/ plus the packages in Packages/packages-lock.json), works out each one's defines, references and
sources like the editor does, and compiles them in dependency order with Roslyn (csc). Package assemblies are cached by content.

It catches what a cloud session can get wrong in C#: syntax, types, missing members, wrong assembly references (asmdef edges),
#if UNITY_EDITOR mistakes. It does not run tests, IL post-processing (Netcode RPCs), shaders or anything that needs the editor:
a licensed `unity.ps1 check` (PC) or `Tools/cloud/unity.sh check` (cloud) stays the merge gate for Assets/ changes.

Setup (once per container; see docs/superpowers/handoff/cloud-environment.md):
    python3 Tools/cloud/sc_cloud_compile.py --setup        # downloads the editor (4.2 GB) and the registry packages to ~/unity
Usage:
    python3 Tools/cloud/sc_cloud_compile.py [--project PATH] [--only NAME[,NAME]] [--warnings] [--csproj]
    --csproj also writes <Assembly>.csproj + SleepingCalamity.sln at the project root for the csharp-lsp plugin (what Tools/sync-csproj.ps1
    gives a PC worktree); the SessionStart hook runs it in cloud sessions. Gitignored: never commit them.
Exit codes: 0 every project assembly compiled, 1 compile errors, 2 setup missing or usage.
"""
import argparse
import glob
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys

UNITY_VERSION = "6000.6.5f1"
UNITY_REVISION = "3ff58d469c8a"
EDITOR_URL = f"https://download.unity3d.com/download_unity/{UNITY_REVISION}/LinuxEditorInstaller/Unity-{UNITY_VERSION}.tar.xz"
HOME = os.path.expanduser("~/unity")
EDITOR = os.path.join(HOME, "ed", "Editor")
PKGS = os.path.join(HOME, "pkgs")
CACHE = os.path.join(HOME, "cache")
LANG_VERSION = "9.0"
# References the editor resolves to assemblies under another name (Unity 6 folds ugui into these).
ALIASES = {"Unity.ugui": ["UnityEngine.UI", "UnityEditor.UI"]}


def die(msg, code=2):
    print(msg, file=sys.stderr)
    sys.exit(code)


# ---------- setup ----------

def setup(project):
    os.makedirs(HOME, exist_ok=True)
    tarball = os.path.join(HOME, f"Unity-{UNITY_VERSION}.tar.xz")
    if not os.path.isdir(EDITOR):
        if not os.path.exists(tarball):
            print(f"downloading {EDITOR_URL}")
            subprocess.run(["curl", "-fL", "--retry", "4", "-o", tarball + ".part", EDITOR_URL], check=True)
            os.replace(tarball + ".part", tarball)
        print("extracting the editor (about 10 minutes)")
        os.makedirs(os.path.join(HOME, "ed"), exist_ok=True)
        subprocess.run(["tar", "-xJf", tarball, "-C", os.path.join(HOME, "ed"), "--exclude=*/Documentation/*"], check=True)
        os.remove(tarball)  # 4.2 GB the sessions' disk allowance needs back
    lock = json.load(open(os.path.join(project, "Packages", "packages-lock.json")))["dependencies"]
    for name, info in lock.items():
        if info.get("source") != "registry":
            continue
        dest = os.path.join(PKGS, name)
        stamp = os.path.join(dest, ".version")
        if os.path.exists(stamp) and open(stamp).read() == info["version"]:
            continue
        url = info.get("url", "https://packages.unity.com")
        meta = json.loads(subprocess.run(["curl", "-sSf", f"{url}/{name}"], capture_output=True, text=True, check=True).stdout)
        tar = meta["versions"][info["version"]]["dist"]["tarball"]
        shutil.rmtree(dest, ignore_errors=True)
        os.makedirs(dest)
        subprocess.run(f"curl -sSfL '{tar}' | tar -xz -C '{dest}'", shell=True, check=True)
        open(stamp, "w").write(info["version"])
        print(f"package {name} {info['version']}")
    print("setup done")


# ---------- package and asmdef discovery ----------

def package_roots(project):
    """name -> (root dir, version) for every package in the lock file that is on disk."""
    lock = json.load(open(os.path.join(project, "Packages", "packages-lock.json")))["dependencies"]
    builtin = os.path.join(EDITOR, "Data", "Resources", "PackageManager", "BuiltInPackages")
    roots = {}
    for name, info in lock.items():
        src = info.get("source")
        if src == "registry":
            root = os.path.join(PKGS, name, "package")
        elif src == "builtin":
            root = os.path.join(builtin, name)
        elif src == "embedded":
            root = os.path.join(project, "Packages", name)
        else:
            root = os.path.join(PKGS, name, "package")  # git packages: fetched by hand if ever needed
        if os.path.isdir(root):
            version = info.get("version", "0")
            pj = os.path.join(root, "package.json")
            if os.path.exists(pj):
                version = json.load(open(pj)).get("version", version)
            roots[name] = (root, version)
    return roots


def read_json(path):
    text = open(path, encoding="utf-8-sig").read()
    text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    return json.loads(text)


def meta_guid(path):
    try:
        m = re.search(r"^guid:\s*(\w+)", open(path + ".meta").read(), re.M)
        return m.group(1) if m else None
    except OSError:
        return None


class Asm:
    def __init__(self, path, package):
        self.path = path
        self.dir = os.path.dirname(path)
        self.package = package  # None for Assets/
        self.data = read_json(path)
        self.name = self.data["name"]
        self.guid = meta_guid(path)
        self.extra_dirs = []  # from .asmref
        self.sources = []

    @property
    def editor_only(self):
        return self.data.get("includePlatforms") == ["Editor"]


def discover(project, roots):
    asms, by_guid = {}, {}
    trees = [(os.path.join(project, "Assets"), None)] + [(r, n) for n, (r, _) in roots.items()]
    asmrefs = []
    for top, pkg in trees:
        for p in glob.glob(os.path.join(top, "**", "*.asmdef"), recursive=True):
            if "/Samples~/" in p or "/Tests~/" in p or "/Documentation~/" in p:
                continue
            a = Asm(p, pkg)
            if a.name in asms and asms[a.name].package is None:
                continue  # the project's own definition wins
            asms[a.name] = a
            if a.guid:
                by_guid[a.guid] = a
        asmrefs += glob.glob(os.path.join(top, "**", "*.asmref"), recursive=True)
    for p in asmrefs:
        ref = read_json(p).get("reference", "")
        target = by_guid.get(ref[5:]) if ref.startswith("GUID:") else asms.get(ref)
        if target:
            target.extra_dirs.append(os.path.dirname(p))
    # Sources: every .cs under the asmdef's folder, minus folders owned by another asmdef/asmref, minus hidden (~ or .) folders.
    owners = {a.dir: a for a in asms.values()}
    for a in asms.values():
        for d in a.extra_dirs:
            owners[d] = a
    for d, a in owners.items():
        for root, dirs, files in os.walk(d):
            dirs[:] = [x for x in dirs if not x.endswith("~") and not x.startswith(".") and os.path.join(root, x) not in owners]
            a.sources += [os.path.join(root, f) for f in files if f.endswith(".cs")]
    return asms, by_guid


# ---------- defines ----------

def unity_defines():
    major, minor, patch = 6000, 6, 5
    d = {f"UNITY_{major}", f"UNITY_{major}_{minor}", f"UNITY_{major}_{minor}_{patch}"}
    for v in ["5_3", "5_4", "5_5", "5_6", "2017_1", "2017_2", "2017_3", "2017_4", "2018_1", "2018_2", "2018_3", "2018_4", "2019_1",
              "2019_2", "2019_3", "2019_4", "2020_1", "2020_2", "2020_3", "2021_1", "2021_2", "2021_3", "2022_1", "2022_2", "2022_3",
              "2023_1", "2023_2", "2023_3"] + [f"6000_{i}" for i in range(minor + 1)]:
        d.add(f"UNITY_{v}_OR_NEWER")
    d |= {"UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_WIN", "UNITY_STANDALONE_WIN", "UNITY_STANDALONE", "PLATFORM_STANDALONE_WIN",
          "PLATFORM_STANDALONE", "UNITY_64", "ENABLE_MONO", "ENABLE_INPUT_SYSTEM", "UNITY_ASSERTIONS", "DEBUG", "TRACE",
          "UNITY_INCLUDE_TESTS", "CSHARP_7_3_OR_NEWER", "CSHARP_7_OR_LATER", "ENABLE_UNITY_COLLECTIONS_CHECKS", "ENABLE_BURST_AOT",
          "UNITY_PRO_LICENSE", "ENABLE_PROFILER", "ENABLE_CLOUD_SERVICES", "UNITY_TEAM_LICENSE", "UNITY_PHYSICS_FUNCTIONALITY"}
    return d


def parse_version(v):
    m = re.match(r"(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:[-a-z]*\.?(\d+))?", v.strip())
    if not m:
        return (0, 0, 0)
    return tuple(int(x) if x else 0 for x in m.groups()[:3])


def in_range(version, expr):
    """Unity versionDefines expression: "1.2.0" (>=), "[1.0,2.0)" style ranges."""
    v = parse_version(version)
    expr = expr.strip()
    if not expr:
        return True
    if expr[0] not in "[(":
        return v >= parse_version(expr)
    lo_inc, hi_inc = expr[0] == "[", expr[-1] == "]"
    parts = expr[1:-1].split(",")
    if len(parts) == 1:
        return v == parse_version(parts[0])
    lo, hi = parts[0].strip(), parts[1].strip()
    if lo and (v < parse_version(lo) or (v == parse_version(lo) and not lo_inc)):
        return False
    if hi and (v > parse_version(hi) or (v == parse_version(hi) and not hi_inc)):
        return False
    return True


def asm_defines(a, base, versions):
    d = set(base)
    for vd in a.data.get("versionDefines", []):
        name = vd.get("name", "")
        ver = UNITY_VERSION if name == "Unity" else versions.get(name)
        if ver is not None and in_range(ver, vd.get("expression", "")):
            d.add(vd["define"])
    return d


def constraints_ok(a, defines):
    for c in a.data.get("defineConstraints", []):
        for alt in c.split("||"):
            alt = alt.strip()
            if (alt.startswith("!") and alt[1:] not in defines) or (not alt.startswith("!") and alt in defines):
                break
        else:
            return False
    return True


def platform_ok(a):
    inc, exc = a.data.get("includePlatforms", []), a.data.get("excludePlatforms", [])
    ok = {"Editor", "WindowsStandalone64"}
    if inc:
        return bool(ok & set(inc))
    return "Editor" not in exc


# ---------- references ----------

def framework_refs():
    """Unity 6 compiles every assembly, editor ones included, against .NET Standard 2.1 (Span, default interface members)."""
    data = os.path.join(EDITOR, "Data", "NetStandard")
    refs = [os.path.join(data, "ref", "2.1.0", "netstandard.dll")]
    # The netfx shims (mscorlib and friends) let it use DLLs built against .NET Framework, such as nunit.
    for shim in ("netstandard", "netfx"):
        refs += glob.glob(os.path.join(data, "compat", "2.1.0", "shims", shim, "*.dll"))
    refs += glob.glob(os.path.join(data, "Extensions", "2.0.0", "*.dll"))
    refs += glob.glob(os.path.join(data, "EditorExtensions", "*.dll"))  # the editor adds these to every in-editor compile
    seen, out = set(), []
    for r in refs:
        b = os.path.basename(r).lower()
        if b not in seen:
            seen.add(b)
            out.append(r)
    return out


def engine_refs():
    managed = os.path.join(EDITOR, "Data", "Managed")
    # Only the engine and editor modules: the folder also holds the editor's own tooling DLLs (Newtonsoft, Cecil, Bee), which would
    # shadow the packages' copies.
    refs = [r for r in glob.glob(os.path.join(managed, "UnityEngine", "*.dll")) if re.match(r"Unity(Engine|Editor|\.Scripting\.dll)", os.path.basename(r))]
    names = {os.path.basename(r) for r in refs}
    for extra in ["UnityEditor.dll", "UnityEngine.dll"]:
        p = os.path.join(managed, extra)
        if os.path.exists(p) and extra not in names:
            refs.append(p)
    return refs


def is_managed(path):
    """True when the PE file has a CLR header (data directory 14); native plugins (lz4, zlib, onigwrap) don't, and Unity never references them."""
    try:
        with open(path, "rb") as f:
            data = f.read(4096)
        pe = int.from_bytes(data[0x3C:0x40], "little")
        if data[pe:pe + 4] != b"PE\0\0":
            return False
        opt = pe + 24
        dirs = opt + (96 if data[opt:opt + 2] == b"\x0b\x01" else 112)  # PE32 vs PE32+
        return int.from_bytes(data[dirs + 14 * 8:dirs + 14 * 8 + 4], "little") != 0
    except (OSError, ValueError):
        return False


def plugin_dlls(project, roots):
    """filename -> (path, explicitly referenced?) for precompiled DLLs in Assets/ and packages, editor-compatible ones only."""
    out = {}
    tops = [os.path.join(project, "Assets")] + [r for r, _ in roots.values()]
    for top in tops:
        for p in glob.glob(os.path.join(top, "**", "*.dll"), recursive=True):
            if any(part.endswith("~") or part.startswith(".") for part in os.path.relpath(p, top).split(os.sep)):
                continue
            meta = ""
            try:
                meta = open(p + ".meta").read()
            except OSError:
                pass
            if "RoslynAnalyzer" in meta or not is_managed(p):
                continue
            # PluginImporter platformData: "first: {Editor: Editor} second: {enabled: N}", and "Exclude Editor: 1" under Any.
            editor = re.search(r"Editor: Editor\s*\n\s*second:\s*\n\s*enabled:\s*(\d)", meta)
            any_on = re.search(r"Any:\s*\n\s*second:\s*\n\s*enabled:\s*1", meta)
            if re.search(r"Exclude Editor:\s*1", meta) or (editor and editor.group(1) == "0" and not any_on):
                continue
            explicit = bool(re.search(r"isExplicitlyReferenced:\s*1", meta))
            out.setdefault(os.path.basename(p), (p, explicit))
    return out


def analyzers():
    tools = os.path.join(EDITOR, "Data", "Tools", "Unity.SourceGenerators")
    return glob.glob(os.path.join(tools, "*.dll"))


# ---------- compile ----------

def find_csc():
    for c in glob.glob(os.path.join(EDITOR, "Data", "DotNetSdkRoslyn", "csc.dll")) + \
             sorted(glob.glob("/usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll"))[::-1]:
        return c
    die("no csc.dll found (install the .NET SDK or run --setup)")


def compile_all(project, only, warnings, csproj=False):
    if not os.path.isdir(EDITOR):
        die(f"Unity editor not found at {EDITOR}: run with --setup first")
    roots = package_roots(project)
    versions = {n: v for n, (_, v) in roots.items()}
    asms, by_guid = discover(project, roots)
    base = unity_defines()
    plugins = plugin_dlls(project, roots)
    engine = engine_refs()
    gens = analyzers()
    csc = find_csc()
    os.makedirs(CACHE, exist_ok=True)

    def resolve(ref):
        return by_guid.get(ref[5:]) if ref.startswith("GUID:") else asms.get(ref)

    project_asms = [a for a in asms.values() if a.package is None]
    if only:
        project_asms = [a for a in project_asms if a.name in only]
    built, failed, skipped = {}, {}, {}
    order = []

    def visit(a, stack=()):
        if a.name in built or a.name in failed or a.name in skipped:
            return
        if a.name in stack:
            failed[a.name] = ["reference cycle: " + " -> ".join(stack + (a.name,))]
            return
        defines = asm_defines(a, base, versions)
        if not platform_ok(a) or not constraints_ok(a, defines):
            skipped[a.name] = "platform or define constraint"
            return
        if not a.sources:
            skipped[a.name] = "no sources"
            return
        deps = []
        refs = []
        for r in a.data.get("references", []):
            refs += ALIASES.get(r, [r])
        for r in refs:
            t = resolve(r)
            if t is None:
                continue  # Unity ignores missing references too (a warning)
            visit(t, stack + (a.name,))
            if t.name in failed:
                failed[a.name] = [f"dependency {t.name} failed"]
                return
            if t.name in built:
                deps.append(built[t.name])
        out = compile_one(a, defines, deps, plugins, engine, gens, csc, warnings)
        order.append(a.name)
        if out[0]:
            built[a.name] = out[1]
        else:
            failed[a.name] = out[1]

    for a in sorted(project_asms, key=lambda x: x.name):
        visit(a)

    bad = False
    for a in sorted(project_asms, key=lambda x: x.name):
        if a.name in built:
            print(f"OK    {a.name} ({len(a.sources)} files)")
        elif a.name in skipped:
            print(f"SKIP  {a.name}: {skipped[a.name]}")
        else:
            bad = True
            print(f"FAIL  {a.name}")
            for line in failed.get(a.name, [])[:60]:
                print("      " + line)
    pkg_fail = [n for n in failed if asms[n].package is not None]
    for n in pkg_fail:
        print(f"FAIL  package assembly {n} ({asms[n].package})")
        for line in failed[n][:15]:
            print("      " + line)
    if csproj:
        write_csproj(project, built)
    print("PASS" if not bad else "FAIL")
    return 0 if not bad else 1


PROJECT_INFO = {}  # project assembly name -> (defines, references, sources, unsafe), filled by compile_one for --csproj


def write_csproj(project, built):
    """Unity-style <Assembly>.csproj files and SleepingCalamity.sln at the project root, for the csharp-lsp plugin (csharp-ls). Gitignored.
    Project-to-project references become ProjectReference; everything else (engine, framework, packages, plugins) a HintPath Reference."""
    own = {path: name for name, path in built.items() if name in PROJECT_INFO}
    names = []
    for name, (defines, refs, srcs, unsafe) in sorted(PROJECT_INFO.items()):
        rel = lambda x: os.path.relpath(x, project)
        lines = ['<Project Sdk="Microsoft.NET.Sdk">', "  <PropertyGroup>",
                 "    <TargetFramework>netstandard2.1</TargetFramework>", f"    <LangVersion>{LANG_VERSION}</LangVersion>",
                 f"    <AssemblyName>{name}</AssemblyName>", f"    <DefineConstants>{';'.join(defines)}</DefineConstants>",
                 "    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>", "    <NoStdLib>true</NoStdLib>",
                 "    <EnableDefaultItems>false</EnableDefaultItems>", "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
                 f"    <AllowUnsafeBlocks>{str(unsafe).lower()}</AllowUnsafeBlocks>", "    <NoWarn>1701;1702;0169;0649;0414</NoWarn>",
                 f"    <BaseIntermediateOutputPath>Temp/cloud-obj/{name}/</BaseIntermediateOutputPath>",
                 f"    <OutputPath>Temp/cloud-bin/{name}/</OutputPath>",
                 "  </PropertyGroup>", "  <ItemGroup>"]
        lines += [f'    <Compile Include="{rel(x)}" />' for x in srcs]
        lines += ["  </ItemGroup>", "  <ItemGroup>"]
        for r in refs:
            if r in own:
                lines.append(f'    <ProjectReference Include="{own[r]}.csproj" />')
            else:
                lines.append(f'    <Reference Include="{os.path.splitext(os.path.basename(r))[0]}"><HintPath>{r}</HintPath><Private>false</Private></Reference>')
        lines += ["  </ItemGroup>", "</Project>", ""]
        open(os.path.join(project, f"{name}.csproj"), "w").write("\n".join(lines))
        names.append(name)
    # classic .sln: csharp-ls 0.16 loads .sln, not Unity's .slnx
    ptype = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"

    def guid(n):  # stable per assembly name
        h = hashlib.md5(n.encode()).hexdigest().upper()
        return f"{h[:8]}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:]}"
    sln = ["", "Microsoft Visual Studio Solution File, Format Version 12.00", "# Visual Studio Version 17"]
    for n in names:
        sln += [f'Project("{{{ptype}}}") = "{n}", "{n}.csproj", "{{{guid(n)}}}"', "EndProject"]
    sln += ["Global", "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution", "\t\tDebug|Any CPU = Debug|Any CPU", "\tEndGlobalSection",
            "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution"]
    for n in names:
        sln += [f"\t\t{{{guid(n)}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU", f"\t\t{{{guid(n)}}}.Debug|Any CPU.Build.0 = Debug|Any CPU"]
    sln += ["\tEndGlobalSection", "EndGlobal", ""]
    open(os.path.join(project, "SleepingCalamity.sln"), "w").write("\r\n".join(sln))
    print(f"wrote {len(names)} .csproj files and SleepingCalamity.sln (gitignored: never commit them)")


def compile_one(a, defines, deps, plugins, engine, gens, csc, warnings):
    refs = framework_refs()
    if not a.data.get("noEngineReferences", False):
        refs += engine
    if a.data.get("overrideReferences", False):
        refs += [plugins[n][0] for n in a.data.get("precompiledReferences", []) if n in plugins]
    else:
        refs += [p for p, explicit in plugins.values() if not explicit]
    refs += deps
    # Dedupe by file name: later (project/package) references don't shadow engine ones.
    seen, uniq = set(), []
    for r in refs:
        b = os.path.basename(r).lower()
        if b in seen:
            continue
        seen.add(b)
        uniq.append(r)
    srcs = sorted(a.sources)
    if a.package is None:
        PROJECT_INFO[a.name] = (sorted(defines), uniq, srcs, a.data.get("allowUnsafeCode", False))
    h = hashlib.sha256()
    for part in [a.name, LANG_VERSION, ";".join(sorted(defines))] + uniq:
        h.update(part.encode())
    for s in srcs:
        h.update(s.encode())
        h.update(open(s, "rb").read())
    for r in deps:
        h.update(open(r, "rb").read())
    key = h.hexdigest()[:20]
    # The file name sets the assembly name, which InternalsVisibleTo grants (the engine opens internals to ugui, the test runner...).
    out_dir = os.path.join(CACHE, a.name, key)
    out = os.path.join(out_dir, f"{a.name}.dll")
    if os.path.exists(out):
        return True, out
    os.makedirs(out_dir, exist_ok=True)
    rsp = os.path.join(out_dir, "csc.rsp")
    args = ["-nologo", "-target:library", "-nostdlib+", "-noconfig", f"-langversion:{LANG_VERSION}", "-deterministic",
            "-debug-", "-optimize-", "-nowarn:1701,1702,0169,0649,0414", f"-out:{out}",
            "-define:" + ";".join(sorted(defines))]
    if a.data.get("allowUnsafeCode", False):
        args.append("-unsafe")
    if not warnings or a.package is not None:
        args.append("-warn:0")
    args += [f'-r:"{r}"' for r in uniq]
    args += [f'-analyzer:"{g}"' for g in gens]
    args += [f'"{s}"' for s in srcs]
    open(rsp, "w").write("\n".join(args))
    r = subprocess.run(["dotnet", csc, f"@{rsp}"], capture_output=True, text=True)
    lines = [l for l in (r.stdout + r.stderr).splitlines() if l.strip()]
    if r.returncode == 0 and os.path.exists(out):
        if warnings and a.package is None:
            for l in lines:
                print(f"      {a.name}: {l}")
        return True, out
    errs = [l for l in lines if ": error " in l] or lines
    return False, errs


def main():
    ap = argparse.ArgumentParser(description="Compile SleepingCalamity's C# assemblies on Linux against the Unity editor's reference DLLs.")
    ap.add_argument("--project", default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
    ap.add_argument("--setup", action="store_true", help="download the editor and packages to ~/unity, then exit")
    ap.add_argument("--only", default="", help="comma-separated project assembly names")
    ap.add_argument("--warnings", action="store_true", help="print compiler warnings for project assemblies")
    ap.add_argument("--csproj", action="store_true", help="also write <Assembly>.csproj files and SleepingCalamity.sln for the C# LSP (gitignored)")
    a = ap.parse_args()
    if a.setup:
        setup(a.project)
        return 0
    only = {x for x in a.only.split(",") if x}
    return compile_all(a.project, only, a.warnings, a.csproj)


if __name__ == "__main__":
    sys.exit(main())
