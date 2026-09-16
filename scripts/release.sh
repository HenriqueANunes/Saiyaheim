#!/usr/bin/env bash
# Publica a versão atual como release no GitHub, com o zip do package.sh anexado e a seção
# correspondente do CHANGELOG como notas.
#
# Uso:  ./scripts/release.sh
#
# Não cria commit nem tag — isso continua sendo à mão. A ordem esperada é:
#   1. subir PluginVersion e escrever a seção no packaging/CHANGELOG.md
#   2. commitar
#   3. git tag -a vX.Y.Z -m "Saiyaheim X.Y.Z" && git push origin main vX.Y.Z
#   4. ./scripts/release.sh
#
# As checagens abaixo existem para que o zip anexado seja exatamente o código que a tag marca.
# O zip é compilado da árvore de trabalho, não da tag: se as duas divergirem, a release mente.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHANGELOG="$REPO_ROOT/packaging/CHANGELOG.md"

# O gh descobre o repositório pelo remoto da pasta atual.
cd "$REPO_ROOT"

fail() {
  echo "erro: $*" >&2
  exit 1
}

command -v gh >/dev/null || fail "gh não está instalado."
gh auth status >/dev/null 2>&1 || fail "gh não está autenticado. Rode: gh auth login"

VERSION="$(grep -oP 'PluginVersion\s*=\s*"\K[^"]+' "$REPO_ROOT/src/Saiyaheim/Plugin.cs")"
[[ -n "$VERSION" ]] || fail "não consegui ler PluginVersion de src/Saiyaheim/Plugin.cs"
TAG="v$VERSION"

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

if gh release view "$TAG" >/dev/null 2>&1; then
  fail "a release $TAG já existe no GitHub."
fi

# ---------- Notas: a seção desta versão no CHANGELOG ----------

# Tudo entre "## X.Y.Z" e o próximo "## ", sem as linhas em branco das pontas.
NOTES="$(awk -v header="## $VERSION" '
  $0 == header { inside = 1; next }
  inside && /^## / { exit }
  inside { print }
' "$CHANGELOG" | sed -e '/./,$!d' | sed -e ':a' -e '/^\n*$/{$d;N;ba' -e '}')"

[[ -n "$NOTES" ]] || fail "não achei a seção \"## $VERSION\" em packaging/CHANGELOG.md."

# ---------- Empacota e publica ----------

"$REPO_ROOT/scripts/package.sh" Release

ZIP="$REPO_ROOT/dist/Saiyaheim-$VERSION.zip"
[[ -f "$ZIP" ]] || fail "o package.sh não gerou $ZIP."

echo
echo "Publicando $TAG com as notas:"
echo "$NOTES"
echo

gh release create "$TAG" "$ZIP" \
  --verify-tag \
  --title "Saiyaheim $VERSION" \
  --notes "$NOTES"
