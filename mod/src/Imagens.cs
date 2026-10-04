using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GreenHellCompanion
{
    /// <summary>Silhuetas de animais (Data/img/&lt;id da ficha&gt;.png, PhyloPic CC0). Viram brancas para poder tingir.</summary>
    public static class Imagens
    {
        static string pasta;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static void Iniciar(string pastaImagens) => pasta = pastaImagens;

        public static Texture2D Get(string id)
        {
            if (string.IsNullOrEmpty(id) || pasta == null) return null;
            if (cache.TryGetValue(id, out var t)) return t;
            t = null;
            var arq = Path.Combine(pasta, id + ".png");
            if (File.Exists(arq))
            {
                try
                {
                    t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                    t.LoadImage(File.ReadAllBytes(arq));
                    var px = t.GetPixels32();
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a);
                    t.SetPixels32(px);
                    t.Apply();
                }
                catch (System.Exception e) { Plugin.Erro("imagem " + id, e); t = null; }
            }
            cache[id] = t;
            return t;
        }

        /// <summary>Desenha a silhueta inteira dentro do retângulo, alinhada à direita, com a opacidade pedida.</summary>
        public static void Desenhar(Texture2D t, Rect area, float opacidade)
        {
            if (t == null) return;
            float esc = Mathf.Min(area.width / t.width, area.height / t.height);
            float w = t.width * esc, h = t.height * esc;
            var r = new Rect(area.xMax - w, area.y + (area.height - h) / 2, w, h);
            var antes = GUI.color;
            GUI.color = new Color(0.93f, 0.95f, 0.92f, opacidade * antes.a);   // respeita fades em andamento
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = antes;
        }
    }
}
