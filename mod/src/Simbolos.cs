using UnityEngine;

namespace GreenHellCompanion
{
    /// <summary>Símbolos desenhados por código (brancos, para tingir com GUI.color).</summary>
    public static class Simbolos
    {
        static Texture2D caveira;

        /// <summary>Caveira do marcador de loot/morte: campo de distância com sinal, com antisserrilhado.</summary>
        public static Texture2D Caveira()
        {
            if (caveira != null) return caveira;
            const int n = 128;
            caveira = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    // textura do Unity: j = 0 é a linha de baixo, então y já cresce para cima
                    float x = (i + 0.5f) / n * 2 - 1, y = (j + 0.5f) / n * 2 - 1;
                    float d = Sdf(x, y) * n / 2;   // em pixels
                    px[j * n + i] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
                }
            caveira.SetPixels(px);
            caveira.Apply();
            return caveira;
        }

        static float Sdf(float x, float y)
        {
            float d = Mathf.Min(Circulo(x, y, 0, 0.18f, 0.70f), Caixa(x, y, 0, -0.50f, 0.40f, 0.30f, 0.12f));   // crânio + mandíbula
            d = Mathf.Max(d, -Circulo(x, y, -0.28f, 0.10f, 0.19f));                                             // olhos
            d = Mathf.Max(d, -Circulo(x, y, 0.28f, 0.10f, 0.19f));
            d = Mathf.Max(d, -Triangulo(x, y, 0, -0.20f, 0.20f, 0.18f));                                        // nariz
            d = Mathf.Max(d, -Caixa(x, y, -0.14f, -0.66f, 0.025f, 0.14f, 0.02f));                               // dentes
            d = Mathf.Max(d, -Caixa(x, y, 0.14f, -0.66f, 0.025f, 0.14f, 0.02f));
            return d;
        }

        static float Circulo(float x, float y, float cx, float cy, float r) => new Vector2(x - cx, y - cy).magnitude - r;

        static float Caixa(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        // triângulo com a ponta para baixo
        static float Triangulo(float x, float y, float cx, float cy, float w, float h)
        {
            float px = Mathf.Abs(x - cx), py = y - cy;
            float k = h / (w / 2);
            return Mathf.Max(py - h / 2, (k * px - (py + h / 2)) / Mathf.Sqrt(k * k + 1));
        }

        /// <summary>Desenha a textura branca tingida, com uma sombra leve para destacar sobre a mata.</summary>
        public static void Desenhar(Texture2D t, Rect r, Color cor, bool sombra = true)
        {
            var antes = GUI.color;
            if (sombra)
            {
                GUI.color = new Color(0, 0, 0, 0.55f * antes.a);
                GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width, r.height), t);
            }
            GUI.color = new Color(cor.r, cor.g, cor.b, cor.a * antes.a);
            GUI.DrawTexture(r, t);
            GUI.color = antes;
        }
    }
}
