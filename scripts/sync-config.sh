#!/usr/bin/env bash
# Sincroniza o .cfg gerado pelo BepInEx com a cópia versionada em config/.
#
#   ./scripts/sync-config.sh pull [perfil]   perfil do r2modman  ->  repo
#   ./scripts/sync-config.sh push [perfil]   repo  ->  perfil do r2modman
#   ./scripts/sync-config.sh diff [perfil]   mostra a diferença
#
# Por que existe: o .cfg é gerado a partir dos defaults do SaiyaheimConfig.cs, então não é
# fonte. Mas os valores CALIBRADOS por playtest são trabalho real, e sem isto eles vivem só
# dentro do perfil do r2modman — fora do git e fáceis de perder.
#
# Fluxo esperado:
#   - ajustou balanceamento no jogo  ->  pull, e commita
#   - máquina nova, ou perfil recriado  ->  push

set -euo pipefail

ACTION="${1:-pull}"
PROFILE="${2:-Default}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CFG_NAME="com.hman.saiyaheim.cfg"
GAME_CFG="$HOME/.config/r2modmanPlus-local/Valheim/profiles/$PROFILE/BepInEx/config/$CFG_NAME"
REPO_CFG="$REPO_ROOT/config/$CFG_NAME"

case "$ACTION" in
  pull)
    if [[ ! -f "$GAME_CFG" ]]; then
      echo "erro: $GAME_CFG não existe. Abra o jogo uma vez para o BepInEx gerar o arquivo." >&2
      exit 1
    fi
    mkdir -p "$REPO_ROOT/config"
    cp "$GAME_CFG" "$REPO_CFG"
    echo "perfil '$PROFILE' -> repo"
    ;;

  push)
    if [[ ! -f "$REPO_CFG" ]]; then
      echo "erro: $REPO_CFG não existe. Rode 'pull' primeiro." >&2
      exit 1
    fi
    if [[ ! -d "$(dirname "$GAME_CFG")" ]]; then
      echo "erro: perfil '$PROFILE' não encontrado." >&2
      exit 1
    fi
    cp "$REPO_CFG" "$GAME_CFG"
    echo "repo -> perfil '$PROFILE'"
    ;;

  diff)
    diff -u "$REPO_CFG" "$GAME_CFG" && echo "idênticos"
    ;;

  *)
    echo "uso: $0 [pull|push|diff] [perfil]" >&2
    exit 1
    ;;
esac
