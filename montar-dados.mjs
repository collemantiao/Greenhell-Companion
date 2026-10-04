// Junta os arquivos dados/*.json em dados.js (carregado pelo index.html).
// Uso: node montar-dados.mjs
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";

// Ids de "relacionados" que apontam para o nome usado em outro arquivo.
const SINONIMOS = {
  "ferimento-laceracao": "laceracao",
  "envenenamento": "ferimento-por-veneno",
  "mordida-de-cobra": "ferimento-por-veneno",
  "infeccao": "ferimento-infectado",
  "curativo-de-golias": "curativo-golias",
  "inspecao-do-corpo": "inspecao-corporal",
  "armadilha-de-gaiola": "armadilha-gaiola",
  "armadilha-de-vara-de-pesca": "armadilha-de-vara-de-pescar",
  "vara-de-pesca": "vara-de-pescar",
  "sapo-venenoso": "sapo-dardo",
  "caranguejeira-golias": "aranha-golias",
  "queixada": "cateto",
  "nativos": "indigenas-waraha",
  "acampamentos-waraha": "indigenas-waraha",
  "arara": "arara-vermelha",
  "agua-potavel": "agua",
  "armadura-de-tatu": "armadura",
  "armadura-de-metal": "armadura",
  "colmeia": "favo-de-mel",
  "folha-de-lirio": "curativo-de-lirio",
};

const pasta = new URL("./dados/", import.meta.url);
const porId = new Map();

// Quando dois arquivos têm a mesma ficha (ex.: "carvao" como material e como remédio),
// as seções são somadas na mesma ficha.
function juntar(a, b) {
  const titulos = new Set(a.secoes.map(s => s.titulo.toLowerCase()));
  for (const s of b.secoes || []) if (!titulos.has(s.titulo.toLowerCase())) a.secoes.push(s);
  a.aliases = [...new Set([...(a.aliases || []), ...(b.aliases || [])])];
  a.relacionados = [...new Set([...(a.relacionados || []), ...(b.relacionados || [])])];
  a.perigo = Math.max(a.perigo || 0, b.perigo || 0);
}

for (const arq of readdirSync(pasta).filter(f => f.endsWith(".json")).sort()) {
  for (const e of JSON.parse(readFileSync(new URL(arq, pasta), "utf8"))) {
    e.secoes ||= [];
    if (porId.has(e.id)) juntar(porId.get(e.id), e);
    else porId.set(e.id, e);
  }
}

const descartados = new Set();
for (const e of porId.values()) {
  const rel = [];
  for (const r of e.relacionados || []) {
    const id = SINONIMOS[r] || r;
    if (!porId.has(id)) descartados.add(r);
    else if (id !== e.id && !rel.includes(id)) rel.push(id);
  }
  e.relacionados = rel;
}

const entradas = [...porId.values()];
writeFileSync(
  new URL("./dados.js", import.meta.url),
  "// Gerado por montar-dados.mjs — edite os arquivos em dados/ e rode o script de novo.\nwindow.GH_DADOS = " +
    JSON.stringify(entradas) + ";\n",
);
// O mod lê o mesmo conteúdo (JsonUtility do Unity exige um objeto na raiz).
mkdirSync(new URL("./mod/Data/", import.meta.url), { recursive: true });
writeFileSync(new URL("./mod/Data/guia.json", import.meta.url), JSON.stringify({ fichas: entradas }));
console.log(`${entradas.length} fichas gravadas em dados.js e mod/Data/guia.json`);
if (descartados.size) console.log(`Relacionados sem ficha (removidos): ${[...descartados].sort().join(", ")}`);
