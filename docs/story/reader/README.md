# Book reader (artifact source)
Rebuild: `python docs/story/reader/build.py` (reads docs/story/book/0N-*.md, writes book-reader.html next to the script; publish it with the Artifact tool, capability `db`). Notes are in db collection `annotations` (fields: ch, segs, quote, color, target me|claude, note, status, reply), bookmarks in `bookmarks`, position in `state/progress`.
