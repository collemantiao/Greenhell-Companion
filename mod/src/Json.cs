using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GreenHellCompanion
{
    /// <summary>
    /// Leitor/escritor de JSON mínimo. O JsonUtility do Unity não preenche tipos de um plugin carregado
    /// pelo BepInEx (o guia vinha com 0 fichas), então o mod usa este.
    /// Objetos viram Dictionary&lt;string, object&gt;, listas List&lt;object&gt;, números double.
    /// </summary>
    public static class Json
    {
        public static object Ler(string texto)
        {
            int i = 0;
            var v = Valor(texto, ref i);
            Espacos(texto, ref i);
            if (i < texto.Length) throw new System.FormatException($"JSON: sobra texto na posição {i}");
            return v;
        }

        static void Espacos(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Valor(string s, ref int i)
        {
            Espacos(s, ref i);
            if (i >= s.Length) throw new System.FormatException("JSON: fim inesperado");
            char c = s[i];
            if (c == '{') return Objeto(s, ref i);
            if (c == '[') return Lista(s, ref i);
            if (c == '"') return Texto(s, ref i);
            if (c == 't' && Pula(s, ref i, "true")) return true;
            if (c == 'f' && Pula(s, ref i, "false")) return false;
            if (c == 'n' && Pula(s, ref i, "null")) return null;
            return Numero(s, ref i);
        }

        static bool Pula(string s, ref int i, string palavra)
        {
            if (string.CompareOrdinal(s, i, palavra, 0, palavra.Length) != 0) throw new System.FormatException($"JSON: esperado {palavra} na posição {i}");
            i += palavra.Length;
            return true;
        }

        static Dictionary<string, object> Objeto(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Espacos(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Espacos(s, ref i);
                string k = Texto(s, ref i);
                Espacos(s, ref i);
                if (s[i] != ':') throw new System.FormatException($"JSON: esperado ':' na posição {i}");
                i++;
                d[k] = Valor(s, ref i);
                Espacos(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new System.FormatException($"JSON: esperado ',' ou '}}' na posição {i}");
            }
        }

        static List<object> Lista(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Espacos(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Valor(s, ref i));
                Espacos(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new System.FormatException($"JSON: esperado ',' ou ']' na posição {i}");
            }
        }

        static string Texto(string s, ref int i)
        {
            if (s[i] != '"') throw new System.FormatException($"JSON: esperado texto na posição {i}");
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;   // \" \\ \/
                }
            }
        }

        static double Numero(string s, ref int i)
        {
            int ini = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == ini) throw new System.FormatException($"JSON: valor inválido na posição {i}");
            return double.Parse(s.Substring(ini, i - ini), CultureInfo.InvariantCulture);
        }

        // ---------- Escrita ----------
        public static string Escrever(object v)
        {
            var sb = new StringBuilder();
            Escrever(sb, v, 0);
            return sb.ToString();
        }

        static void Escrever(StringBuilder sb, object v, int nivel)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string t: Aspas(sb, t); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> o:
                    sb.Append("{");
                    bool primeiro = true;
                    foreach (var kv in o)
                    {
                        if (!primeiro) sb.Append(",");
                        primeiro = false;
                        sb.Append('\n').Append(' ', (nivel + 1) * 2);
                        Aspas(sb, kv.Key);
                        sb.Append(": ");
                        Escrever(sb, kv.Value, nivel + 1);
                    }
                    if (!primeiro) sb.Append('\n').Append(' ', nivel * 2);
                    sb.Append("}");
                    break;
                case System.Collections.IEnumerable l:
                    sb.Append("[");
                    bool p = true;
                    foreach (var x in l)
                    {
                        if (!p) sb.Append(",");
                        p = false;
                        sb.Append('\n').Append(' ', (nivel + 1) * 2);
                        Escrever(sb, x, nivel + 1);
                    }
                    if (!p) sb.Append('\n').Append(' ', nivel * 2);
                    sb.Append("]");
                    break;
                default: Aspas(sb, v.ToString()); break;
            }
        }

        static void Aspas(StringBuilder sb, string t)
        {
            sb.Append('"');
            foreach (char c in t)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------- Leitura tipada ----------
        public static string S(this Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v != null ? v.ToString() : null;
        public static double N(this Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is double d ? d : 0;
        public static bool B(this Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is bool b && b;
        public static List<object> L(this Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
    }
}
