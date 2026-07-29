#!/usr/bin/env bash
# Compila e copia a DLL para o perfil do r2modman.
#
# Uso:  ./scripts/deploy.sh [Release|Debug] [nome-do-perfil]
#
# Com r2modman o BepInEx NÃO fica na pasta do jogo — fica no perfil do gerenciador.
# É de lá que o jogo carrega os plugins quando lançado pelo r2modman.

set -euo pipefail

CONFIG="${1:-Release}"
PROFILE="${2:-Default}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE_DIR="$HOME/.config/r2modmanPlus-local/Valheim/profiles/$PROFILE"
PLUGIN_DIR="$PROFILE_DIR/BepInEx/plugins/Saiyaheim"
DLL="$REPO_ROOT/src/Saiyaheim/bin/$CONFIG/Saiyaheim.dll"

if [[ ! -d "$PROFILE_DIR/BepInEx" ]]; then
  echo "erro: BepInEx não encontrado em $PROFILE_DIR" >&2
  echo "      Instale o BepInExPack Valheim e o Jotunn no perfil '$PROFILE' pelo r2modman." >&2
  exit 1
fi

dotnet build "$REPO_ROOT/Saiyaheim.sln" -c "$CONFIG" --nologo

mkdir -p "$PLUGIN_DIR"
cp "$DLL" "$PLUGIN_DIR/"
[[ -f "${DLL%.dll}.pdb" ]] && cp "${DLL%.dll}.pdb" "$PLUGIN_DIR/"

echo "Saiyaheim.dll → $PLUGIN_DIR"
