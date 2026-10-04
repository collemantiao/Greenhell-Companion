using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>Busca nas fichas do guia sem sair do jogo.</summary>
    public class JanelaGuia : Janela
    {
        string busca = "", buscaFeita = null;
        List<Ficha> resultados = new List<Ficha>();
        Ficha atual;
        Vector2 rolLista, rolFicha;
        bool focar;

        static readonly Dictionary<string, string> Notas = new Dictionary<string, string>
        {
            { "melhor", Ok }, { "bom", Info }, { "emergencial", Aviso }, { "cuidado", Perigo },
        };
        static readonly Dictionary<string, string> NomeNota = new Dictionary<string, string>
        {
            { "melhor", "MELHOR" }, { "bom", "BOM" }, { "emergencial", "EMERGÊNCIA" }, { "cuidado", "CUIDADO" },
        };

        public void Alternar(string fichaInicial)
        {
            if (Aberta == this) { Fechar(); return; }
            if (Aberta != null) return;
            var f = Guia.Get(fichaInicial);
            if (f != null) { atual = f; rolFicha = Vector2.zero; }
            focar = true;
            Abrir();
        }

        void Buscar()
        {
            if (busca == buscaFeita) return;
            buscaFeita = busca;
            resultados = Guia.Buscar(busca).Take(80).ToList();
            rolLista = Vector2.zero;
            if (resultados.Count > 0 && (atual == null || !resultados.Contains(atual)) && busca.Trim().Length > 0)
            {
                atual = resultados[0];
                rolFicha = Vector2.zero;
            }
        }

        public override void Desenhar()
        {
            var r = Centro(0.6f, 0.76f);
            Fundo(r);
            if (EscPressionado()) { Fechar(); return; }

            float pad = Px(18), x = r.x + pad, w = r.width - pad * 2, y = r.y + pad;
            GUI.Label(new Rect(x, y, w, Px(32)), $"<b>Guia</b> {C(Destaque, "<b>Green Hell</b>")}", TituloGrande);
            if (GUI.Button(new Rect(r.xMax - pad - Px(80), y, Px(80), Px(26)), "Fechar", Botao)) { Fechar(); return; }
            if (GUI.Button(new Rect(r.xMax - pad - Px(80) - Px(8) - Px(120), y, Px(120), Px(26)), "Configurações", Botao)) { Plugin.Configuracoes.Abrir(); return; }
            y += Px(40);

            GUI.SetNextControlName("gh_busca");
            busca = GUI.TextField(new Rect(x, y, w, Px(34)), busca, 60, Campo);
            if (focar) { GUI.FocusControl("gh_busca"); focar = false; }
            if (string.IsNullOrEmpty(busca))
                GUI.Label(new Rect(x + Px(10), y + Px(8), w, Px(20)), C(Apagado, "Busque um bicho, ferimento, doença ou item: onça, febre, corda…"), Texto);
            y += Px(46);
            Buscar();

            float alturaCorpo = r.yMax - pad - y;
            float wl = w * 0.33f;
            DesenharLista(new Rect(x, y, wl, alturaCorpo));
            GUI.DrawTexture(new Rect(x + wl + Px(10), y, 1, alturaCorpo), Tex(Linha));
            DesenharFicha(new Rect(x + wl + Px(22), y, w - wl - Px(22), alturaCorpo));
        }

        void DesenharLista(Rect area)
        {
            if (resultados.Count == 0)
            {
                GUI.Label(area, "Nada encontrado. Tente outra palavra ou o nome em inglês.", Pequeno);
                return;
            }
            float lh = Px(26);
            var conteudo = new Rect(0, 0, area.width - Px(16), resultados.Count * lh);
            rolLista = GUI.BeginScrollView(area, rolLista, conteudo);
            for (int i = 0; i < resultados.Count; i++)
            {
                var f = resultados[i];
                string rot = $"{Esc(f.nome)}  {C(Apagado, "<size=" + Px(10) + ">" + Esc(f.categoria.ToUpperInvariant()) + "</size>")}";
                bool sel = f == atual;
                if (GUI.Toggle(new Rect(0, i * lh, conteudo.width, lh), sel, rot, BotaoLista) && !sel)
                {
                    atual = f;
                    rolFicha = Vector2.zero;
                }
            }
            GUI.EndScrollView();
        }

        void DesenharFicha(Rect area)
        {
            if (atual == null)
            {
                GUI.Label(area, "Escolha uma ficha na lista.", Pequeno);
                return;
            }
            float w = area.width - Px(18);
            var linhas = new List<(GUIStyle s, string t, float esp)>();
            void L(GUIStyle s, string t, float esp = 0) => linhas.Add((s, t, esp));

            string perigo = atual.perigo > 0 ? $"  ·  PERIGO {atual.perigo}/5" : "";
            L(Rotulo, Esc(atual.categoria.ToUpperInvariant()) + perigo);
            L(TituloGrande, $"<b>{Esc(atual.nome)}</b>", 2);
            if (!string.IsNullOrEmpty(atual.nomeEn)) L(Pequeno, $"<i>{Esc(atual.nomeEn)}</i>");
            L(Texto, Esc(atual.resumo), 8);
            foreach (var s in atual.secoes)
            {
                string cor = s.tipo == "alerta" ? Perigo : Destaque;
                L(Rotulo, C(cor, Esc(s.titulo.ToUpperInvariant())), 14);
                int n = 0;
                foreach (var it in s.itens)
                {
                    n++;
                    string marca = s.tipo == "armas" || s.tipo == "tratamento" ? C(Apagado, n + ".") : C(cor, "•");
                    string nota = it.nota != null && Notas.TryGetValue(it.nota, out var cn) ? "  " + C(cn, "<size=" + Px(10) + ">" + NomeNota[it.nota] + "</size>") : "";
                    L(Texto, $"{marca}  <b>{Esc(it.t)}</b>{nota}", 6);
                    if (!string.IsNullOrEmpty(it.d)) L(Pequeno, "     " + Esc(it.d), 1);
                }
            }
            var rel = atual.relacionados.Select(Guia.Get).Where(f => f != null).ToList();

            float altura = linhas.Sum(l => Px(l.esp) + l.s.CalcHeight(new GUIContent(l.t), w));
            float alturaRel = rel.Count > 0 ? Px(30) + Mathf.Ceil(rel.Count / 3f) * Px(30) : 0;
            var conteudo = new Rect(0, 0, w, altura + alturaRel + Px(10));
            rolFicha = GUI.BeginScrollView(area, rolFicha, conteudo);
            Imagens.Desenhar(Imagens.Get(atual.id), new Rect(w * 0.55f, 0, w * 0.45f, Px(150)), Plugin.OpacidadeImagem.Value * 0.8f);
            float y = 0;
            foreach (var l in linhas)
            {
                y += Px(l.esp);
                float h = l.s.CalcHeight(new GUIContent(l.t), w);
                GUI.Label(new Rect(0, y, w, h), l.t, l.s);
                y += h;
            }
            if (rel.Count > 0)
            {
                y += Px(14);
                GUI.Label(new Rect(0, y, w, Px(16)), "VEJA TAMBÉM", Rotulo);
                y += Px(18);
                float bw = (w - Px(12)) / 3;
                for (int i = 0; i < rel.Count; i++)
                {
                    var br = new Rect((i % 3) * (bw + Px(6)), y + (i / 3) * Px(30), bw, Px(26));
                    if (GUI.Button(br, Esc(rel[i].nome), Botao)) { atual = rel[i]; rolFicha = Vector2.zero; }
                }
            }
            GUI.EndScrollView();
        }
    }
}
