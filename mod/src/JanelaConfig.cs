using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>Ajustes visuais e liga/desliga, aplicados na hora. Fica num canto sem escurecer o jogo.</summary>
    public class JanelaConfig : Janela
    {
        public void Alternar()
        {
            if (Aberta == this) Fechar();
            else if (Aberta == null) Abrir();
        }

        protected override void AoFechar() => Plugin.Instancia.Config.Save();

        public override void Desenhar()
        {
            if (EscPressionado()) { Fechar(); return; }
            float pad = Px(16), w = Mathf.Max(Screen.width * 0.24f, Px(330));
            float alt = Px(568);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - alt) / 2, w, alt);
            CaixaArredondada(r, PainelForte, RaioJanela);
            float x = r.x + pad, iw = w - pad * 2, y = r.y + pad;

            GUI.Label(new Rect(x, y, iw, Px(30)), "<b>Configurações</b>", TituloGrande);
            y += Px(36);
            GUI.Label(new Rect(x, y, iw, Px(34)), "As mudanças aparecem na hora. O cartão de exemplo, no canto inferior esquerdo, mostra o resultado.", Pequeno);
            y += Px(38);

            y = Deslizante(x, y, iw, "TAMANHO DO TEXTO", Plugin.Escala, 0.6f, 1.6f, v => $"{v * 100:0}%");
            y = Deslizante(x, y, iw, "OPACIDADE DOS CARTÕES", Plugin.Opacidade, 0.2f, 1f, v => $"{v * 100:0}%");
            y = Deslizante(x, y, iw, "OPACIDADE DAS ILUSTRAÇÕES", Plugin.OpacidadeImagem, 0f, 0.5f, v => v <= 0.001f ? "ocultas" : $"{v * 100:0}%");

            y += Px(6);
            y = Chave(x, y, iw, "Macroelementos acima da vida", Plugin.MostrarMacros);
            y = Chave(x, y, iw, "Avisos de saúde", Plugin.AvisosSaude);
            y = Chave(x, y, iw, "Ajuda de construção", Plugin.AjudaConstrucao);
            y = Chave(x, y, iw, "Todos os marcadores no mundo", Plugin.TodosMarcadoresNoMundo);
            y = Chave(x, y, iw, "Animações", Plugin.Animacoes);

            y += Px(10);
            float bw = (iw - Px(8)) / 2;
            if (GUI.Button(new Rect(x, y, bw, Px(30)), "Restaurar padrão", Botao))
            {
                Plugin.Escala.Value = (float)Plugin.Escala.DefaultValue;
                Plugin.Opacidade.Value = (float)Plugin.Opacidade.DefaultValue;
                Plugin.OpacidadeImagem.Value = (float)Plugin.OpacidadeImagem.DefaultValue;
            }
            if (GUI.Button(new Rect(x + bw + Px(8), y, bw, Px(30)), C(Destaque, "<b>Fechar</b>"), Botao)) { Fechar(); return; }

            // crédito do criador
            y += Px(44);
            GUI.DrawTexture(new Rect(x, y - Px(8), iw, 1), Tex(Linha));
            string versao = Plugin.Instancia.Info.Metadata.Version.ToString();
            GUI.Label(new Rect(x, y, iw, Px(18)), $"Green Hell Companion {versao}  ·  criado por {C(Destaque, "<b>@tiaozadas</b>")}", new GUIStyle(Pequeno) { alignment = TextAnchor.MiddleCenter });

            Exemplo();
        }

        static float Deslizante(float x, float y, float w, string rotulo, BepInEx.Configuration.ConfigEntry<float> cfg, float min, float max, System.Func<float, string> fmt)
        {
            GUI.Label(new Rect(x, y, w, Px(16)), rotulo, Rotulo);
            GUI.Label(new Rect(x, y, w, Px(16)), $"<b>{fmt(cfg.Value)}</b>", new GUIStyle(Texto) { alignment = TextAnchor.UpperRight });
            y += Px(20);
            float v = GUI.HorizontalSlider(new Rect(x, y, w, Px(16)), cfg.Value, min, max);
            v = Mathf.Round(v * 100) / 100f;
            if (!Mathf.Approximately(v, cfg.Value)) cfg.Value = v;
            return y + Px(28);
        }

        static float Chave(float x, float y, float w, string texto, BepInEx.Configuration.ConfigEntry<bool> cfg)
        {
            bool v = GUI.Toggle(new Rect(x, y, w, Px(24)), cfg.Value, "  " + texto, new GUIStyle(GUI.skin.toggle) { fontSize = Px(13) });
            if (v != cfg.Value) cfg.Value = v;
            return y + Px(28);
        }

        /// <summary>Cartão de aviso de mentira, desenhado sobre o jogo, para ver o efeito dos ajustes.</summary>
        static void Exemplo()
        {
            var b = new Bloco { Faixa = Perigo, Imagem = Imagens.Get("onca-pintada") };
            b.Add(Rotulo, C(Perigo, "EXEMPLO DE AVISO"));
            b.Add(Titulo, "Laceração de felino", 0, 2);
            b.Add(Pequeno, $"braço esquerdo · {C(Perigo, "sangrando")} · causado por onça", 0, 1);
            b.Add(Texto, $"{C(Ok, "✓")}  Formigas  {C(Apagado, "3 na mochila")}", 0, 6);
            b.Add(Texto, $"{C(Apagado, "✕")}  Curativo de cinzas  {C(Apagado, "não tem")}", 0, 2);
            b.Add(Texto, $"{C(Aviso, "!")}  Bandagem de folha  {C(Apagado, "2 na mochila")}", 0, 2);
            // no mesmo lugar dos avisos de verdade: canto inferior esquerdo, acima dos macroelementos
            float h = b.Altura(Saude.Largura);
            b.Desenhar(new Rect(Saude.X, Saude.Base - h, Saude.Largura, h), Painel);
        }
    }
}
