#!/usr/bin/env bash
# Monta o zip de distribuição no formato Thunderstore, que o r2modman importa direto
# por "Import local mod". Não publica nada — quem publica este zip é o scripts/release.sh.
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
DEP_BEPINEX="denikson-BepInExPack_Valheim-5.4.2350"
DEP_JOTUNN="ValheimModding-Jotunn-2.30.0"

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

# O CHANGELOG é opcional: presente, vira uma aba na página do Thunderstore.
if [[ -f "$REPO_ROOT/packaging/CHANGELOG.md" ]]; then
  cp "$REPO_ROOT/packaging/CHANGELOG.md" "$STAGE/"
fi

cat > "$STAGE/manifest.json" <<MANIFEST
{
  "name": "Saiyaheim",
  "version_number": "$VERSION",
  "website_url": "https://github.com/HenriqueANunes/Saiyaheim",
  "description": "Dragon Ball Z (DBZ) mod for Valheim: ki, flight, unarmed combat, Kamehameha and ki blasts, and Super Saiyan transformations gated behind the bosses.",
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

# Aviso, não erro: a tag é o que liga uma versão publicada no Thunderstore ao commit que a
# gerou. Versão publicada não pode ser republicada, então um bug report que chegue citando
# "0.1.0" precisa de um ponto exato no histórico para ser investigado.
if ! git -C "$REPO_ROOT" rev-parse -q --verify "refs/tags/v$VERSION" >/dev/null; then
  echo
  echo "aviso: não existe a tag v$VERSION. Antes de publicar no Thunderstore:"
  echo "  git tag -a v$VERSION -m \"Saiyaheim $VERSION\" && git push origin v$VERSION"
elif [[ -n "$(git -C "$REPO_ROOT" status --porcelain)" ]]; then
  echo
  echo "aviso: a tag v$VERSION existe, mas há mudanças não commitadas — o zip pode não"
  echo "       corresponder ao que a tag aponta."
fi
