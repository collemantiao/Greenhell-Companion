using System.Collections.Generic;
using UnityEngine;

namespace GreenHellCompanion
{
    /// <summary>Cores, fontes e desenho de painéis (IMGUI). Tamanhos seguem a altura da tela.</summary>
    public static class Estilo
    {
        public static Color Painel => new Color(10 / 255f, 16 / 255f, 13 / 255f, Plugin.Opacidade.Value);
        public static readonly Color PainelForte = new Color32(10, 16, 13, 245);
        public static readonly Color Linha = new Color32(214, 228, 206, 40);
        public static readonly Color Escurece = new Color32(4, 8, 6, 160);
        public const string Tinta = "#eef3ea", Apagado = "#a9b8ac", Destaque = "#e8a93c",
            Ok = "#73cf98", Aviso = "#e7a547", Perigo = "#ff6b5d", Info = "#79c3dc";
        public static Color Cor(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static float E { get; private set; } = 1f;
        public static GUIStyle Texto, Pequeno, Rotulo, Titulo, TituloGrande, Mono, Botao, BotaoLista, Campo;

        static readonly Dictionary<Color, Texture2D> texturas = new Dictionary<Color, Texture2D>();
        static float escalaFeita = -1;
        static Font fonteFeita;

        public static Texture2D Tex(Color c)
        {
            if (texturas.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            texturas[c] = t;
            return t;
        }

        public static int Px(float v) => Mathf.RoundToInt(v * E);

        // ---------- Animação ----------
        /// <summary>Progresso 0→1 com desaceleração (ease-out cúbico) desde 'inicio'. Sem animações, sempre 1.</summary>
        public static float Entrada(float inicio, float duracao)
        {
            if (!Plugin.Animacoes.Value || duracao <= 0) return 1;
            float t = Mathf.Clamp01((Time.unscaledTime - inicio) / duracao);
            return 1 - Mathf.Pow(1 - t, 3);
        }

        /// <summary>Aproxima 'atual' de 'alvo' suavemente, independente do FPS.</summary>
        public static float Suavizar(float atual, float alvo, float velocidade = 6f)
        {
            if (!Plugin.Animacoes.Value) return alvo;
            return Mathf.Lerp(atual, alvo, 1 - Mathf.Exp(-velocidade * Time.unscaledDeltaTime));
        }

        /// <summary>Desenha algo com opacidade multiplicada (as cores e texturas respeitam GUI.color).</summary>
        public static void ComOpacidade(float alfa, System.Action desenhar)
        {
            if (alfa >= 0.999f) { desenhar(); return; }
            var antes = GUI.color;
            GUI.color = new Color(antes.r, antes.g, antes.b, antes.a * alfa);
            try { desenhar(); } finally { GUI.color = antes; }
        }

        public static void Preparar()
        {
            float e = Screen.height / 1080f * Plugin.Escala.Value;
            var fonte = Tema.Fonte;   // fonte do HUD do jogo (fica disponível depois que o HUD carrega)
            if (Mathf.Approximately(e, escalaFeita) && fonte == fonteFeita && Texto != null) return;
            escalaFeita = E = e;
            fonteFeita = fonte;

            GUIStyle Base(float tam, FontStyle fs = FontStyle.Normal, string cor = Tinta)
            {
                var s = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Px(tam), fontStyle = fs, richText = true, wordWrap = true,
                    padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                };
                if (fonte != null) s.font = fonte;
                s.normal.textColor = Cor(cor);
                return s;
            }
            Texto = Base(13);
            Pequeno = Base(11.5f, FontStyle.Normal, Apagado);
            Rotulo = Base(10.5f, FontStyle.Bold, Apagado);
            Titulo = Base(17, FontStyle.Bold);
            TituloGrande = Base(24, FontStyle.Bold);
            Mono = Base(11.5f, FontStyle.Normal, Apagado);
            Mono.wordWrap = false;

            int rb = Px(RaioBotao);
            Botao = new GUIStyle(GUI.skin.button) { fontSize = Px(12.5f), richText = true, padding = new RectOffset(Px(10), Px(10), Px(4), Px(4)), border = new RectOffset(rb, rb, rb, rb) };
            Botao.normal.background = Arredondada(new Color32(255, 255, 255, 18), null, rb);
            Botao.hover.background = Arredondada(new Color32(232, 169, 60, 60), null, rb);
            Botao.active.background = Arredondada(new Color32(232, 169, 60, 110), null, rb);
            Botao.normal.textColor = Botao.hover.textColor = Botao.active.textColor = Cor(Tinta);
            if (fonte != null) Botao.font = fonte;

            BotaoLista = new GUIStyle(Botao) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = Px(13) };
            BotaoLista.normal.background = Tex(Color.clear);
            BotaoLista.onNormal.background = Arredondada(new Color32(232, 169, 60, 45), null, rb);
            BotaoLista.onNormal.textColor = Cor(Tinta);

            int rc = Px(RaioCampo);
            Campo = new GUIStyle(GUI.skin.textField) { fontSize = Px(15), padding = new RectOffset(Px(10), Px(10), Px(6), Px(6)), border = new RectOffset(rc, rc, rc, rc) };
            Campo.normal.background = Campo.hover.background = Arredondada(new Color32(255, 255, 255, 14), Linha, rc);
            Campo.focused.background = Arredondada(new Color32(255, 255, 255, 18), new Color32(232, 169, 60, 150), rc);
            Campo.normal.textColor = Campo.focused.textColor = Campo.hover.textColor = Cor(Tinta);
            if (fonte != null) Campo.font = fonte;

            estilosCaixa.Clear();
        }

        // ---------- Cantos arredondados ----------
        public const float RaioJanela = 14, RaioLinha = 8, RaioBotao = 6, RaioCampo = 8;

        static readonly Dictionary<string, Texture2D> arredondadas = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Texture2D, GUIStyle> estilosCaixa = new Dictionary<Texture2D, GUIStyle>();

        /// <summary>
        /// Textura pequena com cantos arredondados e borda de 1 px opcional, para ser esticada em 9 partes
        /// (os cantos ficam do mesmo tamanho em qualquer largura).
        /// </summary>
        public static Texture2D Arredondada(Color fundo, Color? borda, int raio)
        {
            raio = Mathf.Max(2, raio);
            string chave = $"{(Color32)fundo}|{(borda.HasValue ? ((Color32)borda.Value).ToString() : "-")}|{raio}";
            if (arredondadas.TryGetValue(chave, out var t) && t != null) return t;

            int s = raio * 2 + 3;
            t = new Texture2D(s, s, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s];
            float meio = s / 2f, metadeInterna = meio - raio;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // distância com sinal até a borda do retângulo arredondado (negativa = dentro)
                    float qx = Mathf.Abs(x + 0.5f - meio) - metadeInterna;
                    float qy = Mathf.Abs(y + 0.5f - meio) - metadeInterna;
                    float fora = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
                    float d = fora + Mathf.Min(Mathf.Max(qx, qy), 0) - raio;
                    float cobertura = Mathf.Clamp01(0.5f - d);
                    var c = fundo;
                    if (borda.HasValue)
                    {
                        float naBorda = Mathf.Clamp01(1f - Mathf.Abs(d + 0.5f)) * borda.Value.a;
                        c = new Color(
                            Mathf.Lerp(fundo.r, borda.Value.r, naBorda),
                            Mathf.Lerp(fundo.g, borda.Value.g, naBorda),
                            Mathf.Lerp(fundo.b, borda.Value.b, naBorda),
                            Mathf.Max(fundo.a, naBorda));
                    }
                    c.a *= cobertura;
                    px[y * s + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            arredondadas[chave] = t;
            return t;
        }

        /// <summary>Caixa com cantos arredondados e borda clara, como as janelas do mod.</summary>
        public static void CaixaArredondada(Rect r, Color fundo, float raio, bool borda = true)
        {
            if (Event.current.type != EventType.Repaint) return;
            int rp = Mathf.Min(Px(raio), Mathf.FloorToInt(Mathf.Min(r.width, r.height) / 2) - 1);
            var tex = Arredondada(fundo, borda ? Linha : (Color?)null, rp);
            if (!estilosCaixa.TryGetValue(tex, out var st))
            {
                int b = (tex.width - 1) / 2;
                st = new GUIStyle { border = new RectOffset(b, b, b, b) };
                st.normal.background = tex;
                estilosCaixa[tex] = st;
            }
            st.Draw(r, false, false, false, false);
        }

        public static void Caixa(Rect r, Color fundo)
        {
            GUI.DrawTexture(r, Tex(fundo));
            var l = Tex(Linha);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), l);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), l);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), l);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), l);
        }

        /// <summary>Desenha um Sprite do jogo (pode estar num atlas) tingido com a cor.</summary>
        public static void DesenharSprite(Sprite s, Rect r, Color cor)
        {
            if (s == null || s.texture == null) return;
            var t = s.texture;
            Rect uv;
            try
            {
                var tr = s.textureRect;
                uv = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);
            }
            catch { uv = new Rect(0, 0, 1, 1); }
            var antes = GUI.color;
            GUI.color = new Color(cor.r, cor.g, cor.b, cor.a * antes.a);
            GUI.DrawTextureWithTexCoords(r, t, uv, true);
            GUI.color = antes;
        }

        public static string C(string hex, string txt) => $"<color={hex}>{txt}</color>";
        public static string Esc(string s) => s?.Replace("<", "‹").Replace(">", "›") ?? "";
    }

    /// <summary>Painel montado em duas passadas: mede as linhas, desenha o fundo e depois o texto.</summary>
    public class Bloco
    {
        struct L { public GUIStyle s; public string t; public float rec, esp; public bool barra; public float frac; public string corBarra; }
        readonly List<L> linhas = new List<L>();
        public string Faixa;      // cor da tarja à esquerda (opcional)
        public Texture2D Imagem;  // silhueta no canto superior direito (opcional)
        public Tema.Lado Lado = Tema.Lado.Esquerda;   // lado da tela em que o cartão encosta (para a pincelada)

        public Bloco Add(GUIStyle s, string t, float recuo = 0, float espacoAntes = 0)
        { linhas.Add(new L { s = s, t = t, rec = recuo, esp = espacoAntes }); return this; }

        public Bloco Barra(float frac, string cor)
        { linhas.Add(new L { barra = true, frac = Mathf.Clamp01(frac), corBarra = cor, esp = 2 }); return this; }

        /// <summary>Largura que o conteúdo pede (sem quebrar linha), entre um mínimo e o máximo dado.</summary>
        public float LarguraIdeal(float maximo)
        {
            float pad = Estilo.Px(10), faixa = Faixa != null && !Plugin.CartoesPincel.Value ? Estilo.Px(5) : 0, w = 0;
            foreach (var l in linhas)
                if (!l.barra) w = Mathf.Max(w, l.s.CalcSize(new GUIContent(l.t)).x + Estilo.Px(l.rec));
            if (Imagem != null) w += Estilo.Px(40);   // espaço para a silhueta não cobrir o texto
            return Mathf.Clamp(w + pad * 2 + faixa + 2, Estilo.Px(200), maximo);
        }

        public float Altura(float largura)
        {
            float pad = Estilo.Px(10), h = pad * 2;
            foreach (var l in linhas)
                h += Estilo.Px(l.esp) + (l.barra ? Estilo.Px(4) : l.s.CalcHeight(new GUIContent(l.t), largura - pad * 2 - Estilo.Px(l.rec) - (Faixa != null ? Estilo.Px(5) : 0)));
            return h;
        }

        public void Desenhar(Rect r, Color fundo)
        {
            bool pincel = Plugin.CartoesPincel.Value;
            if (pincel) Tema.Pincelada(r, Lado);   // faixa de pincel como nos avisos do jogo
            else Estilo.Caixa(r, fundo);
            float pad = Estilo.Px(10), faixa = Faixa != null ? Estilo.Px(5) : 0;
            if (Faixa != null && !pincel) GUI.DrawTexture(new Rect(r.x, r.y, faixa, r.height), Estilo.Tex(Estilo.Cor(Faixa)));
            if (Imagem != null) Imagens.Desenhar(Imagem, new Rect(r.xMax - pad - r.width * 0.42f, r.y + pad * 0.6f, r.width * 0.42f, Estilo.Px(64)), Plugin.OpacidadeImagem.Value);
            float x = r.x + pad + faixa, y = r.y + pad, w = r.width - pad * 2 - faixa;
            foreach (var l in linhas)
            {
                y += Estilo.Px(l.esp);
                if (l.barra)
                {
                    float bh = Estilo.Px(4);
                    GUI.DrawTexture(new Rect(x, y, w, bh), Estilo.Tex(new Color32(255, 255, 255, 26)));
                    GUI.DrawTexture(new Rect(x, y, w * l.frac, bh), Estilo.Tex(Estilo.Cor(l.corBarra)));
                    y += bh;
                    continue;
                }
                float rec = Estilo.Px(l.rec);
                var c = new GUIContent(l.t);
                float h = l.s.CalcHeight(c, w - rec);
                if (pincel) Tema.TextoComSombra(new Rect(x + rec, y, w - rec, h), l.t, l.s);
                else GUI.Label(new Rect(x + rec, y, w - rec, h), c, l.s);
                y += h;
            }
        }
    }

    /// <summary>Janela que toma o mouse e o teclado do jogo enquanto está aberta (só uma por vez).</summary>
    public abstract class Janela
    {
        public static Janela Aberta { get; private set; }
        public static int QuadroFechamento = -1;
        static bool textoAntes;
        float abertaEm;

        /// <summary>Desenha a janela aberta com a entrada animada: fade e leve crescimento a partir do centro.</summary>
        public static void DesenharAnimada()
        {
            var j = Aberta;
            if (j == null) return;
            float e = Estilo.Entrada(j.abertaEm, 0.18f);
            var m = GUI.matrix;
            if (e < 1)
            {
                float esc = 0.96f + 0.04f * e;
                GUIUtility.ScaleAroundPivot(new Vector2(esc, esc), new Vector2(Screen.width / 2f, Screen.height / 2f));
            }
            try { Estilo.ComOpacidade(e, j.Desenhar); }
            finally { GUI.matrix = m; }
        }

        public abstract void Desenhar();
        protected virtual void AoFechar() { }

        public void Abrir()
        {
            if (Aberta == this) return;
            if (Aberta != null) Fechar();
            Aberta = this;
            abertaEm = Time.unscaledTime;
            var p = Player.Get();
            p.BlockMoves();
            p.BlockRotation();
            CursorManager.Get().ShowCursor(true, false);
            textoAntes = InputsManager.Get().m_TextInputActive;
            InputsManager.Get().m_TextInputActive = true;   // impede que as teclas acionem ações do jogo
        }

        public static void Fechar()
        {
            if (Aberta == null) return;
            var j = Aberta;
            Aberta = null;
            QuadroFechamento = Time.frameCount;
            try
            {
                var p = Player.Get();
                if (p != null) { p.UnblockMoves(); p.UnblockRotation(); }
                CursorManager.Get()?.ShowCursor(false);
                var im = InputsManager.Get();
                if (im != null) { im.m_TextInputActive = textoAntes; im.m_OmitMouseUp = true; }
            }
            catch (System.Exception e) { Plugin.Erro("fechar janela", e); }
            j.AoFechar();
        }

        protected static Rect Centro(float larguraFrac, float alturaFrac)
        {
            float w = Screen.width * larguraFrac, h = Screen.height * alturaFrac;
            return new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        }

        protected static void Fundo(Rect r)
        {
            // um pouco maior que a tela para continuar cobrindo tudo durante a animação de escala
            GUI.DrawTexture(new Rect(-Screen.width * 0.05f, -Screen.height * 0.05f, Screen.width * 1.1f, Screen.height * 1.1f), Estilo.Tex(Estilo.Escurece));
            Estilo.CaixaArredondada(r, Estilo.PainelForte, Estilo.RaioJanela);
        }

        /// <summary>Esc dentro de um campo de texto chega pelo OnGUI, não pelo Update.</summary>
        protected static bool EscPressionado()
        {
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) { ev.Use(); return true; }
            return false;
        }
    }
}
