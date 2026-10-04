using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static GreenHellCompanion.Estilo;

namespace GreenHellCompanion
{
    public class Marcador
    {
        public string nome, info;
        public float x, y, z;
        public bool ativo, morte;
        public Vector3 Pos => new Vector3(x, y, z);
        public Color Cor => morte ? Estilo.Cor(Estilo.Perigo) : Estilo.Cor(Estilo.Destaque);
    }

    /// <summary>Pontos marcados pelo jogador, salvos por partida.</summary>
    public class Marcadores
    {
        readonly string pasta;
        string chave;
        List<Marcador> lista = new List<Marcador>();
        readonly JanelaNome janelaNome;
        readonly JanelaLista janelaLista;

        public Marcadores(string pasta)
        {
            this.pasta = pasta;
            janelaNome = new JanelaNome(this);
            janelaLista = new JanelaLista(this);
        }

        public IReadOnlyList<Marcador> Lista => lista;
        public string ChaveAtual => chave;

        // ---------- Arquivo ----------
        string Arquivo => Path.Combine(pasta, "marcadores-" + string.Concat(chave.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_')) + ".json");

        public void Atualizar()
        {
            var k = Jogo.ChavePartida();
            if (k == chave) return;
            chave = k;
            lista = new List<Marcador>();
            try
            {
                if (File.Exists(Arquivo))
                {
                    var raiz = (Dictionary<string, object>)Json.Ler(File.ReadAllText(Arquivo));
                    lista = raiz.L("lista").OfType<Dictionary<string, object>>().Select(o => new Marcador
                    {
                        nome = o.S("nome") ?? "Marcador", info = o.S("info"), x = (float)o.N("x"), y = (float)o.N("y"), z = (float)o.N("z"),
                        ativo = o.B("ativo"), morte = o.B("morte"),
                    }).ToList();
                }
                Plugin.Log($"Marcadores da partida {chave}: {lista.Count}");
            }
            catch (Exception e) { Plugin.Erro("ler marcadores", e); }
        }

        public void Descarregar() => chave = null;

        public void Salvar()
        {
            if (chave == null) return;
            try
            {
                Directory.CreateDirectory(pasta);
                var dados = new Dictionary<string, object>
                {
                    ["lista"] = lista.Select(m => (object)new Dictionary<string, object> { ["nome"] = m.nome, ["info"] = m.info, ["x"] = m.x, ["y"] = m.y, ["z"] = m.z, ["ativo"] = m.ativo, ["morte"] = m.morte }).ToList(),
                };
                File.WriteAllText(Arquivo, Json.Escrever(dados));
            }
            catch (Exception e) { Plugin.Erro("salvar marcadores", e); }
        }

        // ---------- Ações ----------
        public void MarcarOlhando()
        {
            var cam = Jogo.Camera();
            Vector3 ponto = Jogo.PosicaoJogador();
            int mascara = LayerMask.GetMask("Default", "Terrain");
            if (Physics.Raycast(cam.position, cam.forward, out var hit, 400f, mascara, QueryTriggerInteraction.Ignore))
                ponto = hit.point;
            janelaNome.Novo(ponto, $"Marcador {lista.Count + 1}");
        }

        public void Adicionar(Vector3 p, string nome)
        {
            foreach (var m in lista) m.ativo = false;
            lista.Add(new Marcador { nome = nome, x = p.x, y = p.y, z = p.z, ativo = true });
            Salvar();
        }

        public void Ativar(Marcador m)
        {
            bool era = m.ativo;
            foreach (var o in lista) o.ativo = false;
            m.ativo = !era;
            Salvar();
        }

        public void Apagar(Marcador m) { lista.Remove(m); Salvar(); }

        /// <summary>
        /// Marca onde o jogador morreu (só existe um: o da última morte). No multiplayer o jogo solta a mochila
        /// nesse lugar e o jogador renasce longe, então o marcador já fica ativo para guiar até o loot.
        /// </summary>
        public void RegistrarMorte(Vector3 pos, bool lootNoChao, string quando)
        {
            Atualizar();
            if (chave == null) return;
            lista.RemoveAll(m => m.morte);
            foreach (var m in lista) m.ativo = false;
            lista.Add(new Marcador
            {
                nome = lootNoChao ? "Seu loot" : "Última morte",
                info = lootNoChao ? $"{quando} · sua mochila ficou aqui" : quando,
                x = pos.x, y = pos.y, z = pos.z, ativo = true, morte = true,
            });
            Salvar();
            Plugin.Log($"Morte registrada em {pos} ({(lootNoChao ? "loot no chão" : "save recarregado")})");
        }

        public void AlternarLista()
        {
            if (Janela.Aberta == janelaLista) Janela.Fechar();
            else if (Janela.Aberta == null) janelaLista.Abrir();
        }

        // ---------- Cálculos ----------
        public static float Distancia(Vector3 p)
        {
            var d = p - Jogo.PosicaoJogador();
            d.y = 0;
            return d.magnitude;
        }

        public static string TextoDistancia(float m) => m < 1000 ? $"{m:0} m" : $"{m / 1000:0.0} km".Replace('.', ',');

        /// <summary>Direção cardeal (0° = norte, sentido horário, como a bússola do relógio).</summary>
        public static string Cardeal(Vector3 p)
        {
            var d = p - Jogo.PosicaoJogador();
            float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360;
            string[] nomes = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
            return nomes[Mathf.RoundToInt(ang / 45f) % 8];
        }

        /// <summary>Ângulo do alvo em relação para onde a câmera aponta (0 = em frente, positivo = à direita).</summary>
        static float AnguloRelativo(Vector3 p)
        {
            var f = Jogo.Camera().forward; f.y = 0;
            var d = p - Jogo.PosicaoJogador(); d.y = 0;
            return Vector3.SignedAngle(f, d, Vector3.up);
        }

        // ---------- HUD ----------
        static Texture2D seta;
        static Texture2D Seta()
        {
            if (seta != null) return seta;
            const int n = 32;
            seta = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var cor = Cor(Destaque);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // triângulo apontando para cima (y alto = topo da textura)
                    float meia = (y / (float)(n - 1)) * (n / 2f);
                    bool dentro = Mathf.Abs(x - (n - 1) / 2f) <= (n / 2f - meia) && y > 2;
                    seta.SetPixel(x, y, dentro ? cor : Color.clear);
                }
            seta.Apply();
            return seta;
        }

        /// <summary>Losango nos marcadores normais; caveira no marcador da última morte (loot).</summary>
        static void Icone(Marcador m, Vector2 centro, float lado, Color cor)
        {
            if (!m.morte) { Losango(centro, lado, cor); return; }
            float t = lado * 1.7f;
            Simbolos.Desenhar(Simbolos.Caveira(), new Rect(centro.x - t / 2, centro.y - t / 2, t, t), cor);
        }

        static void Losango(Vector2 centro, float lado, Color cor)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, centro);
            GUI.DrawTexture(new Rect(centro.x - lado / 2, centro.y - lado / 2, lado, lado), Tex(cor));
            GUI.matrix = m;
        }

        public void DesenharHud()
        {
            if (chave == null) return;
            var ativo = lista.FirstOrDefault(m => m.ativo);

            foreach (var m in lista)
                if (m.ativo || Plugin.TodosMarcadoresNoMundo.Value) NoMundo(m);

            if (ativo == null) return;
            float dist = Distancia(ativo.Pos);
            string texto = $"<b>{Esc(ativo.nome)}</b>    {TextoDistancia(dist)}         {Cardeal(ativo.Pos)}    {C(Apagado, Jogo.Gps(ativo.Pos))}";
            var cont = new GUIContent(texto);
            var tam = Texto.CalcSize(cont);
            float icone = Px(14), pad = Px(10), h = tam.y + Px(12);
            float w = tam.x + icone + pad * 3;
            var r = new Rect((Screen.width - w) / 2, Screen.height * 0.022f, w, h);
            if (Plugin.CartoesPincel.Value) Tema.Pincelada(r, Tema.Lado.Centro); else Caixa(r, Painel);
            Icone(ativo, new Vector2(r.x + pad + icone / 2, r.center.y), icone * 0.7f, ativo.Cor);
            Tema.TextoComSombra(new Rect(r.x + pad * 2 + icone, r.y + Px(6), tam.x, tam.y), texto, Texto);

            // seta relativa, logo antes da direção cardeal
            var antes = Texto.CalcSize(new GUIContent($"<b>{Esc(ativo.nome)}</b>    {TextoDistancia(dist)}    "));
            var centroSeta = new Vector2(r.x + pad * 2 + icone + antes.x + Px(8), r.center.y);
            var mat = GUI.matrix;
            GUIUtility.RotateAroundPivot(AnguloRelativo(ativo.Pos), centroSeta);
            float s = Px(14);
            GUI.DrawTexture(new Rect(centroSeta.x - s / 2, centroSeta.y - s / 2, s, s), Seta());
            GUI.matrix = mat;
        }

        static GUIStyle estiloMundo, sombraMundo;

        static void NoMundo(Marcador m)
        {
            var p = Jogo.PontoTela(m.Pos + Vector3.up * 1.5f);
            if (p.z <= 0) return;
            var c = new Vector2(p.x, Screen.height - p.y);
            float lado = m.morte ? Px(16) : Px(11);
            Icone(m, c, lado, m.Cor);
            string simples = $"<b>{Esc(m.nome)}</b>\n{TextoDistancia(Distancia(m.Pos))}";
            if (estiloMundo == null || estiloMundo.fontSize != Pequeno.fontSize)
            {
                estiloMundo = new GUIStyle(Pequeno) { alignment = TextAnchor.UpperCenter, wordWrap = false };
                estiloMundo.normal.textColor = Cor(Tinta);
                sombraMundo = new GUIStyle(estiloMundo);
                sombraMundo.normal.textColor = new Color(0, 0, 0, .85f);
            }
            var tam = estiloMundo.CalcSize(new GUIContent(simples));
            var r = new Rect(c.x - tam.x / 2, c.y + lado, tam.x, tam.y);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), simples, sombraMundo);
            GUI.Label(r, simples, estiloMundo);
        }

        // ---------- Janelas ----------
        class JanelaNome : Janela
        {
            readonly Marcadores dono;
            Vector3 ponto;
            string nome;
            bool focar;
            public JanelaNome(Marcadores d) { dono = d; }

            public void Novo(Vector3 p, string sugestao) { ponto = p; nome = sugestao; focar = true; Abrir(); }

            public override void Desenhar()
            {
                var r = Centro(0.28f, 0f);
                r.height = Px(170);
                r.y = (Screen.height - r.height) / 2;
                Fundo(r);
                float pad = Px(16), x = r.x + pad, w = r.width - pad * 2, y = r.y + pad;
                GUI.Label(new Rect(x, y, w, Px(16)), "NOVO MARCADOR", Rotulo); y += Px(18);
                GUI.Label(new Rect(x, y, w, Px(20)), $"{Jogo.Gps(ponto)}  ·  {TextoDistancia(Distancia(ponto))} {Cardeal(ponto)}", Pequeno); y += Px(26);

                bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                if (EscPressionado()) { Fechar(); return; }
                GUI.SetNextControlName("gh_nome");
                nome = GUI.TextField(new Rect(x, y, w, Px(34)), nome ?? "", 40, Campo); y += Px(46);
                if (focar) { GUI.FocusControl("gh_nome"); focar = false; }

                float bw = (w - Px(8)) / 2;
                if (GUI.Button(new Rect(x, y, bw, Px(30)), "Cancelar", Botao)) { Fechar(); return; }
                if (GUI.Button(new Rect(x + bw + Px(8), y, bw, Px(30)), C(Destaque, "<b>Salvar</b>"), Botao) || enter)
                {
                    dono.Adicionar(ponto, string.IsNullOrWhiteSpace(nome) ? "Marcador" : nome.Trim());
                    Fechar();
                }
            }
        }

        class JanelaLista : Janela
        {
            readonly Marcadores dono;
            Vector2 rolagem;
            Marcador renomeando, apagando;
            string novoNome;
            public JanelaLista(Marcadores d) { dono = d; }

            protected override void AoFechar() { renomeando = apagando = null; }

            public override void Desenhar()
            {
                var r = Centro(0.42f, 0.6f);
                Fundo(r);
                if (EscPressionado()) { Fechar(); return; }
                float pad = Px(18), x = r.x + pad, w = r.width - pad * 2, y = r.y + pad;
                GUI.Label(new Rect(x, y, w, Px(30)), "<b>Marcadores</b>", TituloGrande);
                if (GUI.Button(new Rect(r.xMax - pad - Px(80), y, Px(80), Px(26)), "Fechar", Botao)) { Fechar(); return; }
                y += Px(36);
                string teclaMarcar = Plugin.TeclaMarcar.Value.MainKey.ToString();
                GUI.Label(new Rect(x, y, w, Px(18)), $"{teclaMarcar} marca o ponto para onde você está olhando. O marcador ativo aparece no topo da tela.", Pequeno);
                y += Px(26);

                var lista = dono.Lista.OrderByDescending(m => m.ativo).ThenBy(m => Distancia(m.Pos)).ToList();
                if (lista.Count == 0)
                {
                    GUI.Label(new Rect(x, y, w, Px(40)), $"Nenhum marcador nesta partida ainda. Feche esta janela, olhe para um lugar e aperte {teclaMarcar}.", Texto);
                    return;
                }

                float linha = Px(58);
                var area = new Rect(x, y, w, r.yMax - pad - y);
                var conteudo = new Rect(0, 0, w - Px(16), lista.Count * (linha + Px(6)));
                rolagem = GUI.BeginScrollView(area, rolagem, conteudo);
                float ly = 0;
                foreach (var m in lista)
                {
                    var lr = new Rect(0, ly, conteudo.width, linha);
                    CaixaArredondada(lr, m.ativo ? new Color32(232, 169, 60, 30) : new Color32(255, 255, 255, 8), RaioLinha);
                    Icone(m, new Vector2(lr.x + Px(16), lr.y + Px(18)), Px(9), m.ativo || m.morte ? m.Cor : Cor(Apagado));
                    float tx = lr.x + Px(30), bw = Px(84), bx = lr.xMax - Px(8) - bw * 3 - Px(8);

                    if (renomeando == m)
                    {
                        novoNome = GUI.TextField(new Rect(tx, lr.y + Px(6), bx - tx - Px(8), Px(26)), novoNome ?? "", 40, Campo);
                        if (GUI.Button(new Rect(bx + (bw + Px(4)) * 2, lr.y + Px(6), bw, Px(26)), "OK", Botao))
                        {
                            if (!string.IsNullOrWhiteSpace(novoNome)) m.nome = novoNome.Trim();
                            renomeando = null;
                            dono.Salvar();
                        }
                    }
                    else
                    {
                        GUI.Label(new Rect(tx, lr.y + Px(6), bx - tx, Px(22)), $"<b>{Esc(m.nome)}</b>{(m.ativo ? "   " + C(Destaque, "ativo") : "")}{(m.info != null ? "   " + C(Apagado, Esc(m.info)) : "")}", Texto);
                        if (GUI.Button(new Rect(bx, lr.y + Px(6), bw, Px(24)), m.ativo ? "Ocultar" : "Mostrar", Botao)) dono.Ativar(m);
                        if (GUI.Button(new Rect(bx + bw + Px(4), lr.y + Px(6), bw, Px(24)), "Renomear", Botao)) { renomeando = m; novoNome = m.nome; apagando = null; }
                        string rot = apagando == m ? C(Perigo, "Confirmar") : "Apagar";
                        if (GUI.Button(new Rect(bx + (bw + Px(4)) * 2, lr.y + Px(6), bw, Px(24)), rot, Botao))
                        {
                            if (apagando == m) { dono.Apagar(m); apagando = null; GUI.EndScrollView(); return; }
                            apagando = m;
                        }
                    }
                    GUI.Label(new Rect(tx, lr.y + Px(32), lr.width - tx, Px(20)),
                        $"{TextoDistancia(Distancia(m.Pos))} {Cardeal(m.Pos)}    {C(Apagado, Jogo.Gps(m.Pos))}", Mono);
                    ly += linha + Px(6);
                }
                GUI.EndScrollView();
            }
        }
    }
}
