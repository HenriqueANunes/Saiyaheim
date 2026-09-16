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

## Servidor dedicado

O servidor com o mod roda em container na máquina `hserver`. Para acompanhar o log em tempo
real:

```bash
ssh hserver 'docker logs -f --tail 50 valheim'   # log do servidor
ssh hserver 'valheim/joincode.sh'                # join code atual (muda a cada reinício)
ssh hserver 'valheim/players.sh'                 # quem está online agora
```

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

O `players.sh` cruza a última linha `Connections N` do log com os nomes mais recentes de
`Got character ZDOID` — o servidor dedicado não tem console, e o log não liga nome a socket.
A contagem vem da última leitura periódica, então pode estar até ~2 minutos atrasada.
`players.sh -f` acompanha entradas e saídas em tempo real.

### Atualizar o mod no servidor

O servidor precisa da mesma versão do `Saiyaheim.dll` que os clientes — com versão diferente o
Jotunn recusa a conexão. Depois de subir uma versão nova:

```bash
./scripts/deploy.sh                      # compila e copia a DLL para o perfil local

P=~/.config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx
rsync -a "$P/plugins/Saiyaheim/" hserver:/storage/valheim/config/bepinex/plugins/Saiyaheim/
ssh hserver 'cd ~/valheim && docker compose restart'
```

O restart derruba quem estiver jogando e gera um join code novo — confira antes com
`players.sh` e passe o código novo para o grupo com `joincode.sh`.

Conferir se o servidor subiu com a versão certa:

```bash
ssh hserver 'docker logs --since 5m valheim' | grep -oE 'Loading \[Saiyaheim [^]]+\]'
```

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
