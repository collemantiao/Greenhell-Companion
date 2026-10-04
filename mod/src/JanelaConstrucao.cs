using System.Collections.Generic;
using System.Linq;
using Enums;
using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>
    /// Menu de construção e criação: receitas por grupo, no formato "Galhos  10 (8)"
    /// (necessário e, entre parênteses, quanto você tem; verde se dá, vermelho se falta).
    /// As receitas vêm do próprio jogo: projetos (ghosts) das construções e componentes dos itens.
    /// </summary>
    public class JanelaConstrucao : Janela
    {
        class Receita
        {
            public ItemID id;
            public string nome, grupo;
            public int ordemGrupo;
            public bool construcao, porTrecho;
            public List<KeyValuePair<ItemID, int>> itens = new List<KeyValuePair<ItemID, int>>();
            public string busca;     // nome + ingredientes, normalizados
        }

        List<Receita> construcoes, criacao;
        bool abaCriacao, soDesbloqueadas = true, soPossiveis;
        string busca = "";
        Vector2 rolagem;
        readonly Dictionary<ItemID, int> tenho = new Dictionary<ItemID, int>();
        float proximaContagem;
        HashSet<ItemID> desbloqueadas = new HashSet<ItemID>();

        public static ItemInfo Posicionar;   // executado no próximo Update, depois de fechar a janela

        public void Alternar()
        {
            if (Aberta == this) { Fechar(); return; }
            if (Aberta != null) return;
            if (construcoes == null) Carregar();
            desbloqueadas = new HashSet<ItemID>(ItemsManager.Get().m_UnlockedInNotepadItems);
            proximaContagem = 0;
            Abrir();
        }

        // ---------- Receitas do jogo ----------
        void Carregar()
        {
            construcoes = new List<Receita>();
            criacao = new List<Receita>();
            var im = ItemsManager.Get();
            var travadas = new HashSet<ItemID>(im.m_CraftingLockedItems);
            foreach (var info in im.GetAllInfos().Values)
            {
                if (info == null) continue;
                try
                {
                    if (info.IsConstruction()) { var r = DeConstrucao(info); if (r != null) construcoes.Add(r); }
                    else if (info.m_Components != null && info.m_Components.Count > 0 && !travadas.Contains(info.m_ID))
                    {
                        var r = new Receita { id = info.m_ID, construcao = false };
                        foreach (var c in info.m_Components) r.itens.Add(new KeyValuePair<ItemID, int>((ItemID)c.Key, c.Value));
                        (r.grupo, r.ordemGrupo) = GrupoCriacao(info.m_Type);
                        criacao.Add(Finalizar(r));
                    }
                }
                catch (System.Exception e) { Plugin.Erro("receita " + info.m_ID, e); }
            }
            Plugin.Log($"Receitas: {construcoes.Count} construções, {criacao.Count} de criação");
        }

        static Receita DeConstrucao(ItemInfo info)
        {
            var prefab = GreenHellGame.Instance.GetPrefab(info.m_ID + "Ghost");
            var ghost = prefab != null ? prefab.GetComponent<ConstructionGhost>() : null;
            if (ghost == null || ghost.m_Steps == null) return null;
            var conta = new Dictionary<ItemID, int>();
            foreach (var passo in ghost.m_Steps)
                foreach (var slot in passo.m_Slots)
                {
                    if (slot == null || !EnumUtils<ItemID>.TryGetValue(slot.m_ItemName, out var id)) continue;
                    conta.TryGetValue(id, out var n);
                    conta[id] = n + 1;
                }
            if (conta.Count == 0) return null;
            var r = new Receita { id = info.m_ID, construcao = true, porTrecho = info.IsFence() || info.IsChain() };
            r.itens = conta.OrderByDescending(kv => kv.Value).ToList();
            if (ItemInfo.IsDecoration(info.m_ID)) { r.grupo = "Decoração"; r.ordemGrupo = 8; }
            else (r.grupo, r.ordemGrupo) = GrupoConstrucao((info as ConstructionInfo)?.m_ConstructionType ?? ConstructionType.Other);
            return Finalizar(r);
        }

        static Receita Finalizar(Receita r)
        {
            r.nome = Jogo.NomeItem(r.id);
            r.busca = Guia.Norm(r.nome + " " + r.id + " " + string.Join(" ", r.itens.Select(kv => Jogo.NomeItem(kv.Key))));
            return r;
        }

        static (string, int) GrupoConstrucao(ConstructionType t)
        {
            switch (t)
            {
                case ConstructionType.Shelter: return ("Abrigos e camas", 0);
                case ConstructionType.Fire: return ("Fogo", 1);
                case ConstructionType.Water: return ("Água", 2);
                case ConstructionType.Trap: return ("Armadilhas", 3);
                case ConstructionType.WaterTrap: return ("Armadilhas na água", 4);
                case ConstructionType.Stand: return ("Estantes e armazenamento", 5);
                default: return ("Outras construções", 7);
            }
        }

        static (string, int) GrupoCriacao(ItemType t)
        {
            switch (t)
            {
                case ItemType.ItemTool: return ("Ferramentas", 0);
                case ItemType.Weapon: return ("Armas", 1);
                case ItemType.Spear: return ("Lanças", 2);
                case ItemType.Bow: case ItemType.Arrow: return ("Arcos e flechas", 3);
                case ItemType.Blowpipe: case ItemType.BlowpipeArrow: return ("Zarabatana e dardos", 4);
                case ItemType.Dressing: return ("Curativos", 5);
                case ItemType.Torch: return ("Tochas", 6);
                case ItemType.Bowl: case ItemType.LiquidContainer: return ("Recipientes", 7);
                case ItemType.Armor: return ("Armaduras", 8);
                case ItemType.Trap: return ("Armadilhas", 9);
                case ItemType.Form: case ItemType.FormBaked: return ("Barro", 10);
                default: return ("Outros itens", 11);
            }
        }

        // ---------- Contagem ----------
        int Tenho(ItemID id)
        {
            if (!tenho.TryGetValue(id, out var n)) tenho[id] = n = Jogo.QuantosTenho(id);
            return n;
        }

        bool DaParaFazer(Receita r) => r.itens.All(kv => Tenho(kv.Key) >= kv.Value);

        // ---------- Desenho ----------
        public override void Desenhar()
        {
            if (Time.time >= proximaContagem) { tenho.Clear(); proximaContagem = Time.time + 0.5f; }
            var r = Centro(0.66f, 0.8f);
            Fundo(r);
            if (EscPressionado()) { Fechar(); return; }

            float pad = Px(18), x = r.x + pad, w = r.width - pad * 2, y = r.y + pad;
            GUI.Label(new Rect(x, y, w, Px(32)), "<b>Construção</b>", TituloGrande);
            if (GUI.Button(new Rect(r.xMax - pad - Px(80), y, Px(80), Px(26)), "Fechar", Botao)) { Fechar(); return; }
            y += Px(40);

            // abas
            float aw = Px(130);
            if (GUI.Toggle(new Rect(x, y, aw, Px(28)), !abaCriacao, "Construções", BotaoLista) && abaCriacao) { abaCriacao = false; rolagem = Vector2.zero; }
            if (GUI.Toggle(new Rect(x + aw + Px(6), y, aw, Px(28)), abaCriacao, "Criação", BotaoLista) && !abaCriacao) { abaCriacao = true; rolagem = Vector2.zero; }
            float fx = x + aw * 2 + Px(24);
            var tg = new GUIStyle(GUI.skin.toggle) { fontSize = Px(12.5f) };
            tg.normal.textColor = tg.onNormal.textColor = tg.hover.textColor = tg.onHover.textColor = Cor(Tinta);
            soDesbloqueadas = GUI.Toggle(new Rect(fx, y + Px(4), Px(200), Px(22)), soDesbloqueadas, "  Só as que conheço", tg);
            soPossiveis = GUI.Toggle(new Rect(fx + Px(200), y + Px(4), Px(240), Px(22)), soPossiveis, "  Só o que dá para fazer agora", tg);
            y += Px(38);

            busca = GUI.TextField(new Rect(x, y, w, Px(32)), busca, 40, Campo);
            if (string.IsNullOrEmpty(busca))
                GUI.Label(new Rect(x + Px(10), y + Px(7), w, Px(20)), C(Apagado, "Buscar por nome ou material: abrigo, corda, lança…"), Texto);
            y += Px(42);

            GUI.Label(new Rect(x, y, w, Px(16)), $"Entre parênteses, quanto você tem na mochila: {C(Ok, "verde")} dá, {C(Perigo, "vermelho")} falta.", Pequeno);
            y += Px(22);

            var lista = Filtrar(abaCriacao ? criacao : construcoes);
            var area = new Rect(x, y, w, r.yMax - pad - y);
            if (lista.Count == 0)
            {
                string msg = soDesbloqueadas && !string.IsNullOrEmpty(busca) ? "Nada encontrado entre as receitas que você conhece. Desmarque \"Só as que conheço\" para ver todas."
                    : soPossiveis ? "Nenhuma receita completa com o que você tem agora."
                    : soDesbloqueadas ? "Você ainda não conhece receitas deste tipo. Elas aparecem conforme são desbloqueadas no caderno."
                    : "Nada encontrado.";
                GUI.Label(area, msg, Texto);
                return;
            }
            DesenharCartoes(area, lista);
        }

        List<Receita> Filtrar(List<Receita> fonte)
        {
            var termos = Guia.Norm(busca).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            return fonte.Where(rc =>
                    (!soDesbloqueadas || desbloqueadas.Contains(rc.id)) &&
                    termos.All(t => rc.busca.Contains(t)) &&
                    (!soPossiveis || DaParaFazer(rc)))
                .OrderBy(rc => rc.ordemGrupo).ThenBy(rc => rc.nome).ToList();
        }

        // Cartões em colunas: cada grupo tem um título e os cartões vão para a coluna mais baixa.
        void DesenharCartoes(Rect area, List<Receita> lista)
        {
            float larguraUtil = area.width - Px(16);
            int colunas = larguraUtil > Px(900) ? 3 : 2;
            float gap = Px(10), cw = (larguraUtil - gap * (colunas - 1)) / colunas;
            float lh = Px(20), cab = Px(40), rod = abaCriacao ? 0 : Px(34);

            var posicoes = new List<(Receita rc, Rect r)>();
            var titulos = new List<(string t, float y)>();
            float y = 0;
            foreach (var g in lista.GroupBy(rc => rc.grupo))
            {
                titulos.Add(($"{g.Key.ToUpperInvariant()}  {C(Apagado, g.Count().ToString())}", y));
                y += Px(24);
                var alturas = new float[colunas];
                foreach (var rc in g)
                {
                    bool podePos = rc.construcao && desbloqueadas.Contains(rc.id);
                    float h = cab + rc.itens.Count * lh + Px(12) + (podePos ? rod : 0);
                    int c = System.Array.IndexOf(alturas, alturas.Min());
                    posicoes.Add((rc, new Rect(c * (cw + gap), y + alturas[c], cw, h)));
                    alturas[c] += h + gap;
                }
                y += alturas.Max() + Px(8);
            }

            rolagem = GUI.BeginScrollView(area, rolagem, new Rect(0, 0, larguraUtil, y));
            foreach (var (t, ty) in titulos) GUI.Label(new Rect(0, ty, larguraUtil, Px(18)), t, Rotulo);
            foreach (var (rc, cr) in posicoes)
            {
                if (cr.yMax < rolagem.y || cr.y > rolagem.y + area.height) continue;   // fora da tela
                Cartao(rc, cr, lh);
            }
            GUI.EndScrollView();
        }

        void Cartao(Receita rc, Rect r, float lh)
        {
            bool pronto = DaParaFazer(rc);
            CaixaArredondada(r, pronto ? new Color32(115, 207, 152, 22) : new Color32(255, 255, 255, 10), RaioLinha);
            float pad = Px(10), x = r.x + pad, w = r.width - pad * 2, y = r.y + Px(8);

            float ic = Px(26);
            var info = ItemsManager.Get().GetInfo(rc.id);
            Sprite sp = null;
            if (info != null && !string.IsNullOrEmpty(info.m_IconName)) ItemsManager.Get().m_ItemIconsSprites.TryGetValue(info.m_IconName, out sp);
            float tx = x;
            if (sp != null) { DesenharSprite(sp, new Rect(x, y, ic, ic), Color.white); tx += ic + Px(8); }
            string extra = rc.porTrecho ? C(Apagado, "  por trecho") : "";
            GUI.Label(new Rect(tx, y + Px(2), w - (tx - x), Px(22)), $"<b>{Esc(rc.nome)}</b>{extra}", new GUIStyle(Texto) { wordWrap = false, clipping = TextClipping.Clip });
            if (pronto) GUI.Label(new Rect(tx, y + Px(2), w - (tx - x), Px(22)), C(Ok, "<b>✓</b>"), new GUIStyle(Texto) { alignment = TextAnchor.UpperRight });
            y += Px(32);

            var dir = new GUIStyle(Texto) { alignment = TextAnchor.UpperRight, wordWrap = false };
            var esq = new GUIStyle(Texto) { wordWrap = false, clipping = TextClipping.Clip };
            foreach (var kv in rc.itens)
            {
                int t = Tenho(kv.Key);
                string cor = t >= kv.Value ? Ok : Perigo;
                GUI.Label(new Rect(x, y, w - Px(70), lh), Esc(Jogo.NomeItem(kv.Key)), esq);
                GUI.Label(new Rect(x, y, w, lh), $"<b>{kv.Value}</b> {C(cor, $"({t})")}", dir);
                y += lh;
            }

            if (rc.construcao && desbloqueadas.Contains(rc.id))
            {
                y += Px(6);
                if (GUI.Button(new Rect(x, y, Px(120), Px(26)), "Posicionar", Botao))
                {
                    Posicionar = ItemsManager.Get().GetInfo(rc.id);
                    Fechar();
                }
            }
        }

        /// <summary>Começa a posicionar a construção, como se fosse escolhida no caderno.</summary>
        public static void ExecutarPendente()
        {
            var info = Posicionar;
            Posicionar = null;
            if (info == null) return;
            var p = Player.Get();
            if (info.IsFence())
            {
                var c = p.GetComponent<ConstructionFenceController>();
                if (!c.IsActive()) { c.SetupPrefab(info); p.StartController(PlayerControllerType.ConstructionFence); }
            }
            else if (info.IsChain())
            {
                var c = p.GetComponent<ConstructionChainController>();
                if (!c.IsActive()) { c.SetupPrefab(info); p.StartController(PlayerControllerType.ConstructionChain); }
            }
            else
            {
                var c = p.GetComponent<ConstructionController>();
                if (!c.IsActive()) { c.SetupPrefab(info); p.StartController(PlayerControllerType.Construction); }
            }
        }
    }
}
