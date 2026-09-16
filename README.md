# Saiyaheim

Mod de Dragon Ball para Valheim.

Adiciona transformações, ki, voo e ataques inspirados em Dragon Ball ao Valheim, construído
sobre BepInEx e Jotunn.

## Ambiente

- Jogo: Valheim, build nativo Linux, versão `l-1.0.7` (network version 39)
- Gerenciador de mods: [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/)
- SDK: .NET 8 (via [asdf](https://asdf-vm.com/)), compilando para `net462`
- Dependências de runtime: **BepInExPack Valheim** e **Jotunn** (instaladas pelo r2modman)

Copie `Environment.props.example` para `Environment.props` e ajuste os caminhos para a sua
máquina antes do primeiro build.

## Build e deploy

```bash
dotnet build Saiyaheim.sln -c Release    # compila
./scripts/deploy.sh                      # compila e copia a DLL pro perfil do r2modman
```

Sempre pelo `.sln`, não pelo `.csproj`.

## Publicar uma versão

O `scripts/release.sh` publica no GitHub e no Thunderstore com o mesmo zip, mas **não cria commit
nem tag** — isso é à mão, nesta ordem:

1. Subir a versão em `src/Saiyaheim/Plugin.cs` (`PluginVersion`) e em
   `src/Saiyaheim/Saiyaheim.csproj` (`<Version>`), e escrever a seção `## X.Y.Z` no
   `packaging/CHANGELOG.md`. O script recusa se a seção não existir.
2. Testar no jogo e commitar.
3. Criar a tag no commit atual e enviar branch e tag juntos:

   ```bash
   git tag -a v0.1.3 -m "Saiyaheim 0.1.3"   # tag anotada, no HEAD
   git push origin main v0.1.3
   ```

4. Publicar:

   ```bash
   ./scripts/release.sh                      # GitHub + Thunderstore
   ./scripts/release.sh --only-thunderstore  # repete só o Thunderstore, se o upload falhou
   ```

No fim o script pergunta se sobe a versão para o servidor dedicado. Respondendo `s`, ele roda o
`scripts/server-update.sh` com a DLL do mesmo zip (ver "Atualizar o mod no servidor").

A tag tem que ser `v` + exatamente o `PluginVersion`. O script confere que a árvore está limpa,
que a tag existe localmente e no GitHub, e que o `main` do GitHub já contém o commit.

Conferir e corrigir tags:

```bash
git tag -l 'v*'                  # tags existentes
git show v0.1.3 --stat           # para qual commit a tag aponta
git tag -d v0.1.3                # apaga a tag local (antes do push)
```

⚠️ Versão publicada no Thunderstore é queimada para sempre. Se a tag já foi enviada e a release
publicada, não mova a tag: suba para a próxima versão. Commit depois da tag só é aceito se não
tocar no que entra no zip (`src/`, `packaging/`, `Saiyaheim.sln`, `DoPrebuild.props`).

## Servidor dedicado

O servidor com o mod roda em container na máquina `hserver`. Para acompanhar o log em tempo
real:

```bash
ssh hserver 'valheim/status.sh'                  # desligado / ligando / ligado + versão do mod
ssh hserver 'docker logs -f --tail 50 valheim'   # log do servidor
ssh hserver 'valheim/joincode.sh'                # join code atual (pode mudar no reinício)
ssh hserver 'valheim/players.sh'                 # quem está online agora
```

O `status.sh` responde se o servidor está no ar e qual versão do Saiyaheim ele carregou:

```
Status:    LIGADO desde 10:37:13 (join code 902151)
Saiyaheim: v0.1.2
Players:   0 online
```

- **DESLIGADO**: container parado, ou processo `valheim-server` parado dentro dele.
- **LIGANDO**: container preparando (steamcmd, plugins, backup) ou processo iniciado sem join
  code ainda. A subida inteira leva cerca de 1 min e meio.
- **LIGADO**: o log tem `registered with join code` desde o último start do processo.

A versão vem da linha `Saiyaheim vX loaded.` do log, então é a DLL que carregou de fato, não a
que está no disco. Se a DLL em `/config` for diferente da carregada, ele avisa que falta
reiniciar.

A contagem de players é em tempo real, somando entradas e saídas no log:

- entrada: `Server: New peer connected`
- saída: `ZPlayFabSocket::Dispose. State: CONNECTED`
- a cada 10 minutos, a linha `Connections N` do servidor corrige a conta

Quem sai pelo menu some da contagem na hora. Quem fecha o jogo à força ou cai continua contando
por 90 segundos, que é o tempo que o servidor espera a reconexão antes de soltar a vaga. O
`now N player(s)` que o PlayFab escreve não serve: na saída ele ainda mostra o número de antes.

O `--tail` não é opcional. Sem ele, `docker logs -f` despeja todo o log acumulado desde a
criação do container antes de começar a seguir, e restart não zera esse arquivo.

Últimas linhas, sem seguir:

```bash
ssh hserver 'docker logs --tail 200 valheim'
```

Só o que aconteceu agora, para separar problema atual de histórico:

```bash
ssh hserver 'docker logs --since 2m valheim'
```

O `players.sh` mostra quantos estão online, em tempo real, e os nomes. A contagem é a mesma do
`status.sh` (explicada acima), que chama `players.sh --count`. Os nomes vêm de
`Got character ZDOID`, mas a linha de saída não traz nome: a lista é de quem entrou desde a
última vez que o servidor ficou vazio. Se ela tiver mais nomes que a contagem, o script avisa
que alguém da lista já saiu. `players.sh -f` acompanha entradas e saídas enquanto acontecem.

### Atualizar o mod no servidor

O servidor precisa da mesma versão do `Saiyaheim.dll` que os clientes — com versão diferente o
Jotunn recusa a conexão. O `release.sh` já oferece isso no fim; fora de uma release:

```bash
./scripts/deploy.sh                                   # compila e copia a DLL para o perfil local
./scripts/server-update.sh                            # sobe a DLL do perfil local
./scripts/server-update.sh dist/Saiyaheim-0.1.3.zip   # ou a DLL de um zip publicado
```

O script envia só o `Saiyaheim.dll`, reinicia o container e espera até 5 minutos o `status.sh`
mostrar `LIGADO` com a versão da DLL enviada; se não vier, ele falha e aponta o log.

O restart derruba quem estiver jogando e pode trocar o join code. Com alguém online o script
mostra quem e pede confirmação. No fim ele diz se o join code mudou, para avisar o
grupo.

O `.cfg` do mod **não** vai junto nesse `rsync`: a config é `AdminOnly`, então o que vale na
sessão é o `.cfg` do servidor. Valor de balanceamento novo precisa ser copiado para lá à parte.

⚠️ No container, os plugins de `/storage/valheim/config/bepinex/plugins/` são copiados para a
pasta ativa com `rsync` **sem `--delete`**. Sobrescrever a DLL funciona; apagar ou renomear um
plugin em `/config` não o desativa — é preciso apagar também dentro do container
(`docker exec valheim rm ...`) antes do restart.

Log do cliente local (r2modman), reescrito a cada inicialização do jogo:

```bash
tail -f ~/.config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx/LogOutput.log
```

## Estrutura

- `src/Saiyaheim/` — código do mod (C#)
- `tools/` — scripts Python para geração de malhas (cabelo)
- `scripts/` — build, deploy e sincronização de config
- `packaging/` — empacotamento para distribuição
