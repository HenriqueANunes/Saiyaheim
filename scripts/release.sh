#!/usr/bin/env bash
# Publica a versão atual como release no GitHub e no Thunderstore, com o zip do package.sh.
# No GitHub, a seção correspondente do CHANGELOG vira as notas; no Thunderstore, o próprio
# CHANGELOG.md dentro do zip vira a aba de changelog.
#
# Uso:  ./scripts/release.sh                      # GitHub + Thunderstore
#       ./scripts/release.sh --only-thunderstore  # repete só o Thunderstore (ex.: upload falhou)
#
# Não cria commit nem tag — isso continua sendo à mão. A ordem esperada é:
#   1. subir PluginVersion e escrever a seção no packaging/CHANGELOG.md
#   2. commitar
#   3. git tag -a vX.Y.Z -m "Saiyaheim X.Y.Z" && git push origin main vX.Y.Z
#   4. ./scripts/release.sh
#
# As checagens abaixo existem para que o zip publicado seja exatamente o código que a tag marca.
# O zip é compilado da árvore de trabalho, não da tag: se as duas divergirem, a release mente.
#
# Thunderstore: precisa do tcli (dotnet tool install -g tcli) e do token de uma service account
# do time Hman, em $THUNDERSTORE_TOKEN ou no arquivo ~/.config/saiyaheim/thunderstore-token.
# Time, comunidade e categorias ficam em thunderstore.toml, na raiz do repo.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHANGELOG="$REPO_ROOT/packaging/CHANGELOG.md"
TOKEN_FILE="$HOME/.config/saiyaheim/thunderstore-token"
TS_PACKAGE="Hman/Saiyaheim"

# O gh descobre o repositório pelo remoto da pasta atual; o tcli lê ./thunderstore.toml.
cd "$REPO_ROOT"

fail() {
  echo "erro: $*" >&2
  exit 1
}

ONLY_THUNDERSTORE=0
case "${1:-}" in
  "") ;;
  --only-thunderstore) ONLY_THUNDERSTORE=1 ;;
  *) fail "argumento desconhecido: $1 (use --only-thunderstore ou nada)" ;;
esac

VERSION="$(grep -oP 'PluginVersion\s*=\s*"\K[^"]+' "$REPO_ROOT/src/Saiyaheim/Plugin.cs")"
[[ -n "$VERSION" ]] || fail "não consegui ler PluginVersion de src/Saiyaheim/Plugin.cs"
TAG="v$VERSION"

# ---------- Ferramentas ----------

if (( ! ONLY_THUNDERSTORE )); then
  command -v gh >/dev/null || fail "gh não está instalado."
  gh auth status >/dev/null 2>&1 || fail "gh não está autenticado. Rode: gh auth login"
fi

# O tcli é uma dotnet tool: mora em ~/.dotnet/tools e, com o SDK vindo do asdf, só roda com o
# DOTNET_ROOT apontando para a instalação — sem isso sai "You must install .NET".
export PATH="$PATH:$HOME/.dotnet/tools"
command -v tcli >/dev/null || fail "tcli não está instalado. Rode: dotnet tool install -g tcli"
if [[ -z "${DOTNET_ROOT:-}" ]]; then
  SDK_DIR="$(dotnet --list-sdks | tail -n1 | grep -oP '\[\K[^\]]+')"
  [[ -n "$SDK_DIR" ]] || fail "não consegui descobrir o DOTNET_ROOT pelo dotnet --list-sdks."
  export DOTNET_ROOT="$(dirname "$SDK_DIR")"
fi

TS_TOKEN="${THUNDERSTORE_TOKEN:-}"
if [[ -z "$TS_TOKEN" && -f "$TOKEN_FILE" ]]; then
  TS_TOKEN="$(tr -d '[:space:]' < "$TOKEN_FILE")"
fi
[[ -n "$TS_TOKEN" ]] \
  || fail "sem token do Thunderstore. Defina THUNDERSTORE_TOKEN ou grave em $TOKEN_FILE"

# ---------- A árvore bate com a tag ----------

[[ -z "$(git -C "$REPO_ROOT" status --porcelain)" ]] \
  || fail "há mudanças não commitadas; o zip não corresponderia à tag $TAG."

git -C "$REPO_ROOT" rev-parse -q --verify "refs/tags/$TAG" >/dev/null \
  || fail "a tag $TAG não existe. Crie com: git tag -a $TAG -m \"Saiyaheim $VERSION\""

TAG_COMMIT="$(git -C "$REPO_ROOT" rev-parse "$TAG^{commit}")"
HEAD_COMMIT="$(git -C "$REPO_ROOT" rev-parse HEAD)"
[[ "$TAG_COMMIT" == "$HEAD_COMMIT" ]] \
  || fail "a tag $TAG aponta para ${TAG_COMMIT:0:7}, mas o HEAD é ${HEAD_COMMIT:0:7}."

# Sem a tag no remoto, o gh criaria uma nova a partir do branch padrão — que pode não ser o
# commit certo. O --verify-tag abaixo também recusa, mas aqui a mensagem diz o que fazer.
git -C "$REPO_ROOT" ls-remote --exit-code --tags origin "refs/tags/$TAG" >/dev/null \
  || fail "a tag $TAG não está no GitHub. Rode: git push origin $TAG"

# O README da página do Thunderstore puxa a logo de raw.githubusercontent.com/.../main/, então o
# main do GitHub precisa já conter este commit — senão a página nasce com imagem quebrada ou velha.
git -C "$REPO_ROOT" fetch -q origin main
git -C "$REPO_ROOT" merge-base --is-ancestor HEAD origin/main \
  || fail "o main do GitHub ainda não contém ${HEAD_COMMIT:0:7}. Rode: git push origin main"

if (( ! ONLY_THUNDERSTORE )) && gh release view "$TAG" >/dev/null 2>&1; then
  fail "a release $TAG já existe no GitHub. Para publicar só no Thunderstore: $0 --only-thunderstore"
fi

# Versão publicada no Thunderstore é queimada para sempre. Checar antes de criar a release do
# GitHub evita ficar com uma publicada e a outra impossível.
TS_STATUS="$(curl -s -o /dev/null -w '%{http_code}' \
  "https://thunderstore.io/api/experimental/package/$TS_PACKAGE/$VERSION/")"
case "$TS_STATUS" in
  404) ;;
  200) fail "a versão $VERSION já está publicada no Thunderstore. Suba PluginVersion." ;;
  *)   fail "não consegui consultar o Thunderstore (HTTP $TS_STATUS)." ;;
esac

# ---------- Notas: a seção desta versão no CHANGELOG ----------

# Tudo entre "## X.Y.Z" e o próximo "## ", sem as linhas em branco das pontas.
NOTES="$(awk -v header="## $VERSION" '
  $0 == header { inside = 1; next }
  inside && /^## / { exit }
  inside { print }
' "$CHANGELOG" | sed -e '/./,$!d' | sed -e ':a' -e '/^\n*$/{$d;N;ba' -e '}')"

[[ -n "$NOTES" ]] || fail "não achei a seção \"## $VERSION\" em packaging/CHANGELOG.md."

# ---------- Empacota ----------

"$REPO_ROOT/scripts/package.sh" Release

ZIP="$REPO_ROOT/dist/Saiyaheim-$VERSION.zip"
[[ -f "$ZIP" ]] || fail "o package.sh não gerou $ZIP."

# ---------- GitHub ----------

if (( ! ONLY_THUNDERSTORE )); then
  echo
  echo "Publicando $TAG no GitHub com as notas:"
  echo "$NOTES"
  echo

  gh release create "$TAG" "$ZIP" \
    --verify-tag \
    --title "Saiyaheim $VERSION" \
    --notes "$NOTES"
fi

# ---------- Thunderstore ----------

# Único passo sem volta: a release do GitHub pode ser apagada, a versão do Thunderstore não.
echo
read -r -p "Publicar $TS_PACKAGE $VERSION no Thunderstore? Não dá para desfazer. [s/N] " ANSWER
if [[ "$ANSWER" != [sS] ]]; then
  echo "Thunderstore pulado. Para publicar depois: $0 --only-thunderstore"
  exit 0
fi

tcli publish --file "$ZIP" --token "$TS_TOKEN" \
  || fail "o upload para o Thunderstore falhou. Para tentar de novo: $0 --only-thunderstore"

echo
echo "Publicado: https://thunderstore.io/c/valheim/p/$TS_PACKAGE/"
