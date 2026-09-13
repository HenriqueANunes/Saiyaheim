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

## Estrutura

- `src/Saiyaheim/` — código do mod (C#)
- `tools/` — scripts Python para geração de malhas (cabelo)
- `scripts/` — build, deploy e sincronização de config
- `packaging/` — empacotamento para distribuição
