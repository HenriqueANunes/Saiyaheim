# Ferramentas de calibragem

Páginas de uso local para resolver números de balanceamento **antes** de escrever C#. Não fazem
parte do mod, não vão para o `BepInEx/plugins/` e não entram no build — o `deploy.sh` ignora esta
pasta.

## `curva-poder.html`

Calculadora interativa do [[Battle Power]]: sliders para todos os coeficientes reais, curva
proposta contra a de hoje, tabela nível a nível e um snippet de `.cfg` pronto para colar.

Feita em 2026-08-01 para decidir o termo de fim de jogo (`K5_LateGameBonus`), e foi ela que
mostrou que a primeira proposta — um expoente sobre o `k4` — não fazia o que se queria.

### Como abrir

```bash
xdg-open tools/curva-poder.html
```

Funciona offline, sem servidor e sem dependência nenhuma: tudo é inline, inclusive o gráfico
(SVG desenhado à mão em JS). Não há build.

Também está publicada como Artifact, para abrir do celular ou mandar para alguém:
**https://claude.ai/code/artifact/b248144a-f920-4162-bed4-34308a0014a6**

> ⚠️ Para o Claude **atualizar** essa página numa conversa futura sem criar um link novo, ele
> precisa receber essa URL e passá-la no parâmetro `url` do publish. Sem isso, sai uma URL
> diferente e a antiga fica órfã.

### O que ela modela

O JS espelha, de propósito, as fórmulas reais do C#. Se `BattlePower.cs` ou `FlightStats.cs`
mudarem, **esta página fica mentindo** — os pontos a manter em sincronia:

| Na página | No mod |
|---|---|
| `linearPower()` | `BattlePower.GetRaw()` |
| `combatPower()` | `BattlePower.GetCombatRaw()` |
| `lateBonus()` | `BattlePower.GetLateGameBonus()` |
| `formMult()` | `TransformationRegistry.GetPowerMultiplier()` |
| `flyMult` (dentro de `model()`) | `FlightStats.GetFormSpeedFactor()` |
| `weightf` (dentro de `model()`) | `FlightStats.GetWeightSpeedFactor()` |
| `formDrain()` | `Transformation.GetKiDrainPerSecond()` |
| `applyArmor()` | `HitData.ApplyArmor` do Valheim, copiada da decompilação |
| `xpCost[]` | `Skills.Skill.GetNextLevelRequirement()` |

A curva de XP e o `ApplyArmor` são do **jogo**, não do mod: só mudam se o Valheim atualizar.

### As Transformações (etapa 5) — feito em 2026-08-02

O grupo **Transformação** tem `PowerMultiplier`, `KiDrainPerSecond`, a redução de dreno da maestria
e o nível de maestria (com "acompanha o nível da linha", igual ao voo). O multiplicador entra em
`model()` **depois** do `combatPower()`, que é onde o `BattlePower.GetKiCombatRaw` do mod o aplica.

Duas coisas de desenho que valem para a próxima extensão:

- **A terceira série só aparece quando diz alguma coisa.** O `FORM_BLIND` lista os stats que o
  multiplicador não toca (teto de ki, custo e autonomia de voo, dreno, segundos em forma); neles a
  linha transformada seria idêntica à atual, e duas linhas sobrepostas leem como bug. A legenda
  some junto.
- **Esconder item de legenda é `style.display`, não o atributo `hidden`.** O `.item` declara
  display próprio, e regra de autor ganha do `[hidden] { display: none }` do navegador.

Stat novo: **segundos em forma** (`teto de ki ÷ dreno`) — é o número que decide se a transformação
é ferramenta ou modo de jogo, e nenhum dos dois multiplicadores da página o toca.

Cartão novo no topo: **transformado no 100**, que mostra o dano recebido dentro da forma. Existe
porque a armadura é o consumidor que menos aguenta multiplicador — foi ela que obrigou a baixar o
`ArmorFromPower` quando o termo de fim de jogo entrou, e a forma multiplica a armadura junto com o
resto.

### O `FormSpeedShare` (voo) — feito em 2026-08-21

A forma multiplica a velocidade de voo **em parte**: `1 + (PowerMultiplier − 1) × FormSpeedShare`.
O slider está no grupo **Voo** e a lente Voo é onde ele se lê — com ele em 1 as linhas SSJ e SSJ2
sobem até colar no `MaxSpeed`, que é exatamente o defeito que a mudança consertou.

Duas coisas a lembrar ao mexer nessa parte:

- **A página plota o modo lento.** Não há slider de `FastSpeedMultiplier`, e no jogo ele dobra tudo
  — então o `MaxSpeed` morde antes do que o gráfico mostra. O alerta da lente diz isso em voz alta.
- **A ordem dos vereditos importa.** O alerta de "voo de graça" dispara com os defaults atuais, então
  qualquer alerta novo colocado depois dele é código morto. O do teto vem antes de propósito: forma
  colada no teto é defeito estrutural (SSJ e SSJ2 param de se distinguir), voo barato é calibragem
  conhecida.

### A curva do peso e o eixo **por carga** (voo) — feito em 2026-09-06

O peso desconta velocidade por `1 − WeightPenalty × carga^WeightCurve`. Entraram no grupo **Voo**
os dois sliders da fórmula, mais um de **carga do inventário** — que começa em 0, de propósito: até
então a página inteira assumia jogador de mãos vazias, e mudar isso sozinho mexeria em número que
ninguém pediu para mexer.

**O eixo `por carga` é a primeira vez que a página varre algo que não é nível.** Ele só aparece na
lente Voo (`LENSES.fly.loadAxis`), e o truque que o tornou barato é que o domínio continua sendo
`0..100`: no eixo de nível o índice é o nível, no de carga é a porcentagem. `seriesFor()` devolve
duas linhas (nível 100 e nível 0), e nada em `draw()` precisou saber a diferença além do rótulo dos
ticks.

Três coisas a lembrar ao mexer nessa parte:

- **O stat trava em velocidade quando o eixo é carga.** Peso não toca custo, teto nem autonomia — os
  outros stats desenhariam retas horizontais, que leem como bug. Os botões ficam `disabled`, em vez
  de sumirem, para o motivo continuar visível.
- **Sair da lente devolve o eixo.** `setLens()` reseta `axis` quando a lente nova não tem
  `loadAxis`; sem isso as outras lentes desenhariam um domínio que os stats delas não leem.
- **A forma é expoente, não hiperbólica**, e a página diz isso em voz alta porque a confusão é fácil:
  o `1/(1 + r × x)` do `KiPowerReduction` é a curva **espelhada** desta. Entrada sem teto pede
  hiperbólica; entrada em 0–1 pede expoente.

### Se precisar de uma quarta série

⚠️ **Cor nova passa pelo validador antes de entrar.** O script está em `tools/validate_palette.py`,
portado do `skills/dataviz/scripts/validate_palette.js` porque esta máquina não tem Node:

```bash
# O .tool-versions do repo só declara dotnet, então o `python3` do asdf não resolve aqui.
# Chamar o interpretador direto evita ter que mexer no ambiente de build por causa de um script.
PY=~/.asdf/installs/python/3.12.13/bin/python3

$PY tools/validate_palette.py "#0481b3,#ae6700,#8a5cd0,#NOVA" light "#E4E1D9"
$PY tools/validate_palette.py "#06a0dd,#d37e01,#9b6ef3,#NOVA" dark  "#16130F"
```

Ele checa faixa de lightness, chroma, separação para daltonismo e contraste contra o fundo. As três
cores atuais (`--ki`, `--today`, `--form`) passam nos dois temas; não vale escolher no olho.

### Testar a página

⚠️ **Existe Node nesta máquina** (v24, o que vem embutido no Zed:
`~/.local/share/zed/node/node-v24.11.0-linux-x64/bin/node`), ao contrário do que esta seção dizia
até 2026-09-06. Não está no `PATH` por padrão, mas resolve as duas coisas que faltavam:

```bash
NODE=~/.local/share/zed/node/node-v24.11.0-linux-x64/bin/node

# 1. sintaxe, extraindo o <script> da página
perl -0777 -ne 'print $1 if /<script>(.*)<\/script>/s' tools/curva-poder.html > /tmp/curva.js
$NODE --check /tmp/curva.js
```

Não há `jsdom` instalado, mas um DOM falso de ~40 linhas (um `getElementById` que devolve objetos
com `value`/`textContent`/`addEventListener`, `querySelectorAll` devolvendo `[]` e um
`createElementNS` que devolve nós de mentira) basta para **rodar a página inteira** e ler os
cartões, o veredito e o snippet como texto — inclusive disparando os handlers guardados no
`addEventListener` para simular cliques em lente, eixo e sliders. Foi assim que a curva do peso foi
conferida em 2026-09-06, sem abrir o navegador.

Para ver com os próprios olhos, a extensão do Chrome recusa `file://`; o caminho é servir a pasta e
abrir por `http://`:

```bash
~/.asdf/installs/python/3.12.13/bin/python3 -m http.server 8917 --bind 127.0.0.1
```

Só para inspeção — o `http.server` manda `text/html` sem charset e o Chrome cai em windows-1252,
então os acentos aparecem quebrados. Abrindo direto do disco (`xdg-open`) ou como Artifact, não.

### Convenções da página

- **Tema claro e escuro** via tokens em `:root`, com `@media (prefers-color-scheme)` e override
  por `:root[data-theme]`. Não estilizar componente dentro do media query — só redefinir token.
- **Nada de CDN.** O Artifact bloqueia host externo; tudo inline, sempre.
- Números com `toLocaleString("pt-BR")` e `font-variant-numeric: tabular-nums` nas colunas.
