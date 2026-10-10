# Adrenaline Config

A BepInEx plugin for Valheim that makes the adrenaline system configurable: how fast adrenaline
builds, how much an item needs before its effect triggers, how long that effect lasts and how
quickly adrenaline drains.

Every setting scales the game's own value, so nothing has to be set per item. Gains are
multipliers: 1 is the unmodded game and 2 doubles a gain, whatever the item, weapon or enemy
defines. Losses and the adrenaline required are percentages: 100 is the unmodded game and 50
halves a value.

Settings only go in the direction that makes adrenaline easier: gains can be raised, losses and
the adrenaline required can be lowered.

**Early test version.** It compiles against the current game but has not been tested in-game
yet.

## Requirements

- Valheim with [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) 5.4
- Optional: a configuration manager mod to change settings in-game. Changes apply right away.

## Install

Put `AdrenalineConfig.dll` into `BepInEx\plugins\AdrenalineConfig`.

The plugin is client-side. It only affects your own character and needs nothing on the server.

## Settings

Settings live in `BepInEx\config\berv.AdrenalineConfig.cfg`. Multipliers go from 1 to 10 in
steps of 0.1. Loss percentages go from 0 to 100.

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| General | Enabled | on | Off: adrenaline behaves as in the unmodded game |
| Trigger | Adrenaline required | 100 | Adrenaline an item needs before its effect triggers (1 to 100) |
| Trigger | Resize adrenaline bar | on | The bar gets shorter or longer with the adrenaline required. Off: it keeps its usual length |
| Trigger | Effect duration multiplier | 1 | Multiplies the duration of the effect an item triggers at full adrenaline |
| Gain multipliers | All gains | 1 | Multiplies every gain, on top of the individual sources below |
| Gain multipliers | Melee hit | 1 | Hitting an enemy with a melee attack |
| Gain multipliers | Projectile hit | 1 | Hitting an enemy with an arrow, bolt, spell or other projectile |
| Gain multipliers | Block | 1 | Blocking an attack |
| Gain multipliers | Parry | 1 | Parrying an attack |
| Gain multipliers | Perfect dodge | 1 | Dodging through an attack |
| Gain multipliers | Stagger enemy | 1 | Staggering an enemy |
| Gain multipliers | Forsaken power | 1 | Activating a forsaken power |
| Gain multipliers | Status effects | 1 | Adrenaline granted up front by status effects, such as consumables |
| Gain multipliers | Weapon use | 1 | Weapons that grant adrenaline on every attack, hit or miss |
| Loss | Missed attack | 100 | Adrenaline lost when a melee attack hits nothing. 0 removes the penalty |
| Loss | Damage taken | 100 | Adrenaline lost when taking a hit without blocking. 0 removes the penalty |
| Loss | Decay rate | 100 | How fast adrenaline drains out of combat. 0 keeps it until it is used |
| Loss | Decay delay multiplier | 1 | Multiplies the time after the last gain before adrenaline starts to drain |
| Nearby enemies | Scale gain with nearby enemies | off | Every gain is increased for each nearby enemy beyond the first |
| Nearby enemies | Gain bonus per extra enemy | 25 | Percent added per extra enemy. At 25, two enemies give 125% and three give 150% (0 to 500) |
| Nearby enemies | Enemy range | 20 | Distance in meters within which an enemy counts as nearby (5 to 100) |
| Nearby enemies | Gain while sprinting near multiple enemies | off | Sprinting builds adrenaline while two or more enemies are nearby |
| Nearby enemies | Sprint gain per second | 1 | Adrenaline per second from that sprinting, before the other gain settings apply (0 to 20) |

An enemy counts as nearby while it is hostile, not tamed, alerted and within range.

## Things to know

- Item tooltips still show the game's own numbers for adrenaline required, parry adrenaline and
  effect duration.
- The game's own modifiers still apply on top: the adrenaline rate world modifier, the way gains
  change as the bar fills, and status effects that raise adrenaline gain.
- If a game update moves the adrenaline code, the affected setting stops working and a warning
  is written to `BepInEx\LogOutput.log`. Nothing else breaks.

## Build

The build runs in a .NET SDK Docker container, so no SDK is needed on the machine. It
references the game's own assemblies and BepInEx from your Valheim install.

```powershell
.\build.ps1                       # builds to .\dist
.\build.ps1 -Install              # also copies the dll into BepInEx\plugins\AdrenalineConfig
.\build.ps1 -GameDir "D:\Valheim" # if the game is not in the default Steam folder
```
