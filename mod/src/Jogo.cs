using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace GreenHellCompanion
{
    /// <summary>Leituras do estado do jogo usadas por todos os painéis.</summary>
    public static class Jogo
    {
        public static bool EmJogo()
        {
            var ml = MainLevel.Instance;
            var p = Player.Get();
            var gh = GreenHellGame.Instance;
            if (ml == null || p == null || gh == null) return false;
            if (!ml.m_LevelStarted || SaveGame.m_State != SaveGame.State.None) return false;
            if (gh.m_LoadState != GreenHellGame.LoadState.None) return false;
            var ls = LoadingScreen.Get();
            if (ls != null && ls.m_Active) return false;
            return !p.IsDead();
        }

        /// <summary>HUD só aparece em jogo livre: sem menu, pausa, vídeo, caderno ou mapa.</summary>
        public static bool PodeUsarHud()
        {
            var menu = MenuInGameManager.Get();
            if (menu != null && menu.IsAnyScreenVisible()) return false;
            var ml = MainLevel.Instance;
            if (ml.IsPause() || ml.IsMoviePlaying()) return false;
            if (Ativo(NotepadController.Get()) || Ativo(MapController.Get())) return false;
            if (InspecionandoCorpo()) return false;
            return true;
        }

        public static bool InspecionandoCorpo() => Ativo(BodyInspectionController.Get());

        static bool Ativo(PlayerController c) => c != null && c.IsActive();

        // ---------- Itens ----------
        static readonly Dictionary<ItemID, string> nomes = new Dictionary<ItemID, string>();

        /// <summary>Nome do item no idioma em que o jogo está.</summary>
        public static string NomeItem(ItemID id)
        {
            if (nomes.TryGetValue(id, out var n)) return n;
            var chave = id.ToString();
            var loc = GreenHellGame.Instance.GetLocalization();
            n = loc != null && loc.Contains(chave) ? loc.Get(chave) : chave.Replace('_', ' ');
            nomes[id] = n;
            return n;
        }

        /// <summary>Ícone do item (o mesmo da mochila), lido do próprio jogo. Null se não houver.</summary>
        public static Sprite Icone(ItemID id)
        {
            if (id == ItemID.None) return null;
            var im = ItemsManager.Get();
            var info = im?.GetInfo(id);
            if (info == null || string.IsNullOrEmpty(info.m_IconName)) return null;
            return im.m_ItemIconsSprites.TryGetValue(info.m_IconName, out var s) ? s : null;
        }

        static Dictionary<string, ItemID> porNomeIngles;
        static readonly Dictionary<string, ItemID> Apelidos = new Dictionary<string, ItemID>
        {
            ["ash"] = ItemID.Campfire_ash, ["ashes"] = ItemID.Campfire_ash, ["painkiller"] = ItemID.Painkillers,
        };

        /// <summary>
        /// Acha o item pelo nome em inglês que as fichas trazem entre parênteses ("Molineria Leaf" → Molineria_leaf).
        /// Tenta o nome exato, singular, "+ leaf" e, por último, o primeiro item que começa com o nome.
        /// </summary>
        public static ItemID ItemPorNome(string ingles)
        {
            if (string.IsNullOrWhiteSpace(ingles)) return ItemID.None;
            if (porNomeIngles == null)
            {
                porNomeIngles = new Dictionary<string, ItemID>();
                foreach (ItemID id in System.Enum.GetValues(typeof(ItemID)))
                {
                    var n = Guia.Norm(id.ToString().Replace('_', ' '));
                    if (n.Length > 1 && !porNomeIngles.ContainsKey(n)) porNomeIngles[n] = id;
                }
            }
            string k = Guia.Norm(ingles);
            if (Apelidos.TryGetValue(k, out var a)) return a;
            if (porNomeIngles.TryGetValue(k, out var r)) return r;
            if (k.EndsWith("leaves") && porNomeIngles.TryGetValue(k.Substring(0, k.Length - 6) + "leaf", out r)) return r;
            if (k.EndsWith("s") && porNomeIngles.TryGetValue(k.Substring(0, k.Length - 1), out r)) return r;
            if (porNomeIngles.TryGetValue(k + " leaf", out r)) return r;
            foreach (var kv in porNomeIngles)
                if (kv.Key.StartsWith(k + " ") && !kv.Key.Contains("seeds") && !kv.Key.Contains("reward")) return kv.Value;
            return ItemID.None;
        }

        static readonly System.Text.RegularExpressions.Regex Parenteses = new System.Text.RegularExpressions.Regex(@"\(([^()]+)\)\s*$");

        /// <summary>Ícone para uma linha das fichas, pelo nome em inglês entre parênteses no fim ("… (Molineria Leaf)").</summary>
        public static Sprite IconeDoTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            var m = Parenteses.Match(texto);
            if (!m.Success) return null;
            // "(Stone / Bone / Tribal Spear)": vários itens numa linha, sem ícone para não mostrar o errado
            string nome = m.Groups[1].Value;
            if (nome.IndexOfAny(new[] { '/', ',', ';' }) >= 0) return null;
            var id = ItemPorNome(nome.Trim());
            // segunda tentativa: o nome em português antes dos parênteses, comparado com os nomes do jogo no idioma atual
            if (id == ItemID.None) id = ItemPorNomeDoJogo(texto.Substring(0, m.Index));
            return Icone(id);
        }

        static Dictionary<string, ItemID> porNomeDoJogo;

        /// <summary>Item cujo nome no idioma do jogo é exatamente este texto ("1x Pena" → Pena).</summary>
        public static ItemID ItemPorNomeDoJogo(string texto)
        {
            if (porNomeDoJogo == null)
            {
                var loc = GreenHellGame.Instance?.GetLocalization();
                if (loc == null || ItemsManager.Get() == null) return ItemID.None;   // tenta de novo quando o jogo estiver pronto
                porNomeDoJogo = new Dictionary<string, ItemID>();
                foreach (var info in ItemsManager.Get().GetAllInfos().Values)
                {
                    if (info == null || !loc.Contains(info.m_ID.ToString())) continue;
                    var n = Guia.Norm(loc.Get(info.m_ID.ToString()));
                    if (n.Length > 2 && !porNomeDoJogo.ContainsKey(n)) porNomeDoJogo[n] = info.m_ID;
                }
            }
            // tira quantidades e sinais do começo: "1x Pena", "+ cinzas"
            string k = System.Text.RegularExpressions.Regex.Replace(Guia.Norm(texto), @"^(\d+\s*x?\s+|x\s+)", "").Trim();
            return porNomeDoJogo.TryGetValue(k, out var id) ? id : ItemID.None;
        }

        /// <summary>Quantos o jogador carrega: mochila + mãos (incluindo troncos/pedras empilhados no ombro).</summary>
        public static int QuantosTenho(ItemID id)
        {
            int n = 0;
            var mochila = InventoryBackpack.Get();
            if (mochila != null) n += mochila.GetItemsCount(id);
            var p = Player.Get();
            foreach (var mao in new[] { Hand.Right, Hand.Left })
            {
                var item = p.GetCurrentItem(mao);
                if (item == null || item.GetInfoID() != id) continue;
                n++;
                if (item.m_Info.IsHeavyObject() && item is HeavyObject pesado) n += pesado.m_Attached.Count;
            }
            return n;
        }

        /// <summary>Itens guardados em baús e estantes perto do jogador.</summary>
        public static int QuantosPerto(ItemID id, float raio)
        {
            int n = 0;
            var pos = Player.Get().transform.position;
            float r2 = raio * raio;
            foreach (var s in Storage.s_AllStorages)
            {
                if (s == null || (s.transform.position - pos).sqrMagnitude > r2) continue;
                foreach (var it in s.m_Items) if (it != null && it.GetInfoID() == id) n++;
            }
            foreach (var c in Construction.s_AllConstructions)
            {
                if (c is Stand st && st.GetStoredItemId() == id && (st.transform.position - pos).sqrMagnitude <= r2)
                    n += st.m_NumItems;
            }
            return n;
        }

        // ---------- Posição ----------
        public static Vector3 PosicaoJogador() => Player.Get().transform.position;

        public static Transform Camera()
        {
            var cm = CameraManager.Get();
            var cam = cm != null && cm.m_MainCamera != null ? cm.m_MainCamera : UnityEngine.Camera.main;
            return cam != null ? cam.transform : Player.Get().GetHeadTransform();
        }

        /// <summary>Coordenadas no formato do relógio do jogo, ex.: 21'W 34'S.</summary>
        public static string Gps(Vector3 pos)
        {
            try
            {
                Player.Get().GetGPSCoordinates(pos, out int w, out int s);
                return $"{w}'W {s}'S";
            }
            catch { return "--'W --'S"; }
        }

        public static Vector3 PontoTela(Vector3 mundo)
        {
            var cm = CameraManager.Get();
            if (cm != null) return cm.WorldToScreenPoint(mundo);
            return UnityEngine.Camera.main.WorldToScreenPoint(mundo);
        }

        /// <summary>Identifica a partida para guardar marcadores separados por save.</summary>
        public static string ChavePartida()
        {
            string sessao = null;
            try { sessao = P2PSession.Instance.GetSessionId(); } catch { }
            var modo = GreenHellGame.Instance.m_GHGameMode.ToString();
            return string.IsNullOrEmpty(sessao) ? $"{modo}-{SaveGame.s_MainSaveName}" : $"{modo}-{sessao}";
        }
    }
}
