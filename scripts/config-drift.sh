#!/usr/bin/env bash
# Lista as entradas do .cfg cujo valor atual diverge do default definido no SaiyaheimConfig.cs.
#
#   ./scripts/config-drift.sh [perfil]
#
# Por que existe: mudar o default no código NÃO altera um .cfg que já existe. O default só vale
# para geração nova — instalação limpa, .cfg apagado, ou a máquina de outro jogador.
#
# Ou seja: valor calibrado no playtest que não subir para o SaiyaheimConfig.cs fica só nesta
# máquina, e quem instalar do zero pega o chute inicial. Este script mostra o que falta subir.
#
# O próprio BepInEx escreve "# Default value: X" acima de cada entrada, então a comparação sai
# do arquivo, sem precisar parsear o C#.
#
# ATENÇÃO: esses comentários só são reescritos quando o jogo roda com a DLL nova. Depois de
# alterar um default no SaiyaheimConfig.cs, abra o jogo uma vez antes de confiar neste relatório
# — senão ele ainda compara contra o default antigo.

set -euo pipefail

PROFILE="${1:-Default}"
CFG="$HOME/.config/r2modmanPlus-local/Valheim/profiles/$PROFILE/BepInEx/config/com.hman.saiyaheim.cfg"

if [[ ! -f "$CFG" ]]; then
  echo "erro: $CFG não existe. Abra o jogo uma vez." >&2
  exit 1
fi

# O python do asdf exige versão fixada por diretório; o do sistema sempre existe.
PYTHON="$(command -v /usr/bin/python3 || command -v python3)"

"$PYTHON" - "$CFG" <<'PY'
import re, sys

path = sys.argv[1]
default = None
section = None
drift = []

for line in open(path, encoding="utf-8"):
    line = line.rstrip("\n")
    if line.startswith("["):
        section = line.strip("[]")
    m = re.match(r"# Default value: (.*)$", line)
    if m:
        default = m.group(1).strip()
        continue
    m = re.match(r"^([A-Za-z0-9_]+) = (.*)$", line)
    if m and default is not None:
        key, value = m.group(1), m.group(2).strip()
        if value != default:
            drift.append((section, key, default, value))
        default = None

if not drift:
    print("Nenhuma divergência: o .cfg está igual aos defaults do código.")
    sys.exit(0)

print(f"{len(drift)} entrada(s) divergindo do default do código:\n")
for section, key, default, value in drift:
    print(f"  [{section}] {key}")
    print(f"      código: {default}")
    print(f"      atual:  {value}\n")
print("Valores de balanceamento que já se provaram devem subir para SaiyaheimConfig.cs.")
print("Preferências pessoais (teclas, debug) podem ficar só aqui.")
PY
