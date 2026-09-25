# Changelog

## 0.5.1

- Changed: the Kamehameha reaches a full charge in 4 seconds instead of 5.
- Changed: hovering in place while enemies are alerted nearby now costs 3x ki instead of 2x.
- Changed: aim flight is now the default; Alt+F switches to classic flight. Characters that already picked a mode keep it.
- Changed: a kill now restores half of your ki bar and a parry a quarter;
- Fixed: you can no longer gain altitude while charging ki in flight; rising or diving now interrupts the charge.
- Fixed: you can no longer charge ki while charging a Kamehameha.

## 0.5.0

- Added: the **SSJ God**, a fourth form, unlocked by defeating Moder. It hits a little harder
  than SSJ2 and a little softer than SSJ3, and what it buys instead is staying power: less ki
  drain, half the combat surcharge on punching, blocking and taking hits, and passive healing
  that ticks three times as often and keeps going through Wet, Cold and Freezing.
- Added: aim flight. Alt+F switches between classic flight and flying where you look, up and down included.
- Fixed: you can now take off while swimming; you pop out of the water and start flying.
- Fixed: going through a portal or a dungeon entrance no longer drops your form or your flight.

## 0.4.3

- Fixed: on a server, players now use the server's config instead of their own.
- Fixed: changes to the server's config apply live, without a restart.
- Fixed: typing in the build menu's search field no longer triggers the mod's keys.
- Fixed: flying over a creature no longer ends the flight.

## 0.4.2

- Changed: fighting inside a form costs far less ki. A form used to charge its whole power
  multiplier as a surcharge on every punch, block and hit taken; it now charges a fifth of it
  (`CombatFormKiShare` 1 → 0.2), so a 4x form lands 2.5x the damage per point of ki. Mastery
  still clears the surcharge at level 100.
- Changed: form mastery levels much more slowly — XP per point of damage went from 0.25 to
  0.0125, and the per-hit cap from 25 to 1.25. The boss catch-up bonus for older forms now
  climbs to x4 instead of x2 (`MasteryXpBossMultiplierMax`). Flight and Power Level are
  unchanged.
- Changed: existing config files are moved to these values on load even if you had tuned those
  keys — they are one balance pass, and half of it is worse than none. The log says what your
  old values were.

## 0.4.1

- Changed: form mastery and the flight skill now level at half the speed. Mastery XP per point
  of damage, dealt and taken, went from 0.5 to 0.25 for every form (and the per-hit cap from 50
  to 25, so it still bites at the same damage), and flight XP went from 0.15 to 0.075 per metre.
  Power Level is unchanged. Existing configs are updated on load, even if you had tuned these
  keys yourself — the log says what your old values were.
- Fixed: parrying the training dummy (T.W.I.G.) now gives ki, like any other parry. A parry
  that fails — no stamina left, or staggered by the hit — still gives nothing.

## 0.4.0

- Changed: **ki is now the fuel for fighting, not a tax on existing.** Holding a form used to
  cost the same whether you were crossing the map or trading blows, which pushed everyone into
  standing still to charge ki before they could play. The drain is now small (1 / 2 / 3 per
  second for SSJ / SSJ2 / SSJ3, down from 5 / 10 / 15) and the cost moved to what you do inside
  the form: punching, blocking, taking hits and firing ki attacks. Living in a form is cheap;
  fighting in one is what costs. Thanks to everyone who wrote this up in the issues.
- Changed: **a form charges a surcharge on every combat cost, and its mastery pays it off.** At
  mastery 0 a form with 4x power costs 4x the ki per action — the same damage per bar as base
  form, so what the form buys you there is the bigger hit, not efficiency. At mastery 100 the
  surcharge is gone and the whole multiplier is profit. Tune with `CombatFormKiShare` and
  `MasteryFormCostReduction`.
- Changed: **form mastery now trains by fighting inside the form**, from damage dealt and taken,
  instead of by seconds spent transformed. Standing around transformed — or flying — no longer
  trains anything. `MasteryXpPerDamageDealt` and `MasteryXpPerDamageTaken` replace
  `MasteryXpPerSecond`.
- Changed: **the flight skill now trains by distance flown, not time airborne.** Hovering in
  place pays nothing, and flying out and back pays for both legs. `XpPerMeter` replaces
  `XpPerSecond`.
- Changed: early flight is no longer a grind wall. The base cost went from 5 to 3.5 ki/s, and
  the skill discount is now linear instead of back-loaded (`KiSkillCurve` 2 → 1), so level 25
  already flies 24% cheaper instead of 4%.
- Changed: transforming now makes flying cost more, not just go faster — the surcharge matches
  the speed the form gives, and fades to nothing as you master that form (`FormKiShare`).
- Fixed: blocking and taking hits got cheaper the stronger you were, and transforming made it
  worse — in SSJ3 a block stopped over three times more damage for the same ki. Those two costs
  no longer take the punch's power discount (`DefenseKiCostPowerReduction`), and their rates
  were recalibrated (`BlockKiCost` 0.5 → 0.3, `DamageTakenKiCost` 1 → 0.6).
- Changed: **hovering in the air is no longer cheaper than flying**, and hovering while something
  hostile is alerted nearby now costs double (`HoverKiMultiplier`, `CombatHoverMultiplier`,
  `CombatHoverRange`). Hanging out of reach while a boss cannot touch you was the one thing the
  mod was actively paying you to do — it cost half price. Flying in combat is untouched: diving,
  circling and pulling out is air combat, and that is the point. Only holding still is charged.
- Changed: **your existing config file is updated to the new balance automatically.** Keys you
  never touched are moved to the new values; keys you tuned yourself are kept, and the log says
  which. The balance keys of this rework are the exception — they are overwritten either way,
  because half of this rework is worse than none of it, and the log tells you what your value
  was so you can put it back. Two keys are reset even though their default did not change —
  `KiCostPowerReduction` and `MasteryFormCostReduction` — because the formula around them did,
  and a number tuned against the old one no longer means what you meant by it.

- New: a radial menu for your forms and your ki attacks. Two groups sit in the game's own
  wheel, next to consumables, weapons and emotes, so the mod costs you no extra key. Forms show
  their ki drain, attacks show their ki cost, and picking an attack selects it — you still fire
  it with your own key, after you aim. Only what you have unlocked shows up, with its own
  artwork; the old keys keep working.
- Changed: dropping back to base form is now `Z`, and stepping one form down is `Shift+Z`. They
  used to be `G`, which current Valheim versions also use to open the radial menu. If you already
  have a config file, yours keeps `G` until you change it — new installs get `Z`.
- Changed: new artwork for the skill icons — Power Level, Flight and the three Super Saiyan
  forms. They now carry a transparent background, so they sit on the skill wheel and the HUD
  without the black square around them.

- Fixed: SSJ2 and SSJ3 no longer spam `Light was null! This should never happen!` into the log.
- Everyone must update: players on 0.3.x can't join a 0.4.0 server.

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
- Changed: transforming now shows in your power rating. SSJ reads 2x your base form, SSJ2 3x
  and SSJ3 4x. Before, the form only showed through the armor and punch it adds, and since it
  gives no health, SSJ read about 1.3x. Your base-form number is unchanged. Tune with
  `RatingFormShare` (0 hides the form, 1 is the full multiplier).
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
