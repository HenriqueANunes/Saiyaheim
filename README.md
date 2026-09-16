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
ssh hserver 'docker logs -f valheim'        # log do servidor
ssh hserver 'valheim/joincode.sh'           # join code atual (muda a cada reinício)
ssh hserver 'valheim/players.sh'            # quem está online agora
```

Últimas linhas, sem seguir:

```bash
ssh hserver 'docker logs --tail 200 valheim'
```

O `players.sh` cruza a última linha `Connections N` do log com os nomes mais recentes de
`Got character ZDOID` — o servidor dedicado não tem console, e o log não liga nome a socket.
A contagem vem da última leitura periódica, então pode estar até ~2 minutos atrasada.
`players.sh -f` acompanha entradas e saídas em tempo real.

Log do cliente local (r2modman), reescrito a cada inicialização do jogo:

```bash
tail -f ~/.config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx/LogOutput.log
```

## Estrutura

- `src/Saiyaheim/` — código do mod (C#)
- `tools/` — scripts Python para geração de malhas (cabelo)
- `scripts/` — build, deploy e sincronização de config
- `packaging/` — empacotamento para distribuição
