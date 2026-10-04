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
