#!/usr/bin/env python3
"""C# code navigation in cloud sessions: drives csharp-ls over stdio, standing in for Claude Code's LSP tool.

Cloud Claude Code does not offer the LSP tool, even with ENABLE_LSP_TOOL=1 and the csharp-lsp plugin loaded (tested 2026-10-30: no tool,
and the server is never started), so this script asks the same server directly. It needs the .csproj/.sln that
`sc_cloud_compile.py --csproj` writes (the setup job and the SessionStart hook run it once the Unity editor is in; before that only Core loads).

  python3 Tools/cloud/sc_lsp.py def     <file>:<line> <name>   where <name> on that line is defined
  python3 Tools/cloud/sc_lsp.py refs    <file>:<line> <name>   every use of it (declaration left out)
  python3 Tools/cloud/sc_lsp.py hover   <file>:<line> <name>   its signature and docs
  python3 Tools/cloud/sc_lsp.py symbols <query>               types and members matching <query>, project-wide

Lines are 1-based; <name> picks the column (its first occurrence on the line). Paths print repo-relative. Each call starts the server and
loads the solution (about 10-20 s); exit 1 on no result, 2 on a setup problem.
"""
import json, os, queue, subprocess, sys, threading, time

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SLN = os.path.join(REPO, "SleepingCalamity.sln")
SYMBOL_KINDS = {5: "class", 6: "method", 7: "property", 8: "field", 9: "ctor", 10: "enum", 11: "interface", 12: "function",
                13: "variable", 14: "const", 22: "enum member", 23: "struct", 24: "event"}


class Server:
    def __init__(self):
        self.p = subprocess.Popen(["csharp-ls", "--solution", SLN], cwd=REPO, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.DEVNULL)
        self.msgs, self.next_id = queue.Queue(), 0
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        while True:
            headers = {}
            while True:
                line = self.p.stdout.readline()
                if not line:
                    return
                line = line.decode().strip()
                if not line:
                    break
                k, v = line.split(":", 1)
                headers[k.lower()] = v.strip()
            self.msgs.put(json.loads(self.p.stdout.read(int(headers["content-length"]))))

    def _write(self, msg):
        body = json.dumps(msg).encode()
        self.p.stdin.write(b"Content-Length: %d\r\n\r\n" % len(body) + body)
        self.p.stdin.flush()

    def notify(self, method, params):
        self._write({"jsonrpc": "2.0", "method": method, "params": params})

    def request(self, method, params, timeout=180):
        self.next_id += 1
        rid = self.next_id
        self._write({"jsonrpc": "2.0", "id": rid, "method": method, "params": params})
        end = time.time() + timeout
        while time.time() < end:
            try:
                m = self.msgs.get(timeout=1)
            except queue.Empty:
                continue
            if "method" in m and "id" in m:  # a server request (progress, config): answer null
                self._write({"jsonrpc": "2.0", "id": m["id"], "result": None})
            elif m.get("id") == rid:
                return m.get("result")
        return None

    def close(self):
        self.p.kill()


def uri(path):
    return "file://" + path


def rel(u):
    return u[len("file://"):].replace(REPO + "/", "", 1)


def show_locations(locs):
    locs = locs if isinstance(locs, list) else [locs] if locs else []
    for loc in locs:
        u = loc.get("uri") or loc.get("targetUri")
        r = loc.get("range") or loc.get("targetSelectionRange")
        print("%s:%d" % (rel(u), r["start"]["line"] + 1))
    return bool(locs)


def main(argv):
    if len(argv) < 2 or argv[0] not in ("def", "refs", "hover", "symbols") or (argv[0] != "symbols" and len(argv) < 3):
        print(__doc__.split("\n\n")[2], file=sys.stderr)
        return 2
    if not os.path.exists(SLN):
        print("no SleepingCalamity.sln: run python3 Tools/cloud/sc_cloud_compile.py --csproj (needs the Unity editor: setup.sh --check)",
              file=sys.stderr)
        return 2
    srv = Server()
    try:
        srv.request("initialize", {"processId": os.getpid(), "rootUri": uri(REPO), "capabilities": {}})
        srv.notify("initialized", {})
        if argv[0] == "symbols":
            result = None
            for _ in range(20):  # the solution loads in the background: retry until it answers
                result = srv.request("workspace/symbol", {"query": argv[1]})
                if result:
                    break
                time.sleep(2)
            for s in result or []:
                loc = s["location"]
                print("%-11s %s%s  %s:%d" % (SYMBOL_KINDS.get(s["kind"], "?"), (s.get("containerName") + ".") if s.get("containerName") else "",
                                             s["name"], rel(loc["uri"]), loc["range"]["start"]["line"] + 1))
            return 0 if result else 1

        path_arg, name = argv[1], argv[2]
        file_part, _, line_part = path_arg.rpartition(":")
        path = os.path.abspath(os.path.join(REPO, file_part) if not os.path.isabs(file_part) else file_part)
        lines = open(path, encoding="utf-8-sig").read().split("\n")
        line = int(line_part) - 1
        col = lines[line].find(name)
        if col < 0:
            print("'%s' is not on line %d of %s: %s" % (name, line + 1, file_part, lines[line].strip()), file=sys.stderr)
            return 2
        srv.notify("textDocument/didOpen", {"textDocument": {"uri": uri(path), "languageId": "csharp", "version": 1, "text": "\n".join(lines)}})
        pos = {"textDocument": {"uri": uri(path)}, "position": {"line": line, "character": col}}
        hover = None
        for _ in range(20):  # hover answers once the document's project is loaded
            hover = srv.request("textDocument/hover", pos)
            if hover:
                break
            time.sleep(2)
        if argv[0] == "hover":
            contents = (hover or {}).get("contents")
            text = contents.get("value") if isinstance(contents, dict) else contents
            print(text or "no hover (is the file in a generated .csproj?)")
            return 0 if text else 1
        if argv[0] == "def":
            return 0 if show_locations(srv.request("textDocument/definition", pos)) else 1
        refs = srv.request("textDocument/references", dict(pos, context={"includeDeclaration": False})) or []
        refs.sort(key=lambda l: (l["uri"], l["range"]["start"]["line"]))
        show_locations(refs)
        print("%d references" % len(refs), file=sys.stderr)
        return 0 if refs else 1
    finally:
        srv.close()


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
