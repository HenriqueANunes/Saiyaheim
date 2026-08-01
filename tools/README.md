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
| `applyArmor()` | `HitData.ApplyArmor` do Valheim, copiada da decompilação |
| `xpCost[]` | `Skills.Skill.GetNextLevelRequirement()` |

A curva de XP e o `ApplyArmor` são do **jogo**, não do mod: só mudam se o Valheim atualizar.

### Estendendo para as Transformações (etapa 5)

O desenho já prevê isso. As transformações **multiplicam por cima** do power level, então o
caminho é:

1. Um slider novo para o multiplicador da forma e, se necessário, um para o nível de maestria.
2. Multiplicar em `model()`, **depois** do `combatPower()` e antes dos consumidores — é a ordem
   que o `SE_KiBody` usa, e a página tem de errar do mesmo jeito que o mod erra.
3. Uma terceira série no gráfico (transformado), reusando o mesmo padrão das duas atuais.

⚠️ **A paleta tem só duas cores validadas.** Uma terceira série precisa passar pelo validador
antes de entrar — o script está em `tools/validate_palette.py`, portado do
`skills/dataviz/scripts/validate_palette.js` porque esta máquina não tem Node:

```bash
# O .tool-versions do repo só declara dotnet, então o `python3` do asdf não resolve aqui.
# Chamar o interpretador direto evita ter que mexer no ambiente de build por causa de um script.
PY=~/.asdf/installs/python/3.12.13/bin/python3

$PY tools/validate_palette.py "#0481b3,#ae6700,#NOVA" light "#E4E1D9"
$PY tools/validate_palette.py "#06a0dd,#d37e01,#NOVA" dark  "#16130F"
```

Ele checa faixa de lightness, chroma, separação para daltonismo e contraste contra o fundo. As
duas cores atuais passam nos dois temas; não vale escolher a terceira no olho.

### Convenções da página

- **Tema claro e escuro** via tokens em `:root`, com `@media (prefers-color-scheme)` e override
  por `:root[data-theme]`. Não estilizar componente dentro do media query — só redefinir token.
- **Nada de CDN.** O Artifact bloqueia host externo; tudo inline, sempre.
- Números com `toLocaleString("pt-BR")` e `font-variant-numeric: tabular-nums` nas colunas.
