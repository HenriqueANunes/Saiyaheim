# Saiyaheim

Mod de Dragon Ball para Valheim. Ki, voo, combate desarmado, transformação e ataques de ki.

Feito para jogar entre amigos — **não é um mod público**, não espere polimento nem suporte.

## Instalação (r2modman / Thunderstore Mod Manager)

1. Abra o r2modman no perfil que vocês usam para jogar juntos.
2. **Settings → Import local mod** (ou arraste o `.zip` para a janela).
3. Escolha o `Saiyaheim-x.y.z.zip` e confirme.
4. As dependências (**BepInExPack Valheim** e **Jotunn**) precisam estar instaladas no mesmo
   perfil. Se ainda não estiverem, instale pelo **Online** do r2modman antes de jogar.
5. Inicie o jogo **pelo r2modman** (`Start modded`). Iniciar pela Steam sem as launch options
   carrega o Valheim limpo, sem mod nenhum.

## Instalação manual (sem gerenciador)

Copie a pasta `BepInEx/plugins/Saiyaheim/` de dentro do zip para a instalação do BepInEx,
mantendo a estrutura. Você precisa ter BepInEx e Jotunn instalados.

## ⚠️ Todo mundo precisa do mod, na mesma versão

Inclusive quem hospeda a partida. Versões diferentes causam comportamento estranho ou kick —
o mod é marcado como `EveryoneMustHaveMod` e a checagem é por versão *minor*.

Se vocês usam **servidor dedicado**, o mod tem que estar instalado nele também.

## Configuração

Na primeira vez que o jogo abrir com o mod, é gerado
`BepInEx/config/com.hman.saiyaheim.cfg` com todos os valores de balanceamento, teclas e
posição da HUD.

Os valores de **gameplay** são `AdminOnly`: quem hospeda impõe os dele para todo mundo.
Mexer neles no seu arquivo não muda nada em partida com amigos — só em singleplayer.
As **teclas** e os **offsets da HUD** são seus e valem localmente.

## Estado do mod

Multiplayer ainda está em desenvolvimento. O que atravessa a rede hoje é o estado visível
dos outros jogadores (ki ligado, voo, carregamento, forma ativa) e os ataques de ki.
Coisas que ainda podem estar erradas na tela dos outros são esperadas — reporte que ajuda.
