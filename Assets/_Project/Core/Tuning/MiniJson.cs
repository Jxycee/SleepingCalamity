// Core/Tuning/MiniJson.cs: a small strict JSON reader for the tuning files (Core cannot reference Newtonsoft). Pure C#, no engine.
// Output: Dictionary<string, object> (objects), List<object> (arrays), double, string, bool, null. Throws FormatException on bad input.
// Authority: none (pure parsing).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SleepingCalamity.Core.Tuning
{
    public static class MiniJson
    {
        const int MaxDepth = 32;

        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("null json");
            int i = 0;
            if (json.Length > 0 && json[0] == '﻿') i = 1;   // BOM
            object v = Value(json, ref i, 0);
            Skip(json, ref i);
            if (i != json.Length) throw new FormatException("trailing characters at " + i);
            return v;
        }

        static void Skip(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++; }

        static object Value(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("nested too deep");
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            if (c == '{') return Obj(s, ref i, depth);
            if (c == '[') return Arr(s, ref i, depth);
            if (c == '"') return Str(s, ref i);
            if (Lit(s, ref i, "true")) return true;
            if (Lit(s, ref i, "false")) return false;
            if (Lit(s, ref i, "null")) return null;
            return Num(s, ref i);
        }

        static bool Lit(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static object Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && ("+-0123456789.eE".IndexOf(s[i]) >= 0)) i++;
            if (i == start || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                throw new FormatException("bad value at " + start);
            return d;
        }

        static string Str(string s, ref int i)
        {
            i++;   // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("unterminated string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("unterminated escape");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp)) throw new FormatException("bad \\u escape");
                        sb.Append((char)cp); i += 4; break;
                    default: throw new FormatException("bad escape");
                }
            }
        }

        static List<object> Arr(string s, ref int i, int depth)
        {
            i++;
            var list = new List<object>();
            Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(Value(s, ref i, depth + 1));
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("expected , or ] at " + i);
            }
        }

        static Dictionary<string, object> Obj(string s, ref int i, int depth)
        {
            i++;
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return map; }
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("expected key at " + i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected : at " + i);
                i++;
                map[key] = Value(s, ref i, depth + 1);
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return map; }
                throw new FormatException("expected , or } at " + i);
            }
        }
    }
}
