#!/usr/bin/env bash
# Sobe o Saiyaheim.dll para o servidor dedicado (hserver), reinicia o container e espera ele
# voltar com a versão certa carregada.
#
# Uso:  ./scripts/server-update.sh                 # DLL do perfil local do r2modman (pós deploy.sh)
#       ./scripts/server-update.sh <zip|dll>       # DLL de um zip do package.sh, ou uma DLL qualquer
#
# O release.sh chama este script com o zip publicado, para o servidor rodar exatamente o que os
# jogadores baixam do Thunderstore.
#
# Só a DLL vai. O .cfg do servidor fica como está: a config é AdminOnly, então o que vale na sessão
# é o .cfg de lá, e chave nova nasce sozinha com o default quando o plugin carrega.
#
# ⚠️ O restart derruba quem estiver jogando e troca o join code. Com alguém online o script pede
# confirmação antes.

set -euo pipefail

HOST="${VALHEIM_HOST:-hserver}"
REMOTE_PLUGIN_DIR=/storage/valheim/config/bepinex/plugins/Saiyaheim
LOCAL_DLL="$HOME/.config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx/plugins/Saiyaheim/Saiyaheim.dll"
# A subida inteira (steamcmd, plugins, backup, mundo) leva cerca de 1 min e meio.
WAIT_SECONDS=300

fail() {
  echo "erro: $*" >&2
  exit 1
}

SOURCE="${1:-$LOCAL_DLL}"
[[ -f "$SOURCE" ]] || fail "não achei $SOURCE"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

if [[ "$SOURCE" == *.zip ]]; then
  unzip -q -j "$SOURCE" "BepInEx/plugins/Saiyaheim/Saiyaheim.dll" -d "$TMP" \
    || fail "o zip $SOURCE não tem BepInEx/plugins/Saiyaheim/Saiyaheim.dll"
  DLL="$TMP/Saiyaheim.dll"
else
  DLL="$SOURCE"
fi

# A versão vem da própria DLL (a string do log de carregamento), não do Plugin.cs: é ela que o
# status.sh do servidor vai ler depois do restart.
VERSION="$(strings -el "$DLL" | grep -oP '^Saiyaheim v\K[^ ]+(?= loaded\.)' | head -1 || true)"
[[ -n "$VERSION" ]] || fail "não consegui ler a versão de $DLL"

echo "Servidor $HOST, antes:"
BEFORE="$(ssh "$HOST" 'valheim/status.sh')" || fail "não consegui falar com $HOST."
echo "$BEFORE"

PLAYERS="$(ssh "$HOST" 'valheim/players.sh --count' 2>/dev/null || echo 0)"
if [[ "$PLAYERS" =~ ^[0-9]+$ ]] && (( PLAYERS > 0 )); then
  echo
  ssh "$HOST" 'valheim/players.sh' || true
  read -r -p "Há $PLAYERS jogador(es) online e o restart derruba todos. Continuar? [s/N] " ANSWER
  [[ "$ANSWER" == [sS] ]] || { echo "Servidor não atualizado."; exit 0; }
fi

echo
echo "Enviando Saiyaheim v$VERSION para $HOST..."
rsync -a "$DLL" "$HOST:$REMOTE_PLUGIN_DIR/Saiyaheim.dll"

echo "Reiniciando o container..."
ssh "$HOST" 'cd ~/valheim && docker compose restart'

echo -n "Esperando o servidor subir com v$VERSION"
DEADLINE=$((SECONDS + WAIT_SECONDS))
STATUS=""
while (( SECONDS < DEADLINE )); do
  sleep 10
  echo -n "."
  STATUS="$(ssh "$HOST" 'valheim/status.sh' 2>/dev/null || true)"
  if grep -q '^Status: *LIGADO' <<<"$STATUS"; then
    break
  fi
done
echo
echo "$STATUS"

grep -q '^Status: *LIGADO' <<<"$STATUS" \
  || fail "o servidor não ficou LIGADO em $((WAIT_SECONDS / 60)) min. Log: ssh $HOST 'docker logs --since 5m valheim'"
grep -q "^Saiyaheim: *v$VERSION\$" <<<"$STATUS" \
  || fail "o servidor subiu, mas não com Saiyaheim v$VERSION. Log: ssh $HOST 'docker logs --since 5m valheim'"

# O código nem sempre muda no restart; só vale avisar o grupo quando mudou.
join_code() { grep -oP 'join code \K[0-9]+' <<<"$1" || true; }
OLD_CODE="$(join_code "$BEFORE")"
NEW_CODE="$(join_code "$STATUS")"

echo
if [[ -n "$OLD_CODE" && "$OLD_CODE" == "$NEW_CODE" ]]; then
  echo "Servidor atualizado para v$VERSION. Join code continua $NEW_CODE."
else
  echo "Servidor atualizado para v$VERSION. Join code NOVO: $NEW_CODE — passe para o grupo."
fi
