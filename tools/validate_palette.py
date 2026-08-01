"""Porte em Python dos checks de validate_palette.js (sem node no ambiente)."""
import math, sys

BAND = {"light": (0.43, 0.77), "dark": (0.48, 0.67)}
CHROMA_FLOOR = 0.10
CVD_TARGET, CVD_FLOOR = 8.0, 6.0
NORMAL_FLOOR = 15.0
CONTRAST_MIN = 3.0

MACHADO = {
    "protan": [[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]],
    "deutan": [[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]],
    "tritan": [[1.255528, -0.076749, -0.178779], [-0.078411, 0.930809, 0.147602], [0.004733, 0.691367, 0.303900]],
}

def hex2srgb(h):
    h = h.strip().lstrip("#")
    return [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]

def s2lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def lin(h):
    return [s2lin(c) for c in hex2srgb(h)]

def rel_lum(h):
    r, g, b = lin(h)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b

def contrast(a, b):
    hi, lo = sorted([rel_lum(a), rel_lum(b)], reverse=True)
    return (hi + 0.05) / (lo + 0.05)

def oklab_from_lin(rgb):
    r, g, b = rgb
    l = (0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b) ** (1 / 3)
    m = (0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b) ** (1 / 3)
    s = (0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b) ** (1 / 3)
    return [
        0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
        1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
        0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
    ]

def oklch(h):
    L, a, b = oklab_from_lin(lin(h))
    return L, math.hypot(a, b)

def simulate(h, kind):
    r, g, b = lin(h)
    M = MACHADO[kind]
    return [min(1, max(0, M[i][0] * r + M[i][1] * g + M[i][2] * b)) for i in range(3)]

def delta_e(h1, h2, kind=None):
    a = oklab_from_lin(simulate(h1, kind) if kind else lin(h1))
    b = oklab_from_lin(simulate(h2, kind) if kind else lin(h2))
    return 100 * math.dist(a, b)

def validate(palette, mode, surface):
    lo, hi = BAND[mode]
    print(f"\n=== {mode}  surface {surface} ===")
    ok = True

    off = [(c, round(oklch(c)[0], 3)) for c in palette if not (lo <= oklch(c)[0] <= hi)]
    print(f"[{'FAIL' if off else 'PASS'}] faixa de lightness {lo}-{hi}: {off or 'todos dentro'}")
    ok &= not off

    low = [(c, round(oklch(c)[1], 3)) for c in palette if oklch(c)[1] < CHROMA_FLOOR]
    print(f"[{'FAIL' if low else 'PASS'}] chroma >= {CHROMA_FLOOR}: {low or 'todos acima'}")
    ok &= not low

    pairs = [(i, i + 1) for i in range(len(palette) - 1)]
    for i, j in pairs:
        p = delta_e(palette[i], palette[j], "protan")
        d = delta_e(palette[i], palette[j], "deutan")
        t = delta_e(palette[i], palette[j], "tritan")
        n = delta_e(palette[i], palette[j])
        worst = min(p, d)
        tag = "PASS" if worst >= CVD_TARGET else ("WARN" if worst >= CVD_FLOOR else "FAIL")
        print(f"[{tag}] CVD {palette[i]}/{palette[j]}: protan {p:.1f} deutan {d:.1f} tritan {t:.1f} (alvo >= {CVD_TARGET})")
        ntag = "PASS" if n >= NORMAL_FLOOR else "FAIL"
        print(f"[{ntag}] visao normal {palette[i]}/{palette[j]}: dE {n:.1f} (piso {NORMAL_FLOOR})")
        ok &= worst >= CVD_FLOOR and n >= NORMAL_FLOOR

    for c in palette:
        k = contrast(c, surface)
        tag = "PASS" if k >= CONTRAST_MIN else "WARN"
        print(f"[{tag}] contraste {c} vs superficie: {k:.2f} (min {CONTRAST_MIN})")

    print("=> " + ("OK" if ok else "CORRIGIR"))
    return ok

if __name__ == "__main__":
    pal = sys.argv[1].split(",")
    validate(pal, sys.argv[2], sys.argv[3])
