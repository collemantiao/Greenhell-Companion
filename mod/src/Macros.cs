using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>
    /// Mostrador de macroelementos no canto inferior esquerdo, acima da vida/energia do jogo.
    /// Mesmo desenho do relógio: proteínas (cima-esq.), gorduras (cima-dir.), carboidratos (baixo-esq.), água (baixo-dir.),
    /// com as cores e os ícones do próprio jogo.
    /// </summary>
    public class Macros
    {
        enum Q { Proteina, Gordura, Carbo, Agua }
        static readonly string[] Nomes = { "Proteína", "Gordura", "Carboidrato", "Água" };
        static readonly IconColors.Icon[] Icones = { IconColors.Icon.Proteins, IconColors.Icon.Fat, IconColors.Icon.Carbo, IconColors.Icon.Hydration };
        // Cores do relógio, caso o IconColors do jogo ainda não esteja pronto.
        static readonly Color[] CoresPadrao = { new Color32(229, 100, 60, 255), new Color32(230, 194, 30, 255), new Color32(61, 190, 78, 255), new Color32(91, 192, 224, 255) };

        readonly float[] frac = new float[4];
        readonly float[] fracDesenhada = { -1, -1, -1, -1 };
        readonly bool[] critico = new bool[4];
        Color[] cores;
        Texture2D anel;
        float proxima;
        const int N = 192;   // resolução da textura do anel

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

        // ---------- Anel (textura gerada quando os valores mudam) ----------
        Texture2D Anel()
        {
            bool mudou = anel == null;
            for (int i = 0; i < 4; i++) if (Mathf.Abs(frac[i] - fracDesenhada[i]) > 0.004f) mudou = true;
            if (!mudou) return anel;

            if (anel == null) anel = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float c = (N - 1) / 2f, rExt = N * 0.49f, rInt = N * 0.36f, rFundo = N * 0.35f;
            const float vao = 5f;   // graus de espaço entre os quadrantes
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - c, dy = y - c;   // y da textura cresce para cima
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color cor = Color.clear;
                    if (r <= rFundo + 0.5f)
                    {
                        cor = new Color(0.04f, 0.07f, 0.05f, 0.88f * Borda(rFundo - r));
                    }
                    else if (r >= rInt - 0.5f && r <= rExt + 0.5f)
                    {
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;   // 0 = direita, anti-horário
                        if (ang < 0) ang += 360;
                        // quadrante e progresso no sentido horário a partir do início dele
                        int q; float inicio;
                        if (ang >= 90 && ang < 180) { q = (int)Q.Proteina; inicio = 180; }
                        else if (ang < 90) { q = (int)Q.Gordura; inicio = 90; }
                        else if (ang >= 270) { q = (int)Q.Agua; inicio = 360; }
                        else { q = (int)Q.Carbo; inicio = 270; }
                        float andou = inicio - ang;                     // 0..90, horário
                        if (andou >= vao / 2 && andou <= 90 - vao / 2)
                        {
                            float prog = (andou - vao / 2) / (90 - vao);
                            float alfaBorda = Borda(Mathf.Min(r - rInt, rExt - r));
                            var baseCor = CorDe(q);
                            cor = prog <= frac[q]
                                ? new Color(baseCor.r, baseCor.g, baseCor.b, alfaBorda)
                                : new Color(baseCor.r, baseCor.g, baseCor.b, 0.2f * alfaBorda);
                        }
                    }
                    px[y * N + x] = cor;
                }
            anel.SetPixels32(px);
            anel.Apply();
            for (int i = 0; i < 4; i++) fracDesenhada[i] = frac[i];
            return anel;
        }

        static float Borda(float d) => Mathf.Clamp01(d + 0.5f);   // antisserrilhado de 1 px

        // ---------- Ícones do jogo ----------
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

        static void DesenharSprite(Sprite s, Rect r, Color cor)
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
            GUI.color = cor;
            GUI.DrawTextureWithTexCoords(r, t, uv, true);
            GUI.color = antes;
        }

        // ---------- Desenho ----------
        public void Desenhar()
        {
            if (!Plugin.MostrarMacros.Value) return;
            float h = Screen.height / 1080f;          // o HUD do jogo escala com a altura da tela
            float d = Px(56);                          // diâmetro do anel
            float pad = Px(6);
            float colW = Px(86);
            float w = pad + d + Px(10) + colW * 2 + pad;
            float alt = d + pad * 2;
            // a vida/energia do jogo ocupa ~ y 990–1045 (em 1080p), começando em x ≈ 22
            float x = 22 * h;
            float yBase = Screen.height - (1080 - 989) * h;   // encostado nas barras do jogo
            var r = new Rect(x, yBase - alt, w, alt);
            Caixa(r, Painel);

            var ra = new Rect(r.x + pad, r.y + pad, d, d);
            GUI.DrawTexture(ra, Anel(), ScaleMode.StretchToFill, true);

            // ícones dentro do anel, um por quadrante
            float ic = d * 0.2f, off = d * 0.17f;
            var centro = ra.center;
            Vector2[] pos = { new Vector2(-off, -off), new Vector2(off, -off), new Vector2(-off, off), new Vector2(off, off) };
            for (int i = 0; i < 4; i++)
            {
                var s = Icone(i);
                var cr = new Rect(centro.x + pos[i].x - ic / 2, centro.y + pos[i].y - ic / 2, ic, ic);
                if (s != null) DesenharSprite(s, cr, CorDe(i));
                else GUI.Label(cr, C("#" + ColorUtility.ToHtmlStringRGB(CorDe(i)), "●"), new GUIStyle(Pequeno) { alignment = TextAnchor.MiddleCenter });
            }

            // valores em 2x2, na mesma ordem dos quadrantes
            float gx = ra.xMax + Px(10), gy = r.y + pad;
            float linha = d / 2;
            for (int i = 0; i < 4; i++)
            {
                float cx = gx + (i % 2) * colW, cy = gy + (i / 2) * linha;
                string hex = "#" + ColorUtility.ToHtmlStringRGB(CorDe(i));
                string pct = $"{Mathf.RoundToInt(frac[i] * 100)}%";
                string valor = critico[i] ? C(Perigo, $"<b>{pct}</b>") : $"<b>{pct}</b>";
                GUI.Label(new Rect(cx, cy + linha * 0.02f, colW, linha * 0.5f), C(hex, Nomes[i].ToUpperInvariant()), Rotulo);
                GUI.Label(new Rect(cx, cy + linha * 0.42f, colW, linha * 0.6f), valor, Texto);
            }
        }
    }
}
