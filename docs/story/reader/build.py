import re, json, html, glob, os, sys

BOOK_DIR = r"C:\Dev\SleepingCalamity\.claude\worktrees\procgen-next-73e90d\docs\story\book"
SP = os.path.dirname(os.path.abspath(__file__))


def inline(s):
    s = html.escape(s, quote=False)
    s = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", s)
    s = re.sub(r"(?<![*\w])\*(?!\s)(.+?)(?<!\s)\*(?![*\w])", r"<em>\1</em>", s)
    return s


def parse(path):
    lines = open(path, encoding="utf-8").read().split("\n")
    blocks, para = [], []
    title = None
    num = None

    def flush():
        nonlocal para
        if para:
            text = " ".join(x.strip() for x in para).strip()
            if text:
                if re.fullmatch(r"\*[^*]+\*", text):
                    blocks.append(["tag", inline(text[1:-1])])
                else:
                    blocks.append(["p", inline(text)])
        para = []

    for ln in lines:
        s = ln.rstrip()
        if not s.strip():
            flush()
            continue
        m = re.match(r"# Chapter (\d+): (.*)", s)
        if m:
            flush()
            num, title = int(m.group(1)), m.group(2).strip()
            blocks.append(["h1", inline(title)])
            continue
        if s.strip() == "---":
            flush()
            blocks.append(["hr", ""])
            continue
        if s.startswith("### "):
            flush()
            blocks.append(["s", inline(s[4:].strip())])
            continue
        if s.startswith("## "):
            flush()
            blocks.append(["h2", inline(s[3:].strip())])
            continue
        if re.fullmatch(r"\*\*[^*]+\*\*", s.strip()):
            flush()
            blocks.append(["h2", inline(s.strip()[2:-2])])
            continue
        if s.startswith("> "):
            flush()
            blocks.append(["q", inline(s[2:])])
            continue
        m = re.match(r"(?:[-*]|\d+\.)\s+(.*)", s)
        if m and not para:
            flush()
            lead = re.match(r"(\d+\.)\s", s)
            blocks.append(["li", inline((lead.group(1) + " " if lead else "") + m.group(1))])
            continue
        para.append(s)
    flush()
    # drop trailing hr
    return {"n": num, "title": title, "blocks": blocks}


def main():
    files = sorted(glob.glob(os.path.join(BOOK_DIR, "0[1-9]-*.md")))
    chapters = [parse(f) for f in files]
    data = json.dumps({"chapters": chapters}, ensure_ascii=False, separators=(",", ":"))
    data = data.replace("</", "<\\/").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    tpl = open(os.path.join(SP, "reader.template.html"), encoding="utf-8").read()
    out = tpl.replace("__BOOK__", data)
    with open(os.path.join(SP, "book-reader.html"), "w", encoding="utf-8") as f:
        f.write(out)
    words = sum(len(re.sub(r"<[^>]+>", "", b[1]).split()) for c in chapters for b in c["blocks"])
    print(len(chapters), "chapters", words, "words", len(out) // 1024, "KB")


main()
