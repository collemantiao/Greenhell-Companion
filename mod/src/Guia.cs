using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GreenHellCompanion
{
    // Mesmo formato de dados/*.json do site (montar-dados.mjs gera Data/guia.json).
    [Serializable] public class Item { public string t; public string d; public string nota; }
    [Serializable] public class Secao { public string titulo; public string tipo; public List<Item> itens = new List<Item>(); }
    [Serializable] public class Ficha
    {
        public string id, nome, nomeEn, categoria, resumo;
        public int perigo;
        public List<string> aliases = new List<string>();
        public List<Secao> secoes = new List<Secao>();
        public List<string> relacionados = new List<string>();
    }

    /// <summary>Base de fichas e busca (porta da busca do site).</summary>
    public static class Guia
    {
        public static List<Ficha> Fichas { get; private set; } = new List<Ficha>();
        static readonly Dictionary<string, Ficha> porId = new Dictionary<string, Ficha>();
        static readonly List<Indexada> indice = new List<Indexada>();

        class Indexada
        {
            public Ficha f;
            public string id, titulo, nome, resumo, corpo, cat;
            public string[] alias;
        }

        static readonly HashSet<string> Vazias = new HashSet<string>(
            "a o as os de da do das dos e em no na nos nas um uma para pra por com sem como que qual quais matar mato mata tratar trato curar curo cura usar uso fazer faco contra pegar achar onde melhor melhores the how to kill"
                .Split(' '));

        public static void Carregar(string arquivo)
        {
            var raiz = (Dictionary<string, object>)Json.Ler(File.ReadAllText(arquivo, Encoding.UTF8));
            Fichas = raiz.L("fichas").OfType<Dictionary<string, object>>().Select(o => new Ficha
            {
                id = o.S("id"), nome = o.S("nome"), nomeEn = o.S("nomeEn"), categoria = o.S("categoria") ?? "", resumo = o.S("resumo") ?? "",
                perigo = (int)o.N("perigo"),
                aliases = o.L("aliases").Select(a => a?.ToString()).Where(a => a != null).ToList(),
                relacionados = o.L("relacionados").Select(a => a?.ToString()).Where(a => a != null).ToList(),
                secoes = o.L("secoes").OfType<Dictionary<string, object>>().Select(s => new Secao
                {
                    titulo = s.S("titulo") ?? "", tipo = s.S("tipo") ?? "dicas",
                    itens = s.L("itens").OfType<Dictionary<string, object>>().Select(i => new Item { t = i.S("t") ?? "", d = i.S("d"), nota = i.S("nota") }).ToList(),
                }).ToList(),
            }).Where(f => f.id != null && f.nome != null).ToList();
            porId.Clear();
            indice.Clear();
            foreach (var f in Fichas)
            {
                porId[f.id] = f;
                var corpo = new StringBuilder();
                foreach (var s in f.secoes)
                {
                    corpo.Append(s.titulo).Append(' ');
                    foreach (var i in s.itens) corpo.Append(i.t).Append(' ').Append(i.d).Append(' ');
                }
                indice.Add(new Indexada
                {
                    f = f,
                    id = Norm(f.id),
                    titulo = Norm(f.nome),
                    nome = Norm(f.nome + " " + f.nomeEn),
                    alias = f.aliases.Select(Norm).ToArray(),
                    resumo = Norm(f.resumo),
                    corpo = Norm(corpo.ToString()),
                    cat = Norm(f.categoria),
                });
            }
        }

        public static Ficha Get(string id) => id != null && porId.TryGetValue(id, out var f) ? f : null;

        /// <summary>minúsculas, sem acento, só letras e números separados por espaço.</summary>
        public static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool espaco = true;
            foreach (char c in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) { sb.Append(c); espaco = false; }
                else if (!espaco) { sb.Append(' '); espaco = true; }
            }
            return sb.ToString().Trim();
        }

        static int Pontuar(Indexada x, IEnumerable<string> tokens)
        {
            int total = 0;
            foreach (var t in tokens)
            {
                int s = 0;
                string plural = t.Length > 3 && t.EndsWith("s") ? t.Substring(0, t.Length - 1) : null;
                if (x.nome == t || x.alias.Contains(t)) s = 100;
                else if (x.nome.Split(' ').Any(p => p.StartsWith(t)) || x.alias.Any(a => a.Split(' ').Any(p => p.StartsWith(t)))) s = 60;
                else if (plural != null && (x.nome.Contains(plural) || x.alias.Any(a => a.Contains(plural)))) s = 50;
                else if (x.nome.Contains(t) || x.alias.Any(a => a.Contains(t))) s = 40;
                else if (x.cat.Contains(t)) s = 20;
                else if (x.resumo.Contains(t)) s = 14;
                else if (x.corpo.Contains(t)) s = 6;
                if (s == 0) return 0;
                total += s;
            }
            return total;
        }

        public static List<Ficha> Buscar(string q)
        {
            var todos = Norm(q).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (todos.Length == 0) return Fichas.OrderBy(f => f.categoria).ThenBy(f => f.nome).ToList();
            var uteis = todos.Where(t => !Vazias.Contains(t)).ToArray();
            var tokens = uteis.Length > 0 ? uteis : todos;
            var frase = string.Join(" ", todos);

            var r = indice
                .Select(x => new
                {
                    x.f,
                    s = (x.titulo == frase || x.id == frase ? 1500 : x.alias.Contains(frase) ? 1000 : 0) + Pontuar(x, tokens)
                })
                .Where(r0 => r0.s > 0).ToList();
            if (r.Count == 0 && tokens.Length > 1)
                r = indice.Select(x => new { x.f, s = tokens.Max(t => Pontuar(x, new[] { t })) }).Where(r0 => r0.s > 0).ToList();
            return r.OrderByDescending(r0 => r0.s).ThenBy(r0 => r0.f.nome).Select(r0 => r0.f).ToList();
        }
    }
}
