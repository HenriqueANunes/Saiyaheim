#!/usr/bin/env bash
# Monta o zip de distribuição no formato Thunderstore, que o r2modman importa direto
# por "Import local mod". Não publica nada — o zip é entregue à mão para os amigos.
#
# Uso:  ./scripts/package.sh [Release|Debug]
#
# A versão sai de Plugin.cs (PluginVersion), para o manifest e a DLL nunca divergirem —
# divergir aqui é kick no multiplayer, já que o mod é EveryoneMustHaveMod.

set -euo pipefail

CONFIG="${1:-Release}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DLL="$REPO_ROOT/src/Saiyaheim/bin/$CONFIG/Saiyaheim.dll"
DIST="$REPO_ROOT/dist"

# Versões das dependências no Thunderstore. Precisam bater com o que está instalado no
# perfil, senão o r2modman baixa outra e a checagem de rede reclama.
DEP_BEPINEX="denikson-BepInExPack_Valheim-5.4.2333"
DEP_JOTUNN="ValheimModding-Jotunn-2.29.2"

VERSION="$(grep -oP 'PluginVersion\s*=\s*"\K[^"]+' "$REPO_ROOT/src/Saiyaheim/Plugin.cs")"
if [[ -z "$VERSION" ]]; then
  echo "erro: não consegui ler PluginVersion de src/Saiyaheim/Plugin.cs" >&2
  exit 1
fi

dotnet build "$REPO_ROOT/Saiyaheim.sln" -c "$CONFIG" --nologo

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# Estrutura explícita: o r2modman mapeia BepInEx/ do zip para o BepInEx do perfil.
# Só a DLL vai junto — o resto do bin/ são facades de compilação, e um Jotunn.dll
# duplicado em plugins/ quebra o carregamento.
mkdir -p "$STAGE/BepInEx/plugins/Saiyaheim"
cp "$DLL" "$STAGE/BepInEx/plugins/Saiyaheim/"

cp "$REPO_ROOT/packaging/README.md" "$STAGE/"
cp "$REPO_ROOT/packaging/icon.png" "$STAGE/"

cat > "$STAGE/manifest.json" <<MANIFEST
{
  "name": "Saiyaheim",
  "version_number": "$VERSION",
  "website_url": "",
  "description": "Mod de Dragon Ball para Valheim: ki, voo, combate desarmado e transformacoes.",
  "dependencies": [
    "$DEP_BEPINEX",
    "$DEP_JOTUNN"
  ]
}
MANIFEST

mkdir -p "$DIST"
ZIP="$DIST/Saiyaheim-$VERSION.zip"
rm -f "$ZIP"
(cd "$STAGE" && zip -qr "$ZIP" .)

echo "$ZIP"
unzip -l "$ZIP"
