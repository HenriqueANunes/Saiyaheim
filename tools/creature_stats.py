#!/usr/bin/env python3
"""Lê HP e ataques de criaturas direto dos prefabs do jogo extraído.

Uso:
    python3 tools/creature_stats.py Greydwarf Troll Draugr          # tabela markdown
    python3 tools/creature_stats.py --json Greydwarf Troll > out.json

A fonte é o projeto Unity que o AssetRipper gera a partir do Valheim
(~/Downloads/valheim-ripped/ExportedProject/Assets, ou --root). Os sites de wiki não trazem dano
por ataque; o prefab traz.

Como o jogo guarda isso: cada ataque de criatura é um item de arma. O prefab da criatura
referencia esses itens em m_defaultItems (sempre recebe), m_randomWeapon (sorteia UM) e
m_randomSets (sorteia UM conjunto). Cada item tem m_damages e m_aiAttackInterval.

O dano "em estrutura" (chop + pickaxe) sai separado porque só acerta construção, mas o
HitData.DamageTypes.GetTotalDamage() do jogo soma tudo — e é esse que o PowerRating.cs lê.
"""
import argparse
import json
import os
import re
import sys

DEFAULT_ROOT = os.path.expanduser("~/Downloads/valheim-ripped/ExportedProject/Assets")

# ItemDrop.ItemData.IsWeapon(): OneHandedWeapon, Bow, TwoHandedWeapon, Torch, TwoHandedWeaponLeft.
WEAPON_TYPES = {3, 4, 14, 15, 22}
STRUCTURE = ("chop", "pickaxe")


def index_prefabs(root):
    by_guid, by_name = {}, {}
    for d, _, files in os.walk(root):
        for f in files:
            if f.endswith(".prefab.meta"):
                path = os.path.join(d, f)
                with open(path) as fh:
                    for line in fh:
                        if line.startswith("guid:"):
                            by_guid[line.split()[1]] = path[:-5]
                            break
            elif f.endswith(".prefab"):
                by_name.setdefault(f[:-7], os.path.join(d, f))
    return by_guid, by_name


def read_attack(path):
    text = open(path, errors="ignore").read()
    m = re.search(r"m_damages:\n((?:\s+m_\w+: [-\d.e]+\n){11})", text)
    if not m:
        return None
    dmg = {k: float(v) for k, v in re.findall(r"m_(\w+): ([-\d.e]+)", m.group(1)) if float(v)}
    if not dmg:
        return None
    interval = re.search(r"m_aiAttackInterval: ([\d.]+)", text)
    item_type = re.search(r"m_itemType: (\d+)", text)
    return {
        "item": os.path.basename(path)[:-7],
        "combat": sum(v for k, v in dmg.items() if k not in STRUCTURE),
        "structure": sum(v for k, v in dmg.items() if k in STRUCTURE),
        "types": [k for k in dmg if k not in STRUCTURE],
        "interval": float(interval.group(1)) if interval else 0.0,
        "weapon": bool(item_type) and int(item_type.group(1)) in WEAPON_TYPES,
    }


def read_creature(name, by_guid, by_name):
    path = by_name.get(name)
    if not path:
        raise SystemExit(f"prefab não encontrado: {name}")
    text = open(path, errors="ignore").read()
    health = re.search(r"m_health: ([\d.]+)", text)
    block = "m_defaultItems:" + re.search(r"m_defaultItems:(.*?)m_unarmedWeapon", text, re.S).group(1)

    def section(field):
        m = re.search(field + r":(.*?)\n  m_(?!name|items)\w+:", block + "\n  m_end:", re.S)
        return m.group(1) if m else ""

    def attacks(chunk):
        out = []
        for g in dict.fromkeys(re.findall(r"guid: (\w+)", chunk)):
            a = by_guid.get(g) and read_attack(by_guid[g])
            if a and a["weapon"]:
                out.append(a)
        return out

    sets = [attacks(m.group(1)) for m in
            re.finditer(r"- m_name: \S+\n    m_items:\n((?:    - \{.*\}\n)*)", section("m_randomSets") + "\n")]
    return {
        "prefab": name,
        "hp": float(health.group(1)) if health else 0.0,
        "fixed": attacks(section("m_defaultItems")),
        "oneOf": attacks(section("m_randomWeapon")),
        "sets": [s for s in sets if s],
    }


def creature_dps(c, structure=True):
    """Espelha PowerRating.GetCreatureDps, com a média dos inventários que o sorteio pode dar."""
    def hit(a):
        return (a["combat"] + (a["structure"] if structure else 0)) / max(0.05, a["interval"])

    picks = [[a] for a in c["oneOf"]] or [[]]
    sets = c["sets"] or [[]]
    pools = [c["fixed"] + p + s for p in picks for s in sets]
    each = [sum(hit(a) for a in inv) / len(inv) if inv else 0 for inv in pools]
    return sum(each) / len(each)


def markdown(creatures):
    print("| Prefab | HP | Ataques (dano de combate, tipo, intervalo) | DPS no mod | DPS sem estrutura |")
    print("|---|---|---|---|---|")
    for c in creatures:
        seen, parts = set(), []
        for a in c["fixed"] + c["oneOf"] + [a for s in c["sets"] for a in s]:
            if a["item"] in seen:
                continue
            seen.add(a["item"])
            extra = f" (+{a['structure']:g} estrutura)" if a["structure"] else ""
            parts.append(f"{a['combat']:g} {'+'.join(a['types'])}{extra} / {a['interval']:g} s")
        print(f"| `{c['prefab']}` | {c['hp']:g} | {'; '.join(parts)} | "
              f"{creature_dps(c):.1f} | {creature_dps(c, structure=False):.1f} |")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prefabs", nargs="+")
    ap.add_argument("--root", default=DEFAULT_ROOT)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    by_guid, by_name = index_prefabs(args.root)
    creatures = [read_creature(n, by_guid, by_name) for n in args.prefabs]
    if args.json:
        json.dump(creatures, sys.stdout, indent=1)
    else:
        markdown(creatures)


if __name__ == "__main__":
    main()
