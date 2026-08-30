# Bigamst Trainer

A trainer mod for [Big Ambitions](https://store.steampowered.com/app/1331550/Big_Ambitions/),
built on the game's **own mod API** — no BepInEx, no injectors, no dependency folder.

**Version 0.10.0.** Tested against EA 0.11, Build 3670.

## Install

Copy the `Bigamst Trainer` folder into:

```
%LOCALAPPDATA%Low\Hovgaard Games\Big Ambitions\ModsLocal\
```

Paste that path into the Explorer address bar. The folder should look like this — the game
rejects a mod folder with more than one DLL in its root:

```
ModsLocal\
└── Bigamst Trainer\
    ├── BigamstTrainer.dll
    ├── Thumbnail.png          (only used when uploading to the Workshop — safe to delete)
    └── Locales\
        └── en.json
```

The folder name is the mod's display name, so rename it if you like.

**To uninstall, delete the folder.** The enable/disable toggle on the Mods screen does not
work for local mods — the game only stores that state for Workshop items, so it is greyed
out and always shows as on.

## Two ways in

**The phone.** Open your phone in game and pick **Trainer**, at the end of the app row.
Everything is reachable while you play, laid out in tabs.

**Options → MODS.** The same controls, and the only surface that works before a save is
loaded.

Both are built from one list, so they never disagree, and your settings persist between
sessions. Two things appear only on the phone, because the game's mod options have no text
field: the exact money amount, and the item spawner.

## Features

### Money
Quick add $10,000, $100,000 or $1,000,000. Type an exact amount and **Add**, **Subtract** or
**Set** it. Keep your balance above a floor. Pay off all loans, clearing the interest with
them.

### Time
Freeze the clock, or jump to any hour — jumping backwards moves you to the next day rather
than rewinding the current one.

### Economy
Tax rate, employee wages, market prices, bank interest and selling return, each as a
percentage. Remove wholesale and import limits. Unlock all importer products, contacts and
courses.

Most of these are the game's own custom-game settings, normally fixed when you start a save.
This lets you change them mid-game.

### Player
Keep energy, hunger and happiness full, or restore all three at once. Disable aging or the
energy system entirely. Complete or clear all personal goals.

### Businesses
Restock every shelf and fridge across everything you own, and mark the stock paid for.
Remove all dirt. Waive rent — switching it back off restores the original amounts. Restocking
and cleaning can both run automatically.

### Employees
Keep everyone fully satisfied, clear absences and sick days, max out every skill.

### Vehicles
Disable damage and fuel use. Repair, refuel and clean everything you own, or just the car
you are in. Clear parking tickets and fines.

**Tuning** applies to one car at a time. Set max speed, engine power, brake force, turn
radius and damage taken, then press **Apply tuning to the car you are in** — nothing changes
until you press it, so each car keeps its own settings. Damage at 0% means walls stop
mattering.

### Rivals
Adjust rival difficulty, or defeat them all — which shuts down their businesses and sells
off their real estate, not just marks them beaten.

### Gameplay
Game speed from 0 to 500%. Skip an hour, eight hours or a day. Complete the current objective
or quest. Spawn customers into the business you are standing in. Toggle traffic, pedestrians,
seasonal item limits and invincibility.

### Teleport
Jump to the destination marked on your city map, straight inside it, to your current quest
target, or to the casino. **If you are driving, your car comes with you** — it lands at the
building's entrance, with its physics reset properly.

**Waypoints** let you save the spot you are standing on under a name, then travel back to it
or delete it later. Saving does not work inside a building.

### Utility
*(phone only)* Start typing an item name and pick from the suggestions — search matches the
readable name, so "bread" finds it without knowing its id is `ba:itemname_bread`. The item
appears in your hands, so you need empty hands, no vehicle and no placement mode.

**Reset all settings** returns every control to its default. It does not undo cheats already
applied to your save.

## Known issues

Everything here has been used on a real save, but only by one person on one machine — please
report anything odd.

- **Buttons are captioned "Apply".** The game's mod UI never sets a caption on mod buttons,
  leaving a placeholder that this mod overwrites. The label to the left says what each does.
- **Steam achievements lag behind.** Goals you had not genuinely earned unlock only after you
  reload the save; the game grants those during its own check on load.
- **Freezing the clock is experimental.** It is the most invasive feature here and may
  interact badly with deliveries, shifts or rent. Try it on a save you do not mind losing.
- **Some vehicles have no speed limiter or damage module**, and the game says so when tuning
  them. That is normal.
- **The phone app relies on game internals** a future patch could rename. If the Trainer app
  stops appearing, everything is still under Options → MODS.

## Translating

Copy `Locales/en.json` to `Locales/<locale>.json` and translate the values, leaving the keys
and any `{value}` placeholders alone. The game loads it automatically.

## Building

Needs the .NET SDK 8 or newer. The game's assemblies are referenced from your install, so
nothing is bundled.

```bash
dotnet build src/BigamstTrainer/BigamstTrainer.csproj -c Release
```

That deploys straight into `ModsLocal\Bigamst Trainer\`. Add `-p:DeployToGame=false` to skip
that, or point at a different install:

```bash
dotnet build src/BigamstTrainer/BigamstTrainer.csproj -c Release -p:GameDir="D:\Games\Big Ambitions"
```

**Mono caches assemblies for the session**, so fully quit and relaunch the game after every
build — alt-tabbing will not pick up a new DLL.

`NOTES.md` documents the mod API and the game internals this relies on, all taken from
decompiler output rather than guesswork, including how Workshop uploads work. Worth reading
before changing anything.

## License

MIT — see [LICENSE](LICENSE).

Not affiliated with Hovgaard Games. Big Ambitions is their trademark.
