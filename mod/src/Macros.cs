using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>
    /// Macroelementos no estilo da HUD do jogo: ícone num círculo + barra fina, sem cartão de fundo,
    /// em duas linhas logo acima da vida e da energia. Mesma ordem do relógio:
    /// proteína | gordura (em cima) e carboidrato | água (embaixo). Cores e ícones do próprio jogo.
    /// </summary>
    public class Macros
    {
        enum Q { Proteina, Gordura, Carbo, Agua }
        static readonly IconColors.Icon[] Icones = { IconColors.Icon.Proteins, IconColors.Icon.Fat, IconColors.Icon.Carbo, IconColors.Icon.Hydration };
        // Cores do relógio, caso o IconColors do jogo ainda não esteja pronto.
        static readonly Color[] CoresPadrao = { new Color32(229, 100, 60, 255), new Color32(230, 194, 30, 255), new Color32(61, 190, 78, 255), new Color32(91, 192, 224, 255) };

        readonly float[] frac = new float[4];
        readonly bool[] critico = new bool[4];
        Color[] cores;
        float proxima;
        static Texture2D circulo;

        public void Atualizar()
        {
            if (!Plugin.MostrarMacros.Value || Time.time < proxima) return;
            proxima = Time.time + 0.25f;
            var c = PlayerConditionModule.Get();
            if (c == null) return;
            frac[(int)Q.Proteina] = Div(c.GetNutritionProtein(), c.GetMaxNutritionProtein());
            frac[(int)Q.Gordura] = Div(c.GetNutritionFat(), c.GetMaxNutritionFat());
            frac[(int)Q.Carbo] = Div(c.GetNutritionCarbo(), c.GetMaxNutritionCarbo());
            frac[(int)Q.Agua] = Div(c.GetHydration(), c.GetMaxHydration());
            critico[(int)Q.Proteina] = c.IsNutritionProteinsCriticalLevel();
            critico[(int)Q.Gordura] = c.IsNutritionFatCriticalLevel();
            critico[(int)Q.Carbo] = c.IsNutritionCarboCriticalLevel();
            critico[(int)Q.Agua] = c.IsHydrationCriticalLevel();
        }

        static float Div(float v, float max) => max > 0 ? Mathf.Clamp01(v / max) : 0;

        Color CorDe(int i)
        {
            if (cores == null)
            {
                cores = new Color[4];
                for (int k = 0; k < 4; k++)
                {
                    try { cores[k] = IconColors.GetColor(Icones[k]); } catch { cores[k] = CoresPadrao[k]; }
                    if (cores[k].a < 0.1f) cores[k] = CoresPadrao[k];
                    cores[k].a = 1;
                }
            }
            return cores[i];
        }

        /// <summary>Contorno de círculo branco com antisserrilhado, como os ícones da HUD.</summary>
        static Texture2D Circulo()
        {
            if (circulo != null) return circulo;
            const int n = 64;
            circulo = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            float c = (n - 1) / 2f, r = n / 2f - 2f, esp = 3.2f;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Abs(new Vector2(x - c, y - c).magnitude - r);
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(esp / 2 - d + 0.5f));
                }
            circulo.SetPixels(px);
            circulo.Apply();
            return circulo;
        }

        static Sprite Icone(int i)
        {
            HUDMessages h = null;
            try { h = HUDMessages.Get(); } catch { }
            if (h == null) return null;
            switch ((Q)i)
            {
                case Q.Proteina: return h.m_ProteinsIcon;
                case Q.Gordura: return h.m_FatIcon;
                case Q.Carbo: return h.m_CarboIcon;
                default: return h.m_HydrationIcon;
            }
        }

        // Medidas da HUD do jogo em 1080p (ela escala com a altura da tela):
        // ícones pequenos centrados em x ≈ 80, barras de x ≈ 95 até 330, linhas a cada 27 px, vida em y ≈ 1003.
        const float IconeX = 70, Fim = 330, Coluna = 10, LinhaVida = 1003, Passo = 27, TamIcone = 20, Espessura = 4;

        public void Desenhar()
        {
            if (!Plugin.MostrarMacros.Value) return;
            float h = Screen.height / 1080f;
            float larguraColuna = (Fim - IconeX - Coluna) / 2f;
            float pulso = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 3f));
            var vermelho = Cor(Perigo);
            var sombra = new Color(0, 0, 0, 0.45f);

            for (int i = 0; i < 4; i++)
            {
                int coluna = i % 2, linha = i / 2;                       // proteína/gordura em cima, carbo/água embaixo
                float x0 = (IconeX + coluna * (larguraColuna + Coluna)) * h;
                float cy = (LinhaVida - Passo * (2 - linha)) * h;        // duas linhas acima da vida
                float ic = TamIcone * h;

                var cor = CorDe(i);
                var corAnel = critico[i] ? new Color(vermelho.r, vermelho.g, vermelho.b, pulso) : new Color(1, 1, 1, 0.9f);

                // círculo + ícone do jogo
                var ri = new Rect(x0, cy - ic / 2, ic, ic);
                var antes = GUI.color;
                GUI.color = sombra;
                GUI.DrawTexture(new Rect(ri.x + 1, ri.y + 1, ri.width, ri.height), Circulo());
                GUI.color = corAnel;
                GUI.DrawTexture(ri, Circulo());
                GUI.color = antes;
                var s = Icone(i);
                float m = ic * 0.22f;
                if (s != null) DesenharSprite(s, new Rect(ri.x + m, ri.y + m, ic - 2 * m, ic - 2 * m), critico[i] ? corAnel : cor);

                // barra fina com sombra, trilho claro e preenchimento na cor do macroelemento
                float bx = x0 + ic + 4 * h, bw = (x0 + larguraColuna * h) - bx, bh = Mathf.Max(2, Espessura * h);
                var rb = new Rect(bx, cy - bh / 2, bw, bh);
                GUI.DrawTexture(new Rect(rb.x + 1, rb.y + 1, rb.width, rb.height), Tex(sombra));
                GUI.DrawTexture(rb, Tex(new Color(1, 1, 1, 0.18f)));
                var corBarra = critico[i] ? new Color(vermelho.r, vermelho.g, vermelho.b, pulso) : cor;
                GUI.DrawTexture(new Rect(rb.x, rb.y, rb.width * frac[i], rb.height), Tex(corBarra));
            }
        }
    }
}
