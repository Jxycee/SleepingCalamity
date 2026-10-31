// SceneFileNormalizer: makes a freshly saved .unity file byte-stable. Unity gives every new GameObject and component a random local fileID
// (a different base per object, per run), so rebuilding a scene rewrites the file even when nothing changed. Normalize renumbers those ids
// from the scene hierarchy (SceneRoots order, then each object's component order, then its children), renames every {fileID: n} reference
// to match, and writes the documents back in new-id order. The settings documents (fixed small ids and SceneRoots) are left alone, and so is
// any reference that carries a guid (an asset). Pure string code; ProjectBuilder's scene saves call it through BuilderUtil.SaveScene.
// PrefabInstance (class 1001) documents and the stripped documents that stand in for a prefab's objects are renumbered like any other local object: the
// instance is numbered just before the stripped transform that places it in the hierarchy (everything a scene we build contains has plain local ids; a
// stripped id Unity derived from an asset would be negative or XOR-ed, and a scene with a negative document id is returned unchanged with a warning).
// References that carry a guid (the prefab source, modification targets) are never touched.
// NGO derives an in-scene NetworkObject's GlobalObjectIdHash from its fileID (NetworkObject.OnValidate) and serializes it in the scene, so
// renumbering invalidates the stored hash: BuilderUtil.SaveScene reopens a scene that has a NetworkObject, runs OnValidate and saves again
// (a resave keeps the fileIDs), and Validators.SceneNetworkObjectHashes proves no hash would change.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SleepingCalamity.EditorTools
{
    public static class SceneFileNormalizer
    {
        const long FirstId = 1000;
        const string SceneRootsClass = "1660057539";
        static readonly HashSet<string> FixedClasses = new HashSet<string> { "29", "104", "157", "196", SceneRootsClass };
        static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$");
        static readonly Regex LocalRef = new Regex(@"\{fileID: (-?\d+)\}");
        static readonly Regex GameObjectRef = new Regex(@"^\s*m_GameObject: \{fileID: (-?\d+)\}");
        static readonly Regex PrefabInstanceRef = new Regex(@"^\s*m_PrefabInstance: \{fileID: (-?\d+)\}");
        static readonly Regex ComponentRef = new Regex(@"^\s*- component: \{fileID: (-?\d+)\}");
        static readonly Regex ListRef = new Regex(@"^\s*- \{fileID: (-?\d+)\}");

        sealed class Doc
        {
            public string Class, Id, Suffix;
            public readonly List<string> Lines = new List<string>();
            public bool Fixed => FixedClasses.Contains(Class);
        }

        public static string Normalize(string yaml)
        {
            if (string.IsNullOrEmpty(yaml)) return yaml;
            string eol = yaml.Contains("\r\n") ? "\r\n" : "\n";
            var lineList = new List<string>(yaml.Replace("\r\n", "\n").Split('\n'));
            bool endsWithNewline = yaml.EndsWith("\n", StringComparison.Ordinal);
            if (endsWithNewline) lineList.RemoveAt(lineList.Count - 1);   // the empty piece after the final newline
            string[] lines = lineList.ToArray();

            var preamble = new List<string>();
            var docs = new List<Doc>();
            var byId = new Dictionary<string, Doc>();
            Doc cur = null;
            foreach (string line in lines)
            {
                Match m = Header.Match(line);
                if (m.Success)
                {
                    cur = new Doc { Class = m.Groups[1].Value, Id = m.Groups[2].Value, Suffix = m.Groups[3].Value };
                    docs.Add(cur);
                    byId[cur.Id] = cur;
                }
                else if (cur == null) preamble.Add(line);
                else cur.Lines.Add(line);
            }

            foreach (Doc d in docs)
            {
                if (d.Id.StartsWith("-", StringComparison.Ordinal))
                {
                    UnityEngine.Debug.LogWarning("SceneFileNormalizer: left the scene as Unity saved it (negative-id document " + d.Class + " &" + d.Id + ")");
                    return yaml;
                }
            }

            // New ids: walk the hierarchy from the scene roots, then sweep up anything not reached in old-id order.
            var map = new Dictionary<string, long>();
            long next = FirstId;
            void Assign(string id)
            {
                if (id == null || id == "0" || map.ContainsKey(id) || !byId.TryGetValue(id, out Doc assigned) || assigned.Fixed) return;
                map[id] = next++;
            }
            void Walk(string transformId)
            {
                if (transformId == null || map.ContainsKey(transformId) || !byId.TryGetValue(transformId, out Doc transform)) return;
                Assign(FirstRef(transform, PrefabInstanceRef));   // a stripped prefab transform: its PrefabInstance document comes first
                string goId = FirstRef(transform, GameObjectRef);
                Assign(goId);
                if (goId != null && byId.TryGetValue(goId, out Doc owner))
                    foreach (string c in AllRefs(owner, ComponentRef)) Assign(c);
                Assign(transformId);   // normally already assigned as one of the components
                foreach (string child in ChildRefs(transform)) Walk(child);
            }
            foreach (Doc d in docs)
                if (d.Class == SceneRootsClass)
                    foreach (string root in AllRefs(d, ListRef)) Walk(root);
            var rest = new List<Doc>();
            foreach (Doc d in docs) if (!d.Fixed && !map.ContainsKey(d.Id)) rest.Add(d);
            rest.Sort((a, b) => ulong.Parse(a.Id, CultureInfo.InvariantCulture).CompareTo(ulong.Parse(b.Id, CultureInfo.InvariantCulture)));
            foreach (Doc d in rest) Assign(d.Id);

            // Rewrite references and headers, then put the renumbered documents back in id order inside the slots they occupied.
            string Remap(Match m) => map.TryGetValue(m.Groups[1].Value, out long n) ? "{fileID: " + n.ToString(CultureInfo.InvariantCulture) + "}" : m.Value;
            var slots = new List<int>();
            var movable = new List<Doc>();
            for (int i = 0; i < docs.Count; i++)
            {
                Doc d = docs[i];
                for (int k = 0; k < d.Lines.Count; k++) d.Lines[k] = LocalRef.Replace(d.Lines[k], Remap);
                if (map.TryGetValue(d.Id, out long nid))
                {
                    d.Id = nid.ToString(CultureInfo.InvariantCulture);
                    slots.Add(i);
                    movable.Add(d);
                }
            }
            movable.Sort((a, b) => long.Parse(a.Id, CultureInfo.InvariantCulture).CompareTo(long.Parse(b.Id, CultureInfo.InvariantCulture)));
            for (int i = 0; i < slots.Count; i++) docs[slots[i]] = movable[i];

            var sb = new StringBuilder();
            foreach (string l in preamble) sb.Append(l).Append(eol);
            foreach (Doc d in docs)
            {
                sb.Append("--- !u!").Append(d.Class).Append(" &").Append(d.Id).Append(d.Suffix).Append(eol);
                foreach (string l in d.Lines) sb.Append(l).Append(eol);
            }
            string result = sb.ToString();
            return endsWithNewline ? result : result.Substring(0, Math.Max(0, result.Length - eol.Length));
        }

        static string FirstRef(Doc d, Regex r)
        {
            foreach (string l in d.Lines) { Match m = r.Match(l); if (m.Success) return m.Groups[1].Value; }
            return null;
        }

        static List<string> AllRefs(Doc d, Regex r)
        {
            var list = new List<string>();
            foreach (string l in d.Lines) { Match m = r.Match(l); if (m.Success) list.Add(m.Groups[1].Value); }
            return list;
        }

        // The "- {fileID: n}" entries under m_Children (and only there).
        static List<string> ChildRefs(Doc t)
        {
            var list = new List<string>();
            bool inChildren = false;
            foreach (string l in t.Lines)
            {
                if (l.StartsWith("  m_Children:", StringComparison.Ordinal)) { inChildren = true; continue; }
                if (!inChildren) continue;
                Match m = ListRef.Match(l);
                if (m.Success) list.Add(m.Groups[1].Value);
                else break;
            }
            return list;
        }
    }
}
