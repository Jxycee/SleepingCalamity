#!/usr/bin/env python3
"""Cloud shader check for SleepingCalamity: compiles the project's HLSL shader passes on Linux with DXC, without running Unity.

Unity compiles a .shader's HLSLPROGRAM blocks with its own HLSL front end per keyword variant. This script does the same steps by hand:
it reads each ShaderLab file, takes every pass's HLSLINCLUDE + HLSLPROGRAM text, its #pragma vertex/fragment entry points and its keyword
pragmas (multi_compile*, shader_feature*, including the ones pulled in by #include_with_pragmas), and compiles each stage of each variant
with Microsoft's DirectXShaderCompiler (dxc) against the real URP and core ShaderLibrary from the Unity 6000.6.5f1 editor's built-in
packages (sc_cloud_compile.py --setup fetches the editor). "Packages/..." and "Assets/..." includes resolve the way they do in the editor.
Errors print as `Assets/...shader(line): error: ...` thanks to #line directives.

It catches what a cloud session can get wrong in HLSL: syntax, undeclared names, wrong types and overloads, missing includes, a keyword
variant that breaks. It is an approximation of Unity's compile, not a replacement:
  - DXC (shader model 6, HLSL 2018) stands in for the editor's per-platform compilers (FXC for D3D11, DXC for D3D12/Vulkan/Metal);
  - only the variants listed by --variants are compiled (default: the base variant plus each keyword alone);
  - ShaderLab itself (Properties, render states, UsePass, Fallback) is not checked, and nothing is rendered.
The PC's `unity.ps1 check` stays the merge gate for Assets/ changes.

FXC-like preprocessing matters: URP's instancing macros paste an argument that is itself a macro (arr##Array), which only FXC's
expansion order resolves, so dxc runs with -flegacy-macro-expansion. Pragmas in #include_with_pragmas files are read after dxc -P,
because URP guards them with #if (Fog.hlsl picks dynamic_branch or multi_compile_fog).

Setup (once per container): python3 Tools/cloud/sc_cloud_compile.py --setup (the editor and its URP), then
    python3 Tools/cloud/sc_shader_check.py --setup      # the DXC Linux release (13 MB, GitHub) to ~/dxc
Usage:
    python3 Tools/cloud/sc_shader_check.py [--project PATH] [--shader NAME_OR_PATH ...] [--variants base|each] [--api d3d11|vulkan|both]
    python3 Tools/cloud/sc_shader_check.py --selftest     # injects an error into SleepingCalamityCommon.hlsl and expects FAIL on Toon.shader
Exit codes: 0 every compiled variant passed, 1 compile errors, 2 setup missing or usage.
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sc_cloud_compile as cc  # noqa: E402  (shares the editor location and the package lock reading)

DXC_DEFAULT = os.path.expanduser("~/dxc/bin/dxc")
DXC_URL = "https://github.com/microsoft/DirectXShaderCompiler/releases/download/v1.8.2407/linux_dxc_2024_07_31.x86_64.tar.gz"
UNITY_VERSION_DEFINE = "600060"   # UNITY_VERSION for 6000.6 (major * 100 + minor * 10)
PRAGMA_KEYWORDS = re.compile(r"^\s*#pragma\s+(multi_compile|shader_feature|dynamic_branch)(_local)?(_vertex|_fragment|_hull|_domain|_geometry|_raytracing)?\s+(.*)$")
INCLUDE_WITH_PRAGMAS = re.compile(r'^\s*#include_with_pragmas\s+"([^"]+)"')


def die(msg, code=2):
    print(msg, file=sys.stderr)
    sys.exit(code)


# ---------- ShaderLab parsing ----------

class Program:
    def __init__(self, shader, name, includes, start, text):
        self.shader = shader          # project-relative path, for messages and #line
        self.name = name              # the pass Name, or "pass N"
        self.includes = includes      # [(first line number, text)] HLSLINCLUDE blocks in scope
        self.start = start            # line number of the program's first line
        self.text = text


def parse_shader(path, rel):
    """Every HLSLPROGRAM block of a .shader with the HLSLINCLUDE blocks in scope (the shader's and its SubShader's, in order)."""
    lines = open(path, encoding="utf-8-sig").read().split("\n")
    programs, top_includes, sub_includes = [], [], None
    block, block_kind, block_start, pass_name, pass_count = None, None, 0, None, 0
    for i, line in enumerate(lines, 1):
        s = line.strip()
        if block is not None:
            if s == "ENDHLSL":
                text = "\n".join(block)
                if block_kind == "HLSLINCLUDE":
                    (top_includes if sub_includes is None else sub_includes).append((block_start, text))
                else:
                    pass_count += 1
                    programs.append(Program(rel, pass_name or f"pass {pass_count}", top_includes + (sub_includes or []), block_start, text))
                    pass_name = None
                block = None
            else:
                block.append(line)
            continue
        if s.startswith("//"):
            continue
        if s in ("HLSLINCLUDE", "HLSLPROGRAM"):
            block, block_kind, block_start = [], s, i + 1
        elif s == "CGPROGRAM" or s == "CGINCLUDE":
            print(f"  note: {rel}({i}): {s} is not checked (built-in pipeline CG)")
        elif re.match(r"^SubShader\b", s):
            sub_includes = []
        else:
            m = re.match(r'^Name\s+"([^"]*)"', s)
            if m:
                pass_name = m.group(1)
    return programs


# ---------- pragmas and variants ----------

def resolve_include(name, project, roots, from_dir=None):
    if from_dir and not name.startswith(("Packages/", "Assets/")):
        p = os.path.join(from_dir, name)
        if os.path.exists(p):
            return p
    if name.startswith("Assets/"):
        return os.path.join(project, name)
    if name.startswith("Packages/"):
        pkg, _, rest = name[len("Packages/"):].partition("/")
        if pkg in roots:
            return os.path.join(roots[pkg][0], rest)
    return None


def pragma_lines(text, project, roots, preprocess, seen=None):
    """The program's #pragma lines plus those of every #include_with_pragmas file, in order. An included file is preprocessed first
    (preprocess(path) -> text), because URP guards its pragmas with #if (Fog.hlsl: dynamic_branch or multi_compile_fog)."""
    seen = set() if seen is None else seen
    out = []
    for line in text.split("\n"):
        m = INCLUDE_WITH_PRAGMAS.match(line)
        if m:
            p = resolve_include(m.group(1), project, roots)
            if p and os.path.exists(p) and p not in seen:
                seen.add(p)
                out += [l.strip() for l in preprocess(p).split("\n") if l.strip().startswith("#pragma")]
        elif line.strip().startswith("#pragma"):
            out.append(line.strip())
    return out


def keyword_sets(pragmas):
    """[(stage or None, [options])] where '_' is the no-keyword option; plus the dynamic_branch keywords (uniform bools in Unity)."""
    sets, dynamic = [], []
    for p in pragmas:
        if re.match(r"^#pragma\s+multi_compile_instancing\b", p):
            sets.append((None, ["_", "INSTANCING_ON"]))
            continue
        if re.match(r"^#pragma\s+multi_compile_fog\b", p):
            sets.append((None, ["_", "FOG_LINEAR", "FOG_EXP", "FOG_EXP2"]))
            continue
        m = PRAGMA_KEYWORDS.match(p)
        if not m:
            continue
        kind, stage = m.group(1), (m.group(3) or "").lstrip("_") or None
        opts = [o if not re.fullmatch(r"_+", o) else "_" for o in m.group(4).split("//")[0].split()]
        if not opts:
            continue
        if kind == "dynamic_branch":
            dynamic += [o for o in opts if o != "_"]
            continue
        if len(opts) == 1 and kind == "shader_feature":
            opts = ["_"] + opts
        sets.append((stage, opts))
    return sets, dynamic


def variants(sets, mode):
    """Lists of enabled keywords: the base (first option of every set), then with --variants each, every other option alone."""
    base = [opts[0] for _, opts in sets]
    out = [[k for k in base if k != "_"]]
    if mode == "each":
        for si, (_, opts) in enumerate(sets):
            for o in opts[1:]:
                kws = [k for j, k in enumerate(base) if j != si and k != "_"]
                if o != "_":
                    kws.append(o)
                if kws not in out:
                    out.append(kws)
    return out


def entry(pragmas, stage):
    for p in pragmas:
        m = re.match(rf"^#pragma\s+{stage}\s+(\w+)", p)
        if m:
            return m.group(1)
    return None


def target(pragmas):
    for p in pragmas:
        m = re.match(r"^#pragma\s+target\s+(\d)\.(\d)", p)
        if m:
            return m.group(1) + m.group(2)
    return "25"


# ---------- compile ----------

def make_include_root(work, project, roots, overlay):
    """A directory where Packages/<name> and Assets resolve like the editor's virtual paths (overlay first, for --selftest)."""
    root = os.path.join(work, "root")
    os.makedirs(os.path.join(root, "Packages"), exist_ok=True)
    os.symlink(os.path.join(project, "Assets"), os.path.join(root, "Assets"))
    for name, (path, _) in roots.items():
        os.symlink(path, os.path.join(root, "Packages", name))
    dirs = [overlay] if overlay else []
    return dirs + [root]


def wrapper_source(prog, keywords, dynamic, api, stage, shader_target):
    defines = {
        "SHADER_API_D3D11": api == "d3d11", "SHADER_API_VULKAN": api == "vulkan", "SHADER_API_DESKTOP": True,
        "UNITY_COMPILER_DXC": True, "UNITY_VERSION": UNITY_VERSION_DEFINE, "SHADER_TARGET": shader_target,
        "SHADER_STAGE_VERTEX": stage == "vertex", "SHADER_STAGE_FRAGMENT": stage == "fragment",
    }
    out = []
    for k, v in defines.items():
        if v is True:
            out.append(f"#define {k} 1")
        elif v:
            out.append(f"#define {k} {v}")
    out += [f"#define {k} 1" for k in keywords]
    out += [f"uniform bool {k};" for k in dynamic]
    for start, text in prog.includes + [(prog.start, prog.text)]:
        out.append(f'#line {start} "{prog.shader}"')
        out.append(re.sub(r"(?m)^(\s*)#include_with_pragmas\b", r"\1#include", text))
    return "\n".join(out) + "\n"


def compile_stage(dxc, src_path, entry_name, profile, includes, api, hv):
    args = [dxc, "-T", profile, "-E", entry_name, "-HV", hv, "-Wno-unknown-pragmas", "-Wno-ignored-attributes", "-flegacy-macro-expansion",
            "-Fo", os.devnull]
    if api == "vulkan":
        args += ["-spirv", "-fspv-target-env=vulkan1.1", "-fvk-use-dx-layout"]
    for d in includes:
        args += ["-I", d]
    args.append(src_path)
    r = subprocess.run(args, capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def find_shaders(project, names):
    all_shaders = sorted(glob.glob(os.path.join(project, "Assets", "**", "*.shader"), recursive=True))
    if not names:
        return all_shaders
    picked = []
    for n in names:
        hits = [s for s in all_shaders if os.path.abspath(s) == os.path.abspath(n) or os.path.basename(s) in (n, n + ".shader")]
        if not hits:
            die(f"no shader matches {n}")
        picked += hits
    return picked


def check(project, shaders, mode, apis, dxc, hv, overlay=None, quiet=False):
    roots = cc.package_roots(project)
    for need in ("com.unity.render-pipelines.universal", "com.unity.render-pipelines.core"):
        if need not in roots:
            die(f"{need} not found; run sc_cloud_compile.py --setup first")
    work = tempfile.mkdtemp(prefix="sc_shader_")
    failures, compiled = 0, 0
    try:
        includes = make_include_root(work, project, roots, overlay)
        pp_cache = {}

        def preprocess(path):
            if path not in pp_cache:
                src, out = os.path.join(work, "pp.hlsl"), os.path.join(work, "pp.i")
                open(src, "w").write("#define SHADER_API_D3D11 1\n#define UNITY_COMPILER_DXC 1\n"
                                     f"#define UNITY_VERSION {UNITY_VERSION_DEFINE}\n#include \"{path}\"\n")
                args = [dxc, "-P", "-Fi", out, "-HV", hv, "-flegacy-macro-expansion"] + [x for d in includes for x in ("-I", d)] + [src]
                r = subprocess.run(args, capture_output=True, text=True)
                pp_cache[path] = open(out).read() if r.returncode == 0 and os.path.exists(out) else open(path, encoding="utf-8-sig").read()
            return pp_cache[path]

        for path in shaders:
            rel = os.path.relpath(path, project)
            programs = parse_shader(path, rel)
            if not programs:
                print(f"SKIP {rel}: no HLSLPROGRAM")
                continue
            for prog in programs:
                pragmas = pragma_lines("\n".join(t for _, t in prog.includes) + "\n" + prog.text, project, roots, preprocess)
                sets, dynamic = keyword_sets(pragmas)
                stages = [(st, entry(pragmas, st)) for st in ("vertex", "fragment")]
                stages = [(st, e) for st, e in stages if e]
                tgt = target(pragmas)
                for api in apis:
                    for kws in variants(sets, mode):
                        for stage, fn in stages:
                            # a _vertex/_fragment keyword set only exists in its own stage
                            stage_kws = [k for k in kws if all(not (k in opts and s not in (None, stage)) for s, opts in sets)]
                            src = os.path.join(work, "src.hlsl")
                            open(src, "w").write(wrapper_source(prog, stage_kws, dynamic, api, stage, tgt))
                            profile = ("vs_6_0" if stage == "vertex" else "ps_6_0")
                            code, log = compile_stage(dxc, src, fn, profile, includes, api, hv)
                            compiled += 1
                            label = f"{rel} [{prog.name}] {stage} {api} {' '.join(stage_kws) or '(base)'}"
                            if code != 0:
                                failures += 1
                                print(f"FAIL {label}")
                                for l in log.split("\n"):
                                    if "error" in l or failures <= 3:
                                        print("    " + l)
                            elif not quiet:
                                print(f"ok   {label}")
    finally:
        shutil.rmtree(work, ignore_errors=True)
    return failures, compiled


def selftest(project, dxc, hv):
    """Copies SleepingCalamityCommon.hlsl into an overlay with an undeclared call inside SC_PaintVariation (R102) or, on a tree without it,
    SC_PaletteColor; Toon.shader must then FAIL."""
    common = os.path.join(project, "Assets", "_Project", "Shaders", "SleepingCalamityCommon.hlsl")
    text = open(common).read()
    marker = next((m for m in ("half3 SC_PaintVariation(", "half3 SC_PaletteColor(") if m in text), None)
    if marker is None:
        die("selftest: neither SC_PaintVariation nor SC_PaletteColor is in SleepingCalamityCommon.hlsl")
    body = text.index("{", text.index(marker)) + 1
    injected = text[:body] + "\n    SC_NoSuchFunction_SelfTest(1.0);\n" + text[body:]
    overlay = tempfile.mkdtemp(prefix="sc_shader_overlay_")
    try:
        dest = os.path.join(overlay, "Assets", "_Project", "Shaders")
        os.makedirs(dest)
        open(os.path.join(dest, "SleepingCalamityCommon.hlsl"), "w").write(injected)
        toon = find_shaders(project, ["Toon.shader"])
        print("selftest 1/2: Toon.shader as committed must pass (base variant)")
        f1, _ = check(project, toon, "base", ["d3d11"], dxc, hv, quiet=True)
        print(f"selftest 2/2: with an undeclared call injected into {marker[6:-1]} it must fail")
        f2, n2 = check(project, toon, "base", ["d3d11"], dxc, hv, overlay=overlay, quiet=True)
    finally:
        shutil.rmtree(overlay, ignore_errors=True)
    ok = f1 == 0 and f2 > 0
    print(("SELFTEST PASS" if ok else "SELFTEST FAIL") + f": committed {f1} failures, injected {f2}/{n2} failures")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description="Compile SleepingCalamity's HLSL shader passes on Linux with DXC against the editor's URP ShaderLibrary.")
    ap.add_argument("--project", default=os.path.abspath(os.path.join(HERE, "..", "..")))
    ap.add_argument("--shader", nargs="*", default=[], help="shader file names or paths (default: every .shader under Assets/)")
    ap.add_argument("--variants", choices=["base", "each"], default="each", help="base: one variant per pass; each: plus every keyword alone")
    ap.add_argument("--api", choices=["d3d11", "vulkan", "both"], default="d3d11")
    ap.add_argument("--hv", default="2018", help="HLSL language version passed to dxc -HV (Unity's front end is closest to 2018)")
    ap.add_argument("--dxc", default=DXC_DEFAULT)
    ap.add_argument("--quiet", action="store_true", help="print failures only")
    ap.add_argument("--selftest", action="store_true", help="prove the check catches an injected error in SleepingCalamityCommon.hlsl")
    ap.add_argument("--setup", action="store_true", help="download the DXC Linux release to ~/dxc, then exit")
    a = ap.parse_args()
    if a.setup:
        os.makedirs(os.path.dirname(os.path.dirname(DXC_DEFAULT)), exist_ok=True)
        subprocess.run(f"curl -fsSL --retry 4 {DXC_URL} | tar xz -C {os.path.dirname(os.path.dirname(DXC_DEFAULT))}", shell=True, check=True)
        subprocess.run([DXC_DEFAULT, "--version"], check=True)
        sys.exit(0)
    if not os.path.exists(a.dxc):
        die(f"dxc not found at {a.dxc}; run python3 Tools/cloud/sc_shader_check.py --setup")
    if not os.path.isdir(cc.EDITOR):
        die("Unity editor not found; run python3 Tools/cloud/sc_cloud_compile.py --setup")
    project = os.path.abspath(a.project)
    if a.selftest:
        sys.exit(selftest(project, a.dxc, a.hv))
    apis = ["d3d11", "vulkan"] if a.api == "both" else [a.api]
    failures, compiled = check(project, find_shaders(project, a.shader), a.variants, apis, a.dxc, a.hv, quiet=a.quiet)
    print(("PASS" if failures == 0 else "FAIL") + f": {compiled - failures}/{compiled} stage variants compiled")
    sys.exit(0 if failures == 0 else 1)


if __name__ == "__main__":
    main()
