# Changelog

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
