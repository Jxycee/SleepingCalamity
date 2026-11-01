// SceneFileNormalizer: makes a freshly saved .unity file byte-stable. Unity gives every new GameObject and component a random local fileID
// (a different base per object, per run), so rebuilding a scene rewrites the file even when nothing changed. Normalize renumbers those ids
// from the scene hierarchy (SceneRoots order, then each object's component order, then its children), renames every {fileID: n} reference
// to match, and writes the documents back in new-id order. The settings documents (fixed small ids and SceneRoots) are left alone, and so is
// any reference that carries a guid (an asset). Pure string code; ProjectBuilder's scene saves call it through BuilderUtil.SaveScene.
// PrefabInstance (class 1001) documents and the stripped documents that stand in for a prefab's objects are renumbered like any other local object: the
// instance is numbered just before the stripped transform that places it in the hierarchy (everything a scene we build contains has plain local ids; a
// stripped id Unity derived from an asset would be negative or XOR-ed, and a scene with a negative document id is returned unchanged with a warning).
// Walk order inside a prefab instance: the instance's added objects in their saved order (m_AddedGameObjects, m_AddedComponents), then its other stripped
// documents (stripped GameObjects and components) by their source-object reference, then nested instances parented to it. Documents the walk cannot reach are
// swept up in old-id order, except stripped ones, which sort by their instance's new id and source object so no stripped document depends on an old id.
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
        static readonly Regex TransformParentRef = new Regex(@"^\s*m_TransformParent: \{fileID: (-?\d+)\}");
        static readonly Regex AddedObjectRef = new Regex(@"^\s*addedObject: \{fileID: (-?\d+)\}");
        static readonly Regex SourceObjectRef = new Regex(@"^\s*m_CorrespondingSourceObject: (.*)$");
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
                string instanceId = FirstRef(transform, PrefabInstanceRef);
                if (instanceId != null && byId.TryGetValue(instanceId, out Doc instance)) WalkInstance(instance);
            }
            void WalkInstance(Doc instance)
            {
                foreach (string added in AllRefs(instance, AddedObjectRef))
                {
                    if (!byId.TryGetValue(added, out Doc a)) continue;
                    if (a.Class == "4" || a.Class == "224") { Walk(added); continue; }   // an added child object: its transform leads
                    string owner = FirstRef(a, GameObjectRef);
                    if (owner != null && byId.TryGetValue(owner, out Doc ownerDoc) && ownerDoc.Suffix.Length > 0) Assign(owner);   // component on a stripped GameObject
                    Assign(added);
                }
                var stripped = new List<Doc>();
                foreach (Doc d in docs) if (d.Suffix.Length > 0 && FirstRef(d, PrefabInstanceRef) == instance.Id) stripped.Add(d);
                stripped.Sort((x, y) => string.CompareOrdinal(StrippedKey(x), StrippedKey(y)));
                foreach (Doc d in stripped) if (d.Class != "4" && d.Class != "224") Assign(d.Id);
                // Nested prefab instances placed under this instance's objects: their stripped transforms, in a key order independent of old ids.
                var nested = new List<Doc>();
                foreach (Doc d in docs)
                    if (d.Class == "1001" && d != instance)
                    {
                        string parent = FirstRef(d, TransformParentRef);
                        if (parent != null && byId.TryGetValue(parent, out Doc pd) && pd.Suffix.Length > 0 && FirstRef(pd, PrefabInstanceRef) == instance.Id) nested.Add(d);
                    }
                nested.Sort((x, y) => string.CompareOrdinal(string.Join("\n", x.Lines), string.Join("\n", y.Lines)));
                foreach (Doc n in nested)
                    foreach (Doc d in docs)
                        if (d.Suffix.Length > 0 && (d.Class == "4" || d.Class == "224") && FirstRef(d, PrefabInstanceRef) == n.Id) { Walk(d.Id); break; }
            }
            string StrippedKey(Doc d)
            {
                string src = FirstRef(d, SourceObjectRef) ?? "";
                return d.Class + "|" + src + "|" + FirstRef(d, GameObjectRef);
            }
            foreach (Doc d in docs)
                if (d.Class == SceneRootsClass)
                    foreach (string root in AllRefs(d, ListRef)) Walk(root);
            // Objects embedded in the scene but outside the hierarchy (meshes and materials a builder created with new): Unity writes them in an order that changes
            // from run to run, so a second build swapped their contents between ids. Number them in the order the hierarchy first references them instead:
            // documents in new-id order, each one's references in line order, depth first.
            void AssignReferenced(Doc from)
            {
                foreach (string line in from.Lines)
                    foreach (Match m in LocalRef.Matches(line))
                    {
                        string id = m.Groups[1].Value;
                        if (id == "0" || map.ContainsKey(id) || !byId.TryGetValue(id, out Doc target) || target.Fixed || target.Suffix.Length > 0) continue;
                        Assign(id);
                        AssignReferenced(target);
                    }
            }
            var walked = new List<KeyValuePair<long, Doc>>();
            foreach (Doc d in docs) if (map.TryGetValue(d.Id, out long walkedId)) walked.Add(new KeyValuePair<long, Doc>(walkedId, d));
            walked.Sort((x, y) => x.Key.CompareTo(y.Key));
            foreach (var kv in walked) AssignReferenced(kv.Value);
            var rest = new List<Doc>();
            foreach (Doc d in docs) if (!d.Fixed && !map.ContainsKey(d.Id)) rest.Add(d);
            // Stripped documents the walk did not reach go last, keyed by their instance's new id and their source object; everything else in old-id order.
            string RestKey(Doc d)
            {
                string inst = FirstRef(d, PrefabInstanceRef);
                long ni = inst != null && map.TryGetValue(inst, out long m2) ? m2 : long.MaxValue;
                return ni.ToString("D20", CultureInfo.InvariantCulture) + "|" + StrippedKey(d);
            }
            rest.Sort((a, b) =>
            {
                bool sa = a.Suffix.Length > 0, sb2 = b.Suffix.Length > 0;
                if (sa != sb2) return sa ? 1 : -1;
                if (sa) return string.CompareOrdinal(RestKey(a), RestKey(b));
                return ulong.Parse(a.Id, CultureInfo.InvariantCulture).CompareTo(ulong.Parse(b.Id, CultureInfo.InvariantCulture));
            });
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
