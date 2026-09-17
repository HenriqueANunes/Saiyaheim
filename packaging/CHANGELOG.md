# Changelog

## 0.3.0

- New: gain ki by fighting well. A successful parry gives the ki cost of 2 punches, and
  landing the killing blow on a creature gives 4. Both scale with your power, like the punch
  cost. Only with ki on; tamed creatures and players give nothing. Tune with
  `KiOnParryPunches` and `KiOnKillPunches` (0 turns each off).
- New: the ki blast explodes on impact, hitting everything within 2 m for full damage —
  buildings included (`ImpactRadius`, per attack; the Kamehameha stays at 0). To make up for
  it, its damage went down 10% (`DamageBase` 10 → 9, `DamageFromPower` 0.04 → 0.036). Servers
  with an existing config keep the old damage until those two lines are updated.
- New: the flight skill now softens falls. With ki on, fall damage drops with your flight
  level, up to 80% less at level 100 (`FallDamageSkillReduction`). Turning ki off takes the
  protection away.
- Fixed: with ki on, equipping a weapon no longer inflates your power rating. The number
  used to add the weapon's damage on top of the ki punch bonus, a hit that never happens:
  the bonus only applies to unarmed strikes. With ki on it now reads the unarmed punch plus
  the bonus; with ki off it still reads the equipped weapon. Expect the number to drop if
  you carry a weapon.
- Fixed: charging the Kamehameha now holds you in place. On the ground you can no longer
  walk or run while charging; in flight you hover still, with no climbing or diving. Aiming
  stays free. You can't block or attack while charging. Jumping or dodging on the ground drops the
  charge without firing.
- Fixed (multiplayer): other players' ki blast and Kamehameha impacts now show in the
  attack's color, without smoke. They used to show as the game's raw purple, smoky explosion.
- Everyone must update: players on 0.2.x can't join a 0.3.0 server.

## 0.2.0

- Other players' power rating now shows under their health bar, the same way enemies'
  does. Each player computes their own number and shares it over the network, so what you
  see on a friend matches the number under their minimap. Like the enemy number, it only
  shows while your own ki is on.
- Everyone must update: players on 0.1.x can't join a 0.2.0 server.

## 0.1.3

- Transformation mastery XP bonus from bosses is now capped at x2. A form still trains
  faster once the world has killed a boss past the one that unlocked it, but it no longer
  climbs to x3, x5 and beyond as more bosses fall. The new `MasteryXpBossMultiplierMax`
  key (per form) sets the cap; 1 turns the boss bonus off.

## 0.1.2

- Fixed (multiplayer): other players' ki blasts and Kamehameha now show the configured
  color, size and trail on your screen. Before, only the shooter saw the custom look;
  everyone else saw the raw projectile from the base game. Every player needs this version
  for the fix to show.

## 0.1.1

- HUD power label is now `BP:` (Battle Power) instead of `PB:`, on both the player readout
  and the enemy nameplate.
- Fixed: hitting with a vanilla weapon no longer trains Power Level. Only punches and ki
  attacks pay XP for damage dealt; damage taken still pays regardless. The new
  `XpWeaponFactor` key brings weapon XP back for anyone who wants it.

## 0.1.0

First public release.

- Ki as a resource, with regeneration and a HUD bar.
- Flight.
- Unarmed combat scaling with power.
- Ki attacks: Kamehameha and ki blasts.
- Super Saiyan transformations gated behind the game's bosses.
- Power level readout on the HUD and over enemies.
