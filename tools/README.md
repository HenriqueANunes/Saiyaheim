# Ferramentas de calibragem

Páginas de uso local para resolver números de balanceamento **antes** de escrever C#. Não fazem
parte do mod, não vão para o `BepInEx/plugins/` e não entram no build — o `deploy.sh` ignora esta
pasta.

## `curva-poder.html`

Calculadora interativa do [[Power Level]]: sliders para todos os coeficientes reais, curva
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

O JS espelha, de propósito, as fórmulas reais do C#. Se `PowerLevel.cs` ou `FlightStats.cs`
mudarem, **esta página fica mentindo** — os pontos a manter em sincronia:

| Na página | No mod |
|---|---|
| `linearPower()` | `PowerLevel.GetRaw()` |
| `combatPower()` | `PowerLevel.GetCombatRaw()` |
| `lateBonus()` | `PowerLevel.GetLateGameBonus()` |
| `formMult()` | `TransformationRegistry.GetPowerMultiplier()` |
| `formDrain()` | `Transformation.GetKiDrainPerSecond()` |
| `applyArmor()` | `HitData.ApplyArmor` do Valheim, copiada da decompilação |
| `xpCost[]` | `Skills.Skill.GetNextLevelRequirement()` |

A curva de XP e o `ApplyArmor` são do **jogo**, não do mod: só mudam se o Valheim atualizar.

### As Transformações (etapa 5) — feito em 2026-08-02

O grupo **Transformação** tem `PowerMultiplier`, `KiDrainPerSecond`, a redução de dreno da maestria
e o nível de maestria (com "acompanha o nível da linha", igual ao voo). O multiplicador entra em
`model()` **depois** do `combatPower()`, que é onde o `PowerLevel.GetKiCombatRaw` do mod o aplica.

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

### Testar a página sem Node

Não há runtime de JS nesta máquina, e a extensão do Chrome recusa `file://`. O caminho é servir a
pasta e abrir por `http://`:

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
