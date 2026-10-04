using System.Collections.Generic;
using System.Linq;
using AIs;
using Enums;
using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    /// <summary>
    /// Cartão "Dica" no canto superior direito sobre o que está na mira: água, frutas, larvas, cogumelos
    /// e outros itens, animais vivos e mortos. Os números vêm dos dados do próprio jogo; o resumo e a
    /// melhor arma vêm das fichas do guia.
    /// </summary>
    public class Dicas
    {
        class Dica
        {
            public string chave, titulo, rotulo = "DICA", fichaId, imagem;
            public List<string> linhas = new List<string>();
        }

        Dica atual, mostrando;
        float proxima, vistoEm = -99, trocouEm;
        const float Permanencia = 1.2f;   // continua na tela um pouco depois de desviar o olhar

        public string FichaAtual => mostrando != null && Time.unscaledTime - vistoEm < Permanencia ? mostrando.fichaId : null;

        public void Atualizar()
        {
            if (!Plugin.Dicas.Value || Time.unscaledTime < proxima) return;
            proxima = Time.unscaledTime + 0.2f;
            atual = null;
            try { atual = DoGatilho(TriggerController.Get()?.GetBestTrigger()) ?? AnimalNaMira(); }
            catch (System.Exception e) { Plugin.Erro("dica", e); }
            if (atual == null) return;
            if (mostrando == null || mostrando.chave != atual.chave || Time.unscaledTime - vistoEm >= Permanencia) trocouEm = Time.unscaledTime;
            mostrando = atual;
            vistoEm = Time.unscaledTime;
        }

        // ---------- O que está na mira ----------
        Dica DoGatilho(Trigger t)
        {
            if (t == null) return null;
            switch (t)
            {
                case LiquidSource ls: return DeAgua(ls.m_LiquidType);
                case DeadBody db: return DeAnimal(db.m_AIID, true);
                case PlantFruit pf when pf.m_ItemInfo != null: return DeItem(pf.m_ItemInfo);
                case ItemReplacer ir when ir.m_ReplaceInfo != null: return DeItem(ir.m_ReplaceInfo);
                case global::Item it when it.m_Info != null: return DeItem(it.m_Info);
            }
            return null;
        }

        /// <summary>Animal vivo mais alinhado com o centro da tela, até 60 m.</summary>
        Dica AnimalNaMira()
        {
            var am = AIManager.Get();
            if (am == null) return null;
            var cam = Jogo.Camera();
            AI melhor = null;
            float melhorAng = 999;
            foreach (var ai in am.m_ActiveAIs)
            {
                if (ai == null || ai.IsDead() || ai.m_Hallucination || ai.m_BoxCollider == null) continue;
                var alvo = ai.m_BoxCollider.bounds.center;
                var d = alvo - cam.position;
                float dist = d.magnitude;
                if (dist > 60 || dist < 0.5f) continue;
                float ang = Vector3.Angle(cam.forward, d);
                float tolerancia = Mathf.Max(3f, Mathf.Atan2(ai.m_BoxCollider.bounds.extents.magnitude, dist) * Mathf.Rad2Deg);
                if (ang < tolerancia && ang < melhorAng) { melhor = ai; melhorAng = ang; }
            }
            return melhor != null ? DeAnimal(melhor.m_ID, false) : null;
        }

        // ---------- Água ----------
        Dica DeAgua(LiquidType tipo)
        {
            var d = LiquidManager.Get()?.GetLiquidData(tipo);
            var dica = new Dica { chave = "agua:" + tipo, titulo = Local("LiquidType_" + tipo, tipo.ToString()), fichaId = "agua" };
            if (d == null) return dica;
            var ruins = d.m_ConsumeEffects.Where(e => e.m_ConsumeEffectLevel > 0 && e.m_ConsumeEffectChance > 0).ToList();
            if (ruins.Count > 0)
                dica.linhas.Add(C(Perigo, "Consumir esta água pode causar ") + string.Join(", ", ruins.Select(e => $"<b>{Doenca(e.m_ConsumeEffect)}</b> ({Pct(e.m_ConsumeEffectChance)} por gole)")) + ".");
            else if (d.m_Water > 0)
                dica.linhas.Add(C(Ok, "Segura para beber."));
            float gole = 40f;   // Player.m_SipHydration padrão
            if (d.m_Water > 0) dica.linhas.Add($"Cada gole: +{Mathf.FloorToInt(gole / 100f * d.m_Water)} de hidratação.");
            if (d.m_Dehydration > 0) dica.linhas.Add(C(Aviso, $"Desidrata: −{Mathf.FloorToInt(gole / 100f * d.m_Dehydration)} por gole."));
            if (d.m_SanityChange < 0) dica.linhas.Add(C(Aviso, $"Sanidade {d.m_SanityChange}."));
            if (d.m_CookingResult != LiquidType.None && d.m_CookingResult != tipo && ruins.Count > 0)
                dica.linhas.Add($"Ferva antes num recipiente no fogo: vira {C(Ok, Local("LiquidType_" + d.m_CookingResult, d.m_CookingResult.ToString()))}.");
            if (tipo == LiquidType.UnsafeWater || tipo == LiquidType.DirtyWater)
                dica.linhas.Add(C(Apagado, "Coco, água da chuva e coletor de água também são seguros."));
            return dica;
        }

        // ---------- Itens ----------
        Dica DeItem(ItemInfo info)
        {
            var dica = new Dica { chave = "item:" + info.m_ID, titulo = info.GetNameToDisplayLocalized() };
            var ficha = FichaDoItem(info.m_ID);
            dica.fichaId = ficha?.id;
            dica.imagem = ficha != null && Imagens.Get(ficha.id) != null ? ficha.id : null;
            string nomeId = info.m_ID.ToString().ToLowerInvariant();

            if (info is ConsumableInfo c)
            {
                var fi = info as FoodInfo;
                if (fi != null && fi.m_State == FoodState.Spoiled) dica.linhas.Add(C(Perigo, "Estragado: não coma. Serve de isca ou para atrair larvas."));
                else if (fi != null && fi.m_State == FoodState.Burned) dica.linhas.Add(C(Aviso, "Queimado: quase não alimenta."));
                else if (nomeId.Contains("_raw") || (fi != null && fi.m_CanCook && info.IsMeat() && fi.m_State == FoodState.Normal))
                    dica.linhas.Add(C(Aviso, "Cru: cozinhe, defume ou seque antes de comer."));

                if (c.m_ConsumeEffect != ConsumeEffect.None && c.m_ConsumeEffectLevel > 0 && c.m_ConsumeEffectChance > 0)
                    dica.linhas.Add(C(Perigo, "Pode causar ") + $"<b>{Doenca(c.m_ConsumeEffect)}</b> ({Pct(c.m_ConsumeEffectChance)}).");
                if (c.m_ConsumeEffect != ConsumeEffect.None && c.m_ConsumeEffectLevel < 0)
                    dica.linhas.Add(C(Ok, $"Ajuda contra {Doenca(c.m_ConsumeEffect)} (−{-c.m_ConsumeEffectLevel})."));
                if (info.m_PoisonDebuff > 0) dica.linhas.Add(C(Ok, $"Reduz o veneno (−{info.m_PoisonDebuff})."));
                if (c.m_SanityChange != 0) dica.linhas.Add(c.m_SanityChange > 0 ? C(Ok, $"Sanidade +{c.m_SanityChange}.") : C(Aviso, $"Sanidade {c.m_SanityChange}."));

                var nut = new List<string>();
                if (c.m_Proteins > 0) nut.Add($"proteína {c.m_Proteins:0}");
                if (c.m_Fat > 0) nut.Add($"gordura {c.m_Fat:0}");
                if (c.m_Carbohydrates > 0) nut.Add($"carboidrato {c.m_Carbohydrates:0}");
                if (c.m_Water > 0) nut.Add($"água {c.m_Water:0}");
                if (nut.Count > 0) dica.linhas.Add("Alimenta: " + string.Join(" · ", nut) + ".");
                if (c.m_Dehydration > 0) dica.linhas.Add(C(Aviso, $"Dá sede (−{c.m_Dehydration:0} de hidratação)."));
            }
            if (ficha != null) dica.linhas.Add(C(Apagado, Esc(Curto(ficha.resumo))));
            return dica.linhas.Count > 0 ? dica : null;
        }

        // ---------- Animais ----------
        Dica DeAnimal(AI.AIID id, bool morto)
        {
            var ficha = FichaDoAnimal(id);
            var dica = new Dica
            {
                chave = (morto ? "morto:" : "ai:") + id,
                titulo = Local(id.ToString(), ficha?.nome ?? id.ToString()),
                rotulo = morto ? "DICA · ANIMAL ABATIDO" : "DICA",
                fichaId = ficha?.id,
                imagem = ficha != null && Imagens.Get(ficha.id) != null ? ficha.id : null,
            };
            if (morto)
            {
                var obtem = Esfolar(id);
                if (obtem.Count > 0) dica.linhas.Add("Esfolando, dá: " + string.Join(", ", obtem) + ".");
                dica.linhas.Add(C(Apagado, "A carne estraga com o tempo: cozinhe, defume ou seque logo."));
            }
            else if (ficha != null)
            {
                if (ficha.perigo >= 4) dica.linhas.Add(C(Perigo, $"<b>Perigo {ficha.perigo}/5.</b>") + " " + Esc(Curto(ficha.resumo)));
                else dica.linhas.Add(Esc(Curto(ficha.resumo)));
                var armas = ficha.secoes.FirstOrDefault(s => s.tipo == "armas");
                if (armas != null && armas.itens.Count > 0) dica.linhas.Add($"Melhor arma: <b>{Esc(armas.itens[0].t)}</b>.");
                var evitar = ficha.secoes.FirstOrDefault(s => s.tipo == "evitar");
                if (ficha.perigo >= 3 && evitar != null && evitar.itens.Count > 0) dica.linhas.Add(C(Aviso, Esc(evitar.itens[0].t)));
            }
            return dica.linhas.Count > 0 ? dica : null;
        }

        static List<string> Esfolar(AI.AIID id)
        {
            var r = new List<string>();
            try
            {
                if (!AIManager.Get().m_AIParamsMap.TryGetValue((int)id, out var p)) return r;
                foreach (var g in p.m_HarvestingResult.Where(x => x != null).GroupBy(x => x.GetComponent<global::Item>()?.m_InfoName))
                {
                    if (g.Key == null || !EnumUtils<ItemID>.TryGetValue(g.Key, out var item)) continue;
                    r.Add((g.Count() > 1 ? g.Count() + "× " : "") + Esc(Jogo.NomeItem(item)));
                }
            }
            catch { }
            return r;
        }

        // ---------- Ligação com as fichas do guia ----------
        static Dictionary<string, Ficha> porNome;

        static void Indexar()
        {
            if (porNome != null) return;
            porNome = new Dictionary<string, Ficha>();
            foreach (var f in Guia.Fichas)
                foreach (var k in new[] { f.nomeEn, f.nome, f.id.Replace('-', ' ') }.Concat(f.aliases))
                {
                    var n = Guia.Norm(k);
                    if (n.Length > 2 && !porNome.ContainsKey(n)) porNome[n] = f;
                }
        }

        // Palavras de estado/forma que não mudam a ficha (Fat_Meat_Cooked_Spoiled → "meat").
        static readonly HashSet<string> Estados = new HashSet<string>(
            "raw cooked coocked smoked dryed dried spoiled burned boiled whole flesh shell bowl green item fruit flowers flower leaf leaves bulb powder 1 2 3 4 5 6 7 8 9".Split(' '));
        // Palavras genéricas que, sozinhas, apontariam para a ficha errada (ex.: "fat" → Gorduras).
        static readonly HashSet<string> Genericas = new HashSet<string> { "fat", "lean", "meat", "fish" };
        // Famílias de itens sem nome próprio nas fichas.
        static readonly Dictionary<string, string> Familias = new Dictionary<string, string>
        {
            ["meat"] = "cozinhar-carne", ["fish"] = "cozinhar-carne", ["coconut"] = "coco", ["cassava"] = "mandioca",
            ["nuts"] = "castanha-do-para", ["brazil"] = "castanha-do-para", ["egg"] = "ovo", ["quassia"] = "quassia-amara",
            ["tobacco"] = "tabaco", ["anthill"] = "formigas", ["ants"] = "formigas", ["honey"] = "favo-de-mel",
            ["monstera"] = "fruta-de-monstera", ["pirahnia"] = "piranha", ["arowana"] = "aruana", ["peacock"] = "tucunare",
            ["prawn"] = "camarao", ["snail"] = "caramujo", ["larva"] = "larva", ["maggots"] = "larvas-de-mosca",
            ["banana"] = "banana", ["cocona"] = "cocona", ["guanabana"] = "graviola", ["heart"] = "palmito",
            ["molineria"] = "molineria", ["malanga"] = "malanga", ["mushroom"] = "cogumelos", ["unknown"] = "cogumelos",
        };

        static Ficha FichaDoItem(ItemID id)
        {
            Indexar();
            var toks = Guia.Norm(id.ToString().Replace('_', ' ')).Split(' ');
            if (porNome.TryGetValue(string.Join(" ", toks), out var f)) return f;
            var limpo = toks.Where(t => !Estados.Contains(t)).ToArray();
            bool Tenta(string[] partes, out Ficha r)
            {
                r = null;
                if (partes.Length == 0 || (partes.Length == 1 && Genericas.Contains(partes[0]))) return false;
                return porNome.TryGetValue(string.Join(" ", partes), out r);
            }
            for (int n = limpo.Length; n >= 1; n--) if (Tenta(limpo.Take(n).ToArray(), out f)) return f;      // Pirahnia_Meat → pirahnia
            for (int s = 1; s < limpo.Length; s++) if (Tenta(limpo.Skip(s).ToArray(), out f)) return f;       // Peacock_Bass → bass…
            foreach (var t in toks) if (Familias.TryGetValue(t, out var fid) && Guia.Get(fid) != null) return Guia.Get(fid);
            return null;
        }

        static Ficha FichaDoAnimal(AI.AIID id)
        {
            Indexar();
            // "BlackPanther" → "black panther"
            string n = Guia.Norm(System.Text.RegularExpressions.Regex.Replace(id.ToString().Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " "));
            if (porNome.TryGetValue(n, out var f)) return f;
            switch (id)
            {
                case AI.AIID.BlackCaiman: case AI.AIID.BlackCaiman_Arena_Fishing: return Guia.Get("jacare-acu");
                case AI.AIID.AlbinoCaiman: return Guia.Get("jacare-albino");
                case AI.AIID.SouthAmericanRattlesnake: return Guia.Get("cascavel");
                case AI.AIID.GreenAnaconda: case AI.AIID.BoaConstrictor: return Guia.Get("sucuri");
                case AI.AIID.GoliathBirdEater: return Guia.Get("aranha-golias");
                case AI.AIID.BrasilianWanderingSpider: return Guia.Get("aranha-armadeira");
                case AI.AIID.PoisonDartFrog: return Guia.Get("sapo-dardo");
                case AI.AIID.CaneToad: return Guia.Get("sapo-cururu");
                case AI.AIID.Peccary: return Guia.Get("cateto");
                case AI.AIID.Tapir_baby: return Guia.Get("filhote-de-anta");
                case AI.AIID.ArmadilloThreeBanded: return Guia.Get("tatu-bola");
                case AI.AIID.RedFootedTortoise: case AI.AIID.MudTurtle: return Guia.Get("jabuti");
                case AI.AIID.CaimanLizard: return Guia.Get("jacuruxi");
                case AI.AIID.GiantAnteater: return Guia.Get("tamandua-bandeira");
                case AI.AIID.Regular: case AI.AIID.Savage: return Guia.Get("guerreiro-waraha");
                case AI.AIID.Hunter: return Guia.Get("cacador-waraha");
                case AI.AIID.Spearman: return Guia.Get("lanceiro-waraha");
                case AI.AIID.Thug: return Guia.Get("brutamontes-waraha");
                case AI.AIID.Arowana: return Guia.Get("aruana");
                case AI.AIID.PeacockBass: return Guia.Get("tucunare");
                case AI.AIID.AngelFish: return Guia.Get("acara-bandeira");
                case AI.AIID.DiscusFish: return Guia.Get("acara-disco");
                case AI.AIID.Stingray: return Guia.Get("arraia");
                case AI.AIID.Mouse: return Guia.Get("rato");
                case AI.AIID.GreenIguana: return Guia.Get("iguana");
                default: return null;
            }
        }

        // ---------- Texto ----------
        static string Local(string chave, string reserva)
        {
            var loc = GreenHellGame.Instance.GetLocalization();
            return loc != null && loc.Contains(chave) ? loc.Get(chave) : reserva;
        }

        static string Pct(float chance) => $"{Mathf.RoundToInt(Mathf.Clamp01(chance) * 100)}% de chance";

        static string Doenca(ConsumeEffect e)
        {
            switch (e)
            {
                case ConsumeEffect.FoodPoisoning: return "intoxicação alimentar";
                case ConsumeEffect.Fever: return "febre";
                case ConsumeEffect.ParasiteSickness: return "parasitas";
                case ConsumeEffect.Insomnia: return "insônia";
                case ConsumeEffect.DirtSickness: return "sujeira";
                default: return e.ToString();
            }
        }

        /// <summary>Primeira frase do resumo da ficha.</summary>
        static string Curto(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf(". ");
            return i > 0 ? s.Substring(0, i + 1) : s;
        }

        // ---------- Desenho ----------
        /// <summary>Desenha no canto superior direito a partir de y (abaixo do card de construção, se houver).</summary>
        public float Desenhar(float y)
        {
            if (!Plugin.Dicas.Value || mostrando == null) return y;
            float desde = Time.unscaledTime - vistoEm;
            if (desde >= Permanencia) return y;

            var b = new Bloco { Faixa = Info, Imagem = Imagens.Get(mostrando.imagem), Lado = Tema.Lado.Direita };
            b.Add(Rotulo, C(Info, mostrando.rotulo));
            b.Add(Titulo, Esc(mostrando.titulo), 0, 2);
            for (int i = 0; i < mostrando.linhas.Count; i++) b.Add(Texto, mostrando.linhas[i], 0, i == 0 ? 6 : 3);
            if (mostrando.fichaId != null) b.Add(Pequeno, $"Ficha completa: {C(Tinta, Plugin.TeclaGuia.Value.MainKey.ToString())}", 0, 6);

            float largura = Mathf.Max(Screen.width * 0.22f, Px(300));
            float h = b.Altura(largura);
            float x = Screen.width - largura - Screen.width * 0.016f;
            // entra com fade/deslize; ao desviar o olhar, some com fade
            float e = Entrada(trocouEm, 0.2f);
            float saida = Plugin.Animacoes.Value ? Mathf.Clamp01((Permanencia - desde) / 0.3f) : (desde < 0.25f ? 1 : 0);
            var rect = new Rect(x + (1 - e) * Px(24), y, largura, h);
            ComOpacidade(Mathf.Min(e, saida), () => b.Desenhar(rect, Painel));
            return y + h + Px(8);
        }
    }
}
