using System.Collections.Generic;
using System.Linq;
using AIs;
using Enums;
using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>Observa ferimentos, doenças e status do jogador e mostra o que fazer.</summary>
    public class Saude
    {
        class Alerta
        {
            public string chave, rotulo, corRotulo, titulo, onde, fichaId, rodape, imagem;
            public List<string> linhas = new List<string>();
            public float criado;
            public bool tratando;
        }

        readonly Dictionary<string, Alerta> ativos = new Dictionary<string, Alerta>();
        readonly Dictionary<string, int> nivelDoenca = new Dictionary<string, int>();
        bool primeiraLeitura = true;
        float proximaLeitura;

        public void Reiniciar()
        {
            ativos.Clear();
            nivelDoenca.Clear();
            primeiraLeitura = true;
        }

        /// <summary>Ficha do aviso mais recente que ainda está aberto na tela (o F1 abre nela).</summary>
        public string FichaRecente()
        {
            float limite = Time.time - Plugin.SegundosAlerta.Value;
            return ativos.Values.Where(a => a.criado >= limite && !a.tratando).OrderByDescending(a => a.criado).FirstOrDefault()?.fichaId;
        }

        // ---------------- Leitura ----------------
        public void Atualizar()
        {
            if (!Plugin.AvisosSaude.Value || Time.time < proximaLeitura) return;
            proximaLeitura = Time.time + 0.5f;

            var agora = new Dictionary<string, Alerta>();
            LerFerimentos(agora);
            LerDoencas(agora);
            LerStatus(agora);

            foreach (var kv in agora)
            {
                if (ativos.TryGetValue(kv.Key, out var velho) && !Piorou(kv.Key, kv.Value))
                {
                    // mantém a hora de criação; atualiza o texto (contagem, tratamento, quantidades na mochila)
                    kv.Value.criado = velho.criado;
                }
                else
                {
                    kv.Value.criado = primeiraLeitura ? -9999f : Time.time;
                }
            }
            ativos.Clear();
            foreach (var kv in agora) ativos[kv.Key] = kv.Value;
            primeiraLeitura = false;
        }

        bool Piorou(string chave, Alerta a)
        {
            if (!chave.StartsWith("D:")) return false;
            int nivel = int.Parse(a.titulo.Split(' ').Last());
            nivelDoenca.TryGetValue(chave, out var antes);
            nivelDoenca[chave] = nivel;
            return nivel > antes && antes > 0;
        }

        void LerFerimentos(Dictionary<string, Alerta> saida)
        {
            var mod = PlayerInjuryModule.Get();
            if (mod == null) return;
            var grupos = new Dictionary<string, List<Injury>>();
            foreach (var inj in mod.m_Injuries)
            {
                if (inj == null) continue;
                var tipo = inj.m_Type;
                if (tipo == InjuryType.LeechHole) continue;
                // o buraco com verme dentro é tratado junto com o verme
                if (tipo == InjuryType.WormHole && inj.m_State == InjuryState.WormInside) continue;
                if (inj.m_State == InjuryState.Closed) continue;
                string chave = inj.m_State == InjuryState.Infected ? "I:" + tipo : "F:" + tipo;
                if (!grupos.TryGetValue(chave, out var l)) grupos[chave] = l = new List<Injury>();
                l.Add(inj);
            }

            int veneno = mod.GetPosionLevel();
            foreach (var g in grupos)
            {
                var primeiro = g.Value[0];
                var tipo = primeiro.m_Type;
                bool infectado = g.Key.StartsWith("I:");
                int n = g.Value.Count;
                var a = new Alerta
                {
                    chave = g.Key,
                    rotulo = infectado ? "FERIDA INFECCIONOU" : "NOVO FERIMENTO",
                    corRotulo = infectado || tipo == InjuryType.VenomBite || tipo == InjuryType.SnakeBite || tipo == InjuryType.LacerationCat || tipo == InjuryType.Laceration ? Perigo : Aviso,
                    titulo = NomeFerimento(tipo) + (n > 1 ? $" ×{n}" : "") + (infectado ? " infeccionado" : ""),
                    fichaId = infectado ? "ferimento-infectado" : FichaFerimento(tipo),
                    tratando = g.Value.All(i => i.m_Bandage != null || i.IsHealing()),
                };
                var partes = new List<string>();
                partes.Add(string.Join(", ", g.Value.Select(i => Lugar(i.m_Place)).Distinct()));
                if (!infectado && Estado(primeiro.m_State) != null) partes.Add(Estado(primeiro.m_State));
                var causador = g.Value.Select(i => i.m_AIDamager).FirstOrDefault(id => Causa(id) != null);
                if (Causa(causador) != null) partes.Add("causado por " + Causa(causador));
                a.imagem = ImagemCausa(causador) ?? ImagemFerimento(tipo);
                if ((tipo == InjuryType.VenomBite || tipo == InjuryType.SnakeBite) && veneno > 0) partes.Add($"veneno {veneno}");
                if (a.tratando) partes.Add(C(Ok, "tratando"));
                a.onde = string.Join(" · ", partes);

                Tratamentos(a, tipo, infectado, primeiro.m_State);
                a.rodape = tipo == InjuryType.Leech ? "Inspecione o corpo para arrancar." : "Aplique na inspeção do corpo.";
                saida[g.Key] = a;
            }
        }

        void LerDoencas(Dictionary<string, Alerta> saida)
        {
            var mod = PlayerDiseasesModule.Get();
            if (mod == null) return;
            foreach (var d in mod.GetAllDiseases().Values)
            {
                if (d == null || !d.IsActive() || d.m_Level <= 0) continue;
                var tipo = d.m_Type;
                string chave = "D:" + tipo;
                var a = new Alerta
                {
                    chave = chave,
                    rotulo = "DOENÇA",
                    corRotulo = Aviso,
                    titulo = $"{NomeDoenca(tipo)} · nível {d.m_Level}",
                    fichaId = FichaDoenca(tipo),
                };
                foreach (var (id, efeito) in CurasDe(tipo).Take(4))
                    a.linhas.Add(LinhaItem(id, efeito > 0 ? $"−{efeito}" : null));
                if (a.linhas.Count < 2) DicasDaFicha(a, 3 - a.linhas.Count);
                a.rodape = "Coma ou beba pela mochila.";
                saida[chave] = a;
            }
        }

        void LerStatus(Dictionary<string, Alerta> saida)
        {
            var c = PlayerConditionModule.Get();
            if (c == null) return;
            void Status(bool baixo, string chave, string titulo, string ficha, string detalhe)
            {
                if (!baixo) return;
                var a = new Alerta { chave = "S:" + chave, rotulo = "STATUS BAIXO", corRotulo = Info, titulo = titulo, fichaId = ficha, onde = detalhe };
                DicasDaFicha(a, 3);
                saida[a.chave] = a;
            }
            Status(c.IsHydrationCriticalLevel(), "agua", "Sede", "desidratacao", Pct(c.GetHydration(), c.GetMaxHydration()));
            Status(c.IsNutritionCarboCriticalLevel(), "carbo", "Falta de carboidratos", "carboidratos", Pct(c.GetNutritionCarbo(), c.GetMaxNutritionCarbo()));
            Status(c.IsNutritionFatCriticalLevel(), "gordura", "Falta de gorduras", "gorduras", Pct(c.GetNutritionFat(), c.GetMaxNutritionFat()));
            Status(c.IsNutritionProteinsCriticalLevel(), "proteina", "Falta de proteínas", "proteinas", Pct(c.GetNutritionProtein(), c.GetMaxNutritionProtein()));
            Status(c.GetEnergy() < c.GetMaxEnergy() * 0.2f, "energia", "Energia baixa", "energia", Pct(c.GetEnergy(), c.GetMaxEnergy()));
            var san = PlayerSanityModule.Get();
            if (san != null) Status(san.m_Sanity < 30, "sanidade", "Sanidade baixa", "sanidade", $"{san.m_Sanity} de {PlayerSanityModule.MAX_SANITY}");
        }

        static string Pct(float v, float max) => max > 0 ? $"{Mathf.RoundToInt(v / max * 100)}%" : "";

        // ---------------- Tratamentos (tabela do próprio jogo: BIWoundSlot.CanInsertItem) ----------------
        void Tratamentos(Alerta a, InjuryType tipo, bool infectado, InjuryState estado)
        {
            if (infectado)
            {
                a.linhas.Add(LinhaItem(ItemID.Maggots));
                a.linhas.Add(LinhaItem(ItemID.Honey_Dressing));
                return;
            }
            switch (tipo)
            {
                case InjuryType.Laceration:
                case InjuryType.LacerationCat:
                    a.linhas.Add(LinhaItem(ItemID.Ants));
                    a.linhas.Add(LinhaItem(ItemID.ash_dressing));
                    a.linhas.Add(LinhaItem(ItemID.Goliath_dressing));
                    a.linhas.Add(LinhaItem(ItemID.Honey_Dressing));
                    a.linhas.Add(LinhaItem(ItemID.Leaf_Bandage, null, estado == InjuryState.Bleeding ? "Em ferida sangrando, infecciona. Só em emergência." : null));
                    break;
                case InjuryType.VenomBite:
                case InjuryType.SnakeBite:
                    a.linhas.Add(LinhaItem(ItemID.Tabaco_Dressing));
                    a.linhas.Add(LinhaItem(ItemID.lily_dressing));
                    foreach (var (id, v) in Antiveneno().Take(2)) a.linhas.Add(LinhaItem(id, $"−{v} veneno", null, "comer"));
                    break;
                case InjuryType.Rash:
                    a.linhas.Add(LinhaItem(ItemID.lily_dressing));
                    a.linhas.Add(LinhaItem(ItemID.Honey_Dressing));
                    break;
                case InjuryType.Worm:
                    a.linhas.Add(LinhaItem(ItemID.Bone_Needle));
                    a.linhas.Add(LinhaItem(ItemID.Fish_Bone));
                    a.linhas.Add(LinhaItem(ItemID.Stingray_sting));
                    break;
                case InjuryType.Leech:
                    a.linhas.Add(C(Ok, "✓") + "  Arranque com a mão. Não precisa de item.");
                    break;
                default: // escoriação, arranhão, buraco de verme aberto: qualquer curativo
                    foreach (var id in Curativos().OrderByDescending(Jogo.QuantosTenho).Take(4)) a.linhas.Add(LinhaItem(id));
                    break;
            }
        }

        static string LinhaItem(ItemID id, string efeito = null, string risco = null, string acao = null)
        {
            int tenho = Jogo.QuantosTenho(id);
            string marca = risco != null ? C(Aviso, "!") : tenho > 0 ? C(Ok, "✓") : C(Apagado, "✕");
            string qtd = tenho > 0 ? C(Apagado, $"{tenho} na mochila") : C(Apagado, "não tem");
            string extra = efeito != null ? C(Apagado, $" ({efeito}{(acao != null ? ", " + acao : "")})") : "";
            string linha = $"{marca}  {Esc(Jogo.NomeItem(id))}{extra}  {qtd}";
            if (risco != null) linha += "\n     " + C(Aviso, risco);
            return linha;
        }

        void DicasDaFicha(Alerta a, int max)
        {
            var f = Guia.Get(a.fichaId);
            if (f == null || max <= 0) return;
            var secao = f.secoes.FirstOrDefault(s => s.tipo == "tratamento") ?? f.secoes.FirstOrDefault(s => s.tipo == "obter" || s.tipo == "dicas");
            if (secao == null) return;
            foreach (var it in secao.itens.Take(max)) a.linhas.Add(C(Destaque, "•") + "  " + Esc(it.t));
        }

        // Dados que o jogo carrega dos próprios arquivos de itens.
        static List<ItemID> curativos;
        static List<(ItemID, int)> antiveneno;
        static readonly Dictionary<ConsumeEffect, List<(ItemID, int)>> curas = new Dictionary<ConsumeEffect, List<(ItemID, int)>>();

        static List<ItemID> Curativos() =>
            curativos ??= ItemsManager.Get().GetAllInfosOfType(ItemType.Dressing).Select(i => i.m_ID).Distinct().ToList();

        static IEnumerable<(ItemID, int)> Antiveneno()
        {
            antiveneno ??= ItemsManager.Get().GetAllInfos().Values
                .Where(i => i is ConsumableInfo && i.m_PoisonDebuff > 0)
                .Select(i => (i.m_ID, i.m_PoisonDebuff)).ToList();
            return antiveneno.OrderByDescending(x => Jogo.QuantosTenho(x.Item1)).ThenByDescending(x => x.Item2);
        }

        static IEnumerable<(ItemID id, int efeito)> CurasDe(ConsumeEffect tipo)
        {
            if (!curas.TryGetValue(tipo, out var l))
            {
                l = ItemsManager.Get().GetAllInfos().Values.OfType<ConsumableInfo>()
                    .Where(i => i.m_ConsumeEffect == tipo && i.m_ConsumeEffectLevel < 0)
                    .Select(i => (i.m_ID, -i.m_ConsumeEffectLevel)).ToList();
                curas[tipo] = l;
            }
            return l.OrderByDescending(x => Jogo.QuantosTenho(x.Item1) > 0).ThenByDescending(x => x.Item2);
        }

        // ---------------- Textos ----------------
        static string NomeFerimento(InjuryType t)
        {
            switch (t)
            {
                case InjuryType.SmallWoundAbrassion: return "Escoriação";
                case InjuryType.SmallWoundScratch: return "Arranhão";
                case InjuryType.Laceration: return "Laceração";
                case InjuryType.LacerationCat: return "Laceração de felino";
                case InjuryType.Rash: return "Erupção na pele";
                case InjuryType.Worm: return "Verme sob a pele";
                case InjuryType.WormHole: return "Buraco de verme";
                case InjuryType.Leech: return "Sanguessuga";
                case InjuryType.VenomBite: return "Picada venenosa";
                case InjuryType.SnakeBite: return "Mordida de cobra";
                default: return t.ToString();
            }
        }

        static string FichaFerimento(InjuryType t)
        {
            switch (t)
            {
                case InjuryType.Laceration:
                case InjuryType.LacerationCat: return "laceracao";
                case InjuryType.Rash: return "erupcao-cutanea";
                case InjuryType.Worm:
                case InjuryType.WormHole: return "vermes-sob-a-pele";
                case InjuryType.Leech: return "sanguessugas";
                case InjuryType.VenomBite:
                case InjuryType.SnakeBite: return "ferimento-por-veneno";
                default: return "arranhoes-e-escoriacoes";
            }
        }

        static string NomeDoenca(ConsumeEffect t)
        {
            switch (t)
            {
                case ConsumeEffect.FoodPoisoning: return "Intoxicação alimentar";
                case ConsumeEffect.Fever: return "Febre";
                case ConsumeEffect.ParasiteSickness: return "Parasitas";
                case ConsumeEffect.Insomnia: return "Insônia";
                case ConsumeEffect.DirtSickness: return "Sujeira";
                default: return t.ToString();
            }
        }

        static string FichaDoenca(ConsumeEffect t)
        {
            switch (t)
            {
                case ConsumeEffect.FoodPoisoning: return "intoxicacao-alimentar";
                case ConsumeEffect.Fever: return "febre";
                case ConsumeEffect.ParasiteSickness: return "parasitas";
                case ConsumeEffect.Insomnia: return "insonia";
                default: return "sujeira";
            }
        }

        static string Lugar(InjuryPlace p)
        {
            switch (p)
            {
                case InjuryPlace.LHand: return "braço esquerdo";
                case InjuryPlace.RHand: return "braço direito";
                case InjuryPlace.LLeg: return "perna esquerda";
                case InjuryPlace.RLeg: return "perna direita";
                default: return "corpo";
            }
        }

        static string Estado(InjuryState s)
        {
            switch (s)
            {
                case InjuryState.Bleeding: return C(Perigo, "sangrando");
                case InjuryState.Open: return "aberto";
                case InjuryState.WormInside: return "com verme dentro";
                default: return null;
            }
        }

        // Silhueta (Data/img/<id>.png) do bicho que causou o ferimento.
        static string ImagemCausa(AI.AIID id)
        {
            switch (id)
            {
                case AI.AIID.Jaguar: case AI.AIID.Jaguar_Arena: case AI.AIID.Jaguar_Arena_Farmer: return "onca-pintada";
                case AI.AIID.Puma: case AI.AIID.Puma_Arena_Farmer: return "puma";
                case AI.AIID.BlackPanther: case AI.AIID.Quest_BlackPanther: return "pantera-negra";
                case AI.AIID.BlackCaiman: case AI.AIID.BlackCaiman_Arena_Fishing: return "jacare-acu";
                case AI.AIID.AlbinoCaiman: return "jacare-albino";
                case AI.AIID.Piranha: return "piranha";
                case AI.AIID.SouthAmericanRattlesnake: return "cascavel";
                case AI.AIID.GreenAnaconda: case AI.AIID.BoaConstrictor: return "sucuri";
                case AI.AIID.GoliathBirdEater: return "aranha-golias";
                case AI.AIID.BrasilianWanderingSpider: return "aranha-armadeira";
                case AI.AIID.Scorpion: return "escorpiao";
                case AI.AIID.Stingray: return "arraia";
                case AI.AIID.PoisonDartFrog: return "sapo-dardo";
                case AI.AIID.Centipede: return "centopeia";
                case AI.AIID.GiantAnteater: return "tamandua-bandeira";
                case AI.AIID.Peccary: case AI.AIID.Peccary_Arena: return "cateto";
                default: return null;
            }
        }

        static string ImagemFerimento(InjuryType t)
        {
            switch (t)
            {
                case InjuryType.Leech: return "sanguessuga";
                case InjuryType.Worm: case InjuryType.WormHole: return "vermes";
                case InjuryType.SnakeBite: return "cascavel";
                default: return null;
            }
        }

        static string Causa(AI.AIID id)
        {
            switch (id)
            {
                case AI.AIID.Jaguar: case AI.AIID.Jaguar_Arena: case AI.AIID.Jaguar_Arena_Farmer: return "onça";
                case AI.AIID.Puma: case AI.AIID.Puma_Arena_Farmer: return "puma";
                case AI.AIID.BlackPanther: case AI.AIID.Quest_BlackPanther: return "pantera negra";
                case AI.AIID.BlackCaiman: case AI.AIID.BlackCaiman_Arena_Fishing: return "jacaré";
                case AI.AIID.AlbinoCaiman: return "jacaré albino";
                case AI.AIID.Piranha: return "piranha";
                case AI.AIID.SouthAmericanRattlesnake: return "cascavel";
                case AI.AIID.GreenAnaconda: return "sucuri";
                case AI.AIID.BoaConstrictor: return "jiboia";
                case AI.AIID.GoliathBirdEater: return "aranha golias";
                case AI.AIID.BrasilianWanderingSpider: return "aranha armadeira";
                case AI.AIID.Scorpion: return "escorpião";
                case AI.AIID.Stingray: return "arraia";
                case AI.AIID.PoisonDartFrog: return "sapo-dardo";
                case AI.AIID.Centipede: return "centopeia";
                case AI.AIID.GiantAnteater: return "tamanduá";
                case AI.AIID.Peccary: case AI.AIID.Peccary_Arena: return "cateto";
                case AI.AIID.Regular: case AI.AIID.Hunter: case AI.AIID.Spearman: case AI.AIID.Thug: case AI.AIID.Savage:
                case AI.AIID.Savage_Arena_Tribe: case AI.AIID.Spearman_Arena_Tribe: return "indígena";
                default: return null;
            }
        }

        // ---------------- Desenho ----------------
        // ---------- Posição: canto inferior esquerdo, empilhando para cima a partir dos macroelementos ----------
        public static float Largura => Mathf.Max(Screen.width * 0.22f, Px(300));
        public static float X => 25 * Screen.height / 1080f;                      // alinhado à HUD do jogo
        /// <summary>Base da pilha: um pouco acima da linha de cima dos macroelementos (HUD em 1080p: y ≈ 949).</summary>
        public static float Base => (Plugin.MostrarMacros.Value ? 842f : 980f) * Screen.height / 1080f;
        static float Topo => Screen.height * 0.30f;

        public void Desenhar()
        {
            if (ativos.Count == 0) return;
            float largura = Largura, x = X, gap = Px(8);
            float limite = Time.time - Plugin.SegundosAlerta.Value;

            // mais novo primeiro; os que não couberem entre a base e o topo viram linha em "Condições ativas"
            var completos = ativos.Values.Where(a => a.criado >= limite && !a.tratando).OrderByDescending(a => a.criado).Take(3).ToList();
            var blocos = completos.Select(Cartao).ToList();
            Bloco resumo;
            while (true)
            {
                resumo = Resumo(ativos.Values.Except(completos).OrderBy(a => a.chave).ToList());
                float total = blocos.Sum(b => b.Altura(largura) + gap) + (resumo != null ? resumo.Altura(largura) + gap : 0);
                if (total <= Base - Topo || completos.Count == 0) break;
                completos.RemoveAt(completos.Count - 1);
                blocos.RemoveAt(blocos.Count - 1);
            }

            // de baixo para cima: resumo encostado na HUD, depois o aviso mais novo, depois os mais antigos
            float y = Base;
            if (resumo != null)
            {
                float h = resumo.Altura(largura);
                y -= h;
                resumo.Desenhar(new Rect(x, y, largura, h), Painel);
                y -= gap;
            }
            for (int i = 0; i < blocos.Count; i++)
            {
                var b = blocos[i];
                float h = b.Altura(largura);
                y -= h;
                // aviso novo entra deslizando da esquerda com fade (0,3 s)
                float e = Plugin.Animacoes.Value ? 1 - Mathf.Pow(1 - Mathf.Clamp01((Time.time - completos[i].criado) / 0.3f), 3) : 1;
                var rect = new Rect(x - (1 - e) * Px(40), y, largura, h);
                ComOpacidade(e, () => b.Desenhar(rect, Painel));
                y -= gap;
            }
        }

        static Bloco Cartao(Alerta a)
        {
            var b = new Bloco { Faixa = a.corRotulo, Imagem = Imagens.Get(a.imagem) };
            b.Add(Rotulo, C(a.corRotulo, a.rotulo));
            b.Add(Titulo, Esc(a.titulo), 0, 2);
            if (!string.IsNullOrEmpty(a.onde)) b.Add(Pequeno, a.onde, 0, 1);
            for (int i = 0; i < a.linhas.Count; i++) b.Add(Texto, a.linhas[i], 0, i == 0 ? 6 : 2);
            string tecla = Plugin.TeclaGuia.Value.MainKey.ToString();
            b.Add(Pequeno, $"{a.rodape} Ficha completa: {C(Tinta, tecla)}", 0, 6);
            return b;
        }

        static Bloco Resumo(List<Alerta> resto)
        {
            if (resto.Count == 0) return null;
            var r = new Bloco();
            r.Add(Rotulo, "CONDIÇÕES ATIVAS");
            foreach (var a in resto)
                r.Add(Pequeno, $"{C(a.corRotulo, "•")}  {C(Tinta, Esc(a.titulo))}{(a.tratando ? "  " + C(Ok, "tratando") : "")}", 0, 3);
            return r;
        }
    }
}
