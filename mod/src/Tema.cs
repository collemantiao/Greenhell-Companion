using System.Text.RegularExpressions;
using UnityEngine;

namespace GreenHellCompanion
{
    /// <summary>
    /// Visual do jogo para os cartões do HUD: a fonte e a faixa de pincel do aviso "Caderno: Nova entrada"
    /// (HUDInfoLog). Se não estiverem disponíveis, usa uma pincelada desenhada por código.
    /// </summary>
    public static class Tema
    {
        static Font fonte;
        static Texture pincelJogo;
        static Color corPincelJogo = new Color(0, 0, 0, 1);
        static Texture2D pincelProprio;
        static float proximaBusca;

        /// <summary>Fonte do HUD do jogo, quando o HUD já existe.</summary>
        public static Font Fonte
        {
            get
            {
                Buscar();
                return fonte;
            }
        }

        static void Buscar()
        {
            if ((fonte != null && pincelJogo != null) || Time.unscaledTime < proximaBusca) return;
            proximaBusca = Time.unscaledTime + 2f;
            try
            {
                var info = HUDInfoLog.Get();
                if (info == null) return;
                if (fonte == null && info.m_Text != null && info.m_Text.font != null)
                {
                    fonte = info.m_Text.font;
                    Plugin.Log($"Fonte do jogo: {fonte.name}");
                }
                if (pincelJogo == null && info.m_BG != null && info.m_BG.texture != null)
                {
                    pincelJogo = info.m_BG.texture;
                    corPincelJogo = info.m_BG.color;
                    Plugin.Log($"Pincel do jogo: {pincelJogo.name} ({pincelJogo.width}x{pincelJogo.height})");
                }
            }
            catch (System.Exception e) { Plugin.Erro("tema do jogo", e); proximaBusca = float.MaxValue; }
        }

        public enum Lado { Esquerda, Direita, Centro }

        /// <summary>
        /// Faixa de pincel atrás de um cartão. 'lado' é onde o cartão encosta na tela: a parte mais escura fica
        /// desse lado e a pincelada se dissolve para o outro, além da área do texto (como no jogo).
        /// </summary>
        public static void Pincelada(Rect r, Lado lado)
        {
            if (Event.current.type != EventType.Repaint) return;
            Buscar();
            var antes = GUI.color;
            // no padrão (84%) fica com a mesma opacidade do aviso do jogo; o ajuste do F4 escala a partir daí
            float baseJogo = pincelJogo != null ? corPincelJogo.a : 0.6f;
            float alfa = Mathf.Clamp01(baseJogo * Plugin.Opacidade.Value / 0.84f) * antes.a;
            var cor = pincelJogo != null ? corPincelJogo : Color.black;
            GUI.color = new Color(cor.r, cor.g, cor.b, alfa);

            Texture t = pincelJogo != null ? pincelJogo : PincelProprio();
            // área maior que o texto: a pincelada sobra para cima/baixo e se desfaz para o lado de dentro da tela
            float extraV = r.height * 0.18f + 8, extraH = r.width * 0.35f;
            var area = new Rect(r.x - 10, r.y - extraV, r.width + 20, r.height + extraV * 2);
            switch (lado)
            {
                case Lado.Direita:   // denso à direita (como o aviso do caderno), some para a esquerda
                    area.xMin -= extraH;
                    GUI.DrawTextureWithTexCoords(area, t, new Rect(0, 0, 1, 1), true);
                    break;
                case Lado.Esquerda:  // espelhado: denso à esquerda
                    area.xMax += extraH;
                    GUI.DrawTextureWithTexCoords(area, t, new Rect(1, 0, -1, 1), true);
                    break;
                default:             // centro: duas metades espelhadas, densas no meio
                    area.xMin -= extraH / 2; area.xMax += extraH / 2;
                    var esq = new Rect(area.x, area.y, area.width / 2, area.height);
                    var dir = new Rect(area.center.x, area.y, area.width / 2, area.height);
                    GUI.DrawTextureWithTexCoords(esq, t, new Rect(0, 0, 1, 1), true);
                    GUI.DrawTextureWithTexCoords(dir, t, new Rect(1, 0, -1, 1), true);
                    break;
            }
            GUI.color = antes;
        }

        /// <summary>Pincelada desenhada por código: escura à direita, bordas irregulares e cerdas horizontais.</summary>
        public static Texture2D PincelProprio()
        {
            if (pincelProprio != null) return pincelProprio;
            const int w = 512, h = 128;
            pincelProprio = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                // bordas de cima e de baixo irregulares ao longo do traço
                float topo = 0.08f + 0.16f * Ruido(u * 5f, 1.3f) + 0.06f * Ruido(u * 23f, 7.1f);
                float base_ = 0.92f - 0.16f * Ruido(u * 6f, 4.2f) - 0.06f * Ruido(u * 21f, 2.6f);
                // some para a esquerda com fim de pincel seco (falhado); a ponta direita também se desfaz
                float fim = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0f, 0.6f, u + 0.1f * Ruido(u * 13f, 9.9f)))
                          * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.9f, 1f, u + 0.05f * Ruido(u * 17f, 5.5f))));
                for (int y = 0; y < h; y++)
                {
                    float v = y / (h - 1f);
                    float dentro = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(topo, topo + 0.12f, v)) *
                                   Mathf.SmoothStep(0, 1, Mathf.InverseLerp(base_, base_ - 0.12f, v));
                    float cerdas = 0.7f + 0.3f * Ruido(u * 2.2f, v * 38f) * (0.8f + 0.2f * Ruido(u * 9f, v * 90f));   // listras horizontais
                    float falhas = Mathf.Clamp01(1.25f - (1 - u) * 1.1f * (0.5f + Ruido(u * 40f, v * 30f)));
                    px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(dentro * fim * cerdas * Mathf.Lerp(falhas, 1, u)));
                }
            }
            pincelProprio.SetPixels(px);
            pincelProprio.Apply();
            return pincelProprio;
        }

        /// <summary>Sombra áspera e uniforme ao longo de uma barra: duas metades espelhadas do pincel, densas no meio.</summary>
        public static void SombraPincel(Rect r, float alfa)
        {
            if (Event.current.type != EventType.Repaint) return;
            var antes = GUI.color;
            GUI.color = new Color(0, 0, 0, alfa * antes.a);
            var t = PincelProprio();
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, r.width / 2, r.height), t, new Rect(0.25f, 0, 0.75f, 1), true);
            GUI.DrawTextureWithTexCoords(new Rect(r.center.x, r.y, r.width / 2, r.height), t, new Rect(1, 0, -0.75f, 1), true);
            GUI.color = antes;
        }

        static float Ruido(float x, float y) => Mathf.PerlinNoise(x + 13.7f, y + 3.1f);

        // ---------- Texto com sombra suave, para ler sobre a mata sem caixa ----------
        static readonly Regex Tags = new Regex("<[^>]+>");
        static GUIStyle sombra;
        static readonly Color CorSombra = new Color(0, 0, 0, 0.75f);

        public static void TextoComSombra(Rect r, string texto, GUIStyle estilo)
        {
            if (Event.current.type == EventType.Repaint)
            {
                if (sombra == null) sombra = new GUIStyle();
                sombra.font = estilo.font; sombra.fontSize = estilo.fontSize; sombra.fontStyle = estilo.fontStyle;
                sombra.wordWrap = estilo.wordWrap; sombra.alignment = estilo.alignment; sombra.richText = true;
                sombra.padding = estilo.padding; sombra.clipping = estilo.clipping;
                var antes = GUI.color;
                GUI.color = new Color(1, 1, 1, antes.a);
                sombra.normal.textColor = CorSombra;
                // mesmo texto sem as cores (mantém <b> para a largura bater)
                string semCor = Tags.Replace(texto, m => m.Value.StartsWith("<b") || m.Value.StartsWith("</b") ? m.Value : "");
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), semCor, sombra);
                GUI.color = antes;
            }
            GUI.Label(r, texto, estilo);
        }
    }
}
