# Green Hell Companion

Mod para Green Hell (BepInEx 5) que ajuda sem trapacear: só mostra informação, não muda status, não cria itens e não teleporta.

| Tecla | O que faz |
|---|---|
| F1 | Abre o guia (as mesmas fichas do site). Se houver um aviso de saúde na tela, abre direto na ficha dele. |
| F2 | Marca o ponto para onde você está olhando e pede um nome. |
| F3 | Lista os marcadores da partida: mostrar no topo, renomear, apagar. Ao morrer, o marcador vermelho "Seu loot" é criado sozinho onde você caiu (no multiplayer a mochila fica lá). |
| F4 | Configurações: tamanho do texto, opacidade dos cartões e das ilustrações, liga/desliga avisos e ajuda de construção. Também abre pelo botão no guia. |
| F5 | Menu de construção e criação: receitas por grupo no formato "Galho 13 (8)" (necessário e, entre parênteses, quanto você tem; verde dá, vermelho falta), busca, filtros e botão Posicionar. |
| Esc | Fecha a janela do mod. |

Sem tecla:
- **Macroelementos:** uma barra por linha no estilo da HUD, acima da vida e da energia (proteína, gordura, carboidrato e água). A barra pisca em vermelho quando o nível está crítico.
- **Avisos de saúde:** ferimentos, infecções, doenças e status baixos aparecem no canto inferior esquerdo, acima dos macroelementos, com os tratamentos que o jogo aceita e quantos você tem na mochila.
- **Construção:** no canto superior direito, ao posicionar uma construção, ou perto de uma já colocada, mostra cada material, quanto você tem (mochila, mãos e baús a até 20 m) e o que falta.

## Configuração

Depois de abrir o jogo uma vez, edite `Green Hell\BepInEx\config\br.felipe.greenhellcompanion.cfg` para trocar teclas, mudar a escala dos textos (`Escala`) ou desligar partes. Os marcadores ficam em `Green Hell\BepInEx\config\GreenHellCompanion\`, um arquivo por partida.

## Compilar

Precisa do .NET SDK 8 e do BepInEx 5 instalado no jogo.

```
cd mod
dotnet build -c Release
```

O build compila contra as DLLs da sua instalação do jogo e copia o plugin para `BepInEx\plugins\GreenHellCompanion`. Se o jogo estiver em outra pasta, use `-p:GamePath="D:\caminho\Green Hell"`. Para só compilar sem instalar, use `-p:InstalarNoJogo=false`.

O conteúdo do guia vem de `dados/*.json` na raiz do repositório. Depois de editar, rode `node montar-dados.mjs` e compile de novo.

## Remover

Apague a pasta `Green Hell\BepInEx\plugins\GreenHellCompanion`. Para desligar todos os mods, apague `winhttp.dll` da pasta do jogo.
