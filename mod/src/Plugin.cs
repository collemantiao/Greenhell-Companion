using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GreenHellCompanion
{
    [BepInPlugin(Id, "Green Hell Companion", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "br.felipe.greenhellcompanion";

        public static Plugin Instancia { get; private set; }
        public static ConfigEntry<KeyboardShortcut> TeclaGuia, TeclaMarcar, TeclaMarcadores, TeclaConfig;
        public static ConfigEntry<float> Escala, Opacidade, OpacidadeImagem;
        public static ConfigEntry<float> SegundosAlerta;
        public static ConfigEntry<bool> AvisosSaude, AjudaConstrucao, TodosMarcadoresNoMundo, MostrarMacros;

        Saude saude;
        Construcao construcao;
        Macros macros;
        Marcadores marcadores;
        JanelaGuia guia;
        public static JanelaConfig Configuracoes { get; private set; }

        void Awake()
        {
            Instancia = this;
            TeclaGuia = Config.Bind("Teclas", "Guia", new KeyboardShortcut(KeyCode.F1), "Abre/fecha o guia.");
            TeclaMarcar = Config.Bind("Teclas", "Marcar", new KeyboardShortcut(KeyCode.F2), "Marca o ponto para onde você está olhando.");
            TeclaMarcadores = Config.Bind("Teclas", "Marcadores", new KeyboardShortcut(KeyCode.F3), "Abre/fecha a lista de marcadores.");
            TeclaConfig = Config.Bind("Teclas", "Configuracoes", new KeyboardShortcut(KeyCode.F4), "Abre/fecha as configurações.");
            Escala = Config.Bind("Tela", "Escala", 1.0f, new ConfigDescription("Tamanho dos textos e painéis.", new AcceptableValueRange<float>(0.6f, 1.6f)));
            Opacidade = Config.Bind("Tela", "OpacidadeCartoes", 0.84f, new ConfigDescription("Opacidade do fundo dos avisos e painéis (0,2 a 1).", new AcceptableValueRange<float>(0.2f, 1f)));
            OpacidadeImagem = Config.Bind("Tela", "OpacidadeIlustracoes", 0.16f, new ConfigDescription("Opacidade das silhuetas de animais (0 esconde).", new AcceptableValueRange<float>(0f, 0.5f)));
            SegundosAlerta = Config.Bind("Saúde", "SegundosAlerta", 20f, "Quanto tempo o aviso completo fica na tela antes de virar uma linha.");
            AvisosSaude = Config.Bind("Saúde", "Ativo", true, "Mostra avisos de ferimentos, doenças e status baixos.");
            AjudaConstrucao = Config.Bind("Construção", "Ativo", true, "Mostra os materiais que faltam ao construir.");
            MostrarMacros = Config.Bind("Tela", "MostrarMacroelementos", true, "Mostra proteínas, gorduras, carboidratos e água acima da vida, no canto inferior esquerdo.");
            TodosMarcadoresNoMundo = Config.Bind("Marcadores", "MostrarTodosNoMundo", false, "Mostra todos os marcadores na tela, não só o ativo.");

            Config.SaveOnConfigSet = false;   // os deslizantes mudam o valor a cada quadro; salva ao fechar a janela

            var arquivoGuia = Path.Combine(Path.GetDirectoryName(Info.Location), "Data", "guia.json");
            try { Guia.Carregar(arquivoGuia); Logger.LogInfo($"Guia: {Guia.Fichas.Count} fichas"); }
            catch (System.Exception e) { Logger.LogError($"Não consegui ler {arquivoGuia}: {e.Message}"); }

            Imagens.Iniciar(Path.Combine(Path.GetDirectoryName(Info.Location), "Data", "img"));
            saude = new Saude();
            construcao = new Construcao();
            macros = new Macros();
            marcadores = new Marcadores(Path.Combine(Paths.ConfigPath, "GreenHellCompanion"));
            guia = new JanelaGuia();
            Configuracoes = new JanelaConfig();

            new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Green Hell Companion carregado");
        }

        public static void Log(string msg) => Instancia.Logger.LogInfo(msg);
        public static void Erro(string onde, System.Exception e) => Instancia.Logger.LogWarning($"{onde}: {e}");

        void Update()
        {
            bool jogando = Jogo.EmJogo();
            if (!jogando)
            {
                if (Janela.Aberta != null) Janela.Fechar();
                saude.Reiniciar();
                marcadores.Descarregar();
                return;
            }

            if (TeclaGuia.Value.IsDown()) guia.Alternar(saude.FichaRecente());
            else if (TeclaMarcadores.Value.IsDown()) marcadores.AlternarLista();
            else if (TeclaConfig.Value.IsDown()) Configuracoes.Alternar();
            else if (TeclaMarcar.Value.IsDown() && Janela.Aberta == null && Jogo.PodeUsarHud()) marcadores.MarcarOlhando();
            else if (Input.GetKeyDown(KeyCode.Escape) && Janela.Aberta != null) Janela.Fechar();

            Proteger(() => saude.Atualizar(), "saude");
            Proteger(() => construcao.Atualizar(), "construcao");
            Proteger(() => macros.Atualizar(), "macros");
            Proteger(() => marcadores.Atualizar(), "marcadores");
        }

        void OnGUI()
        {
            if (!Jogo.EmJogo()) return;
            Estilo.Preparar();
            bool hud = Jogo.PodeUsarHud();
            if (hud)
            {
                Proteger(() => marcadores.DesenharHud(), "hud marcadores");
                Proteger(() => macros.Desenhar(), "hud macros");
                if (AjudaConstrucao.Value) Proteger(() => construcao.Desenhar(), "hud construcao");
            }
            if (hud || Jogo.InspecionandoCorpo())
                if (AvisosSaude.Value) Proteger(() => saude.Desenhar(), "hud saude");
            if (Janela.Aberta != null) Proteger(() => Janela.Aberta.Desenhar(), "janela");
        }

        void OnDestroy() { if (Janela.Aberta != null) Janela.Fechar(); }

        // Um erro num painel não pode derrubar os outros nem encher o log a cada quadro.
        static readonly System.Collections.Generic.HashSet<string> jaLogado = new System.Collections.Generic.HashSet<string>();
        static void Proteger(System.Action a, string onde)
        {
            try { a(); }
            catch (System.Exception e) { if (jaLogado.Add(onde)) Erro(onde, e); }
        }
    }

    // Com uma janela do mod aberta, o Esc fecha a janela em vez de abrir o menu de pausa.
    [HarmonyPatch(typeof(MenuInGameManager), "CanShowMenuInGame")]
    static class BloqueiaMenuPausa
    {
        static bool Prefix(ref bool __result)
        {
            if (Janela.Aberta == null && Time.frameCount - Janela.QuadroFechamento > 1) return true;
            __result = false;
            return false;
        }
    }
}
