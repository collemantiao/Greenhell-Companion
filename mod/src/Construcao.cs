using System.Collections.Generic;
using System.Linq;
using Enums;
using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>Mostra os materiais da construção que está sendo posicionada ou para a qual o jogador olha.</summary>
    public class Construcao
    {
        const float RaioBaus = 20f, RaioGhostPerto = 6f;

        class Material { public ItemID id; public int precisa, colocado, tenho, perto; }

        ConstructionGhost ghost;
        string situacao;
        List<Material> materiais = new List<Material>();
        float proxima, apareceuEm;

        public void Atualizar()
        {
            if (!Plugin.AjudaConstrucao.Value || Time.time < proxima) return;
            proxima = Time.time + 0.25f;
            materiais.Clear();
            var ghostAnterior = ghost;
            ghost = null;

            var lista = GhostsAtuais(out situacao);
            if (lista.Count == 0) return;
            if (ghostAnterior == null) apareceuEm = Time.unscaledTime;   // card acabou de aparecer: anima a entrada
            ghost = lista[0];

            var porId = new Dictionary<ItemID, Material>();
            foreach (var g in lista)
            {
                if (g == null || g.m_Steps == null) continue;
                bool posicionando = g.m_State != ConstructionGhost.GhostState.Building;
                for (int s = 0; s < g.m_Steps.Count; s++)
                {
                    if (!posicionando && s < g.m_CurrentStep) continue;   // etapas já concluídas
                    foreach (var slot in g.m_Steps[s].m_Slots)
                    {
                        var id = slot.m_ItemID != ItemID.None ? slot.m_ItemID : EnumUtils<ItemID>.GetValue(slot.m_ItemName);
                        if (!porId.TryGetValue(id, out var m)) porId[id] = m = new Material { id = id };
                        m.precisa++;
                        if (slot.m_Fulfilled) m.colocado++;
                    }
                }
            }
            foreach (var m in porId.Values)
            {
                m.tenho = Jogo.QuantosTenho(m.id);
                m.perto = Jogo.QuantosPerto(m.id, RaioBaus);
            }
            materiais = porId.Values.OrderBy(m => Faltam(m) == 0).ThenBy(m => Jogo.NomeItem(m.id)).ToList();
        }

        static int Faltam(Material m) => Mathf.Max(0, m.precisa - m.colocado - m.tenho);

        static List<ConstructionGhost> GhostsAtuais(out string situacao)
        {
            situacao = "posicionando";
            var lista = new List<ConstructionGhost>();

            var cerca = ConstructionFenceController.Get();
            if (cerca != null && cerca.IsActive())
            {
                lista.AddRange(cerca.m_Chain.Where(g => g != null));
                if (lista.Count == 0 && cerca.GetGhost() != null) lista.Add(cerca.GetGhost());
                return lista;
            }
            var corrente = ConstructionChainController.Get();
            if (corrente != null && corrente.IsActive())
            {
                lista.AddRange(corrente.m_Chain.Where(g => g != null).Cast<ConstructionGhost>());
                if (lista.Count == 0 && corrente.GetGhost() != null) lista.Add(corrente.GetGhost());
                return lista;
            }
            var simples = ConstructionController.Get();
            if (simples != null && simples.IsActive() && simples.GetGhost() != null)
            {
                lista.Add(simples.GetGhost());
                return lista;
            }

            // Construção já colocada, esperando materiais: a que está na mira, ou a mais perto.
            situacao = "faltam materiais";
            var tc = TriggerController.Get();
            ConstructionGhost alvo = null;
            if (tc != null)
            {
                var t = tc.GetBestTrigger();
                alvo = (t as GhostSlot)?.m_Parent ?? t as ConstructionGhost ?? tc.GetAdditionalTrigger() as ConstructionGhost;
            }
            if (alvo == null)
            {
                var pos = Jogo.PosicaoJogador();
                alvo = ConstructionGhostManager.Get()?.GetAll()
                    .Where(g => g != null && g.m_State == ConstructionGhost.GhostState.Building && !g.m_Challenge
                                && (g.transform.position - pos).sqrMagnitude < RaioGhostPerto * RaioGhostPerto)
                    .OrderBy(g => (g.transform.position - pos).sqrMagnitude)
                    .FirstOrDefault();
            }
            if (alvo != null && alvo.m_State == ConstructionGhost.GhostState.Building && !alvo.IsReady()) lista.Add(alvo);
            return lista;
        }

        /// <summary>Desenha no canto superior direito a partir de y e devolve onde o próximo cartão pode começar.</summary>
        public float Desenhar(float y)
        {
            if (ghost == null || materiais.Count == 0) return y;
            float largura = Mathf.Max(Screen.width * 0.22f, Px(300));
            var b = new Bloco();
            b.Add(Rotulo, "CONSTRUÇÃO · " + situacao.ToUpperInvariant());
            string nome = ghost.m_ResultItemID != ItemID.None ? Jogo.NomeItem(ghost.m_ResultItemID) : "Construção";
            b.Add(Titulo, Esc(nome), 0, 2);

            var faltando = new List<string>();
            foreach (var m in materiais)
            {
                int tenhoUtil = m.colocado + m.tenho;
                int falta = Faltam(m);
                string cont = m.colocado > 0 ? $"{m.colocado} colocados + {m.tenho} na mochila / {m.precisa}" : $"{m.tenho} / {m.precisa}";
                string cor = falta > 0 ? Aviso : Ok;
                b.Add(Texto, $"{Esc(Jogo.NomeItem(m.id))}   {C(cor, cont)}", 0, 6);
                b.Barra(m.precisa > 0 ? (float)tenhoUtil / m.precisa : 1, cor);
                if (falta > 0 && m.perto > 0) b.Add(Pequeno, C(Info, $"+{m.perto} em baús ou estantes a até {RaioBaus:0} m"), 0, 2);
                if (falta > 0) faltando.Add($"{C(Aviso, falta.ToString())} {Esc(Jogo.NomeItem(m.id))}");
            }
            b.Add(Texto, faltando.Count == 0 ? C(Ok, "Você tem tudo para terminar.") : "Faltam " + string.Join(", ", faltando), 0, 8);

            float h = b.Altura(largura);
            float x = Screen.width - largura - Screen.width * 0.016f;
            float e = Entrada(apareceuEm, 0.22f);
            var rect = new Rect(x + (1 - e) * Px(30), y, largura, h);
            ComOpacidade(e, () => b.Desenhar(rect, Painel));
            return y + h + Px(8);
        }
    }
}
