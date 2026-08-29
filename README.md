# Bigamst Trainer — Big Ambitions

A trainer mod for [Big Ambitions](https://store.steampowered.com/app/1331550/Big_Ambitions/),
built on the game's **own mod API**. No BepInEx, no injectors, no dependency folder — one
DLL, and the game draws the menu itself.

Built and tested against **EA 0.11, Build 3670**.

## Install

1. Open `%LOCALAPPDATA%Low\Hovgaard Games\Big Ambitions\ModsLocal\`
   (paste that into the Explorer address bar).
2. Copy the `Bigamst Trainer` folder into it.
3. Start the game and load a save.
4. **Options → MODS**.

The folder must look like this. The game rejects a mod folder containing more than one DLL
in its root:

```
ModsLocal\
└── Bigamst Trainer\
    ├── BigamstTrainer.dll
    └── Locales\
        └── en.json
```

The folder name is the mod's display name, so rename it if you like.

## Uninstall

**Delete the `Bigamst Trainer` folder.**

The enable/disable toggle on the Mods screen does not work for locally installed mods — the
game only stores that state for Steam Workshop items, so it is greyed out and always shows
as on. Deleting the folder is the supported way to remove a local mod. To disable it
temporarily, move the folder somewhere outside `ModsLocal`.

## Features

Everything lives under **Options → MODS** and is grouped by category. Toggles, sliders and
dropdowns remember their values between sessions.

### Money
- Pick an amount ($1,000 up to $100,000,000), then **Add** or **Subtract**
- **Never drop below** — tops your balance back up whenever it falls under the set figure
- **Pay off all loans** — clears every outstanding balance and its daily interest

### Economy
Tax rate, employee wages, market prices, bank interest and selling return, each as a
percentage. Plus: no wholesale or import limits, all products available from importers, all
contacts unlocked, all courses unlocked.

Most of these are the game's own custom-game settings, normally fixed when you start a save.
This lets you change them mid-game.

### Businesses
- **Restock every shelf and fridge** — fills every container in every property you own, and
  marks the stock paid for
- **Keep everything restocked** — does the above automatically
- **Mark all stock as paid for** — clears what you owe without changing quantities
- **Remove all dirt**, and **Keep everything spotless**
- **No rent on owned property** — switch it off again and the original rent comes back

### Player
Keep energy, hunger and happiness full; disable aging; disable the energy system entirely;
restore all three at once.

### Employees
Keep everyone fully satisfied, clear absences and sick days, max out every skill.

### Vehicles
Disable damage and fuel use; repair, refuel and clean everything you own; clear parking
tickets and fines.

### Rivals
Adjust rival difficulty, or defeat all rivals outright — which shuts down their businesses
and sells off their real estate, not just marks them beaten.

### Teleport
Jump to the destination you marked on the city map, go straight inside it, or travel to your
current quest target. **If you are driving, your car comes with you** — it lands at the
building's entrance, or its drive-in entrance where one exists, with its physics reset
properly.

### Time
Freeze the clock, or jump to a chosen hour. Jumping to an earlier hour moves you to the next
day rather than rewinding the current one.

## Known issues

This is a **0.9 beta**. Everything below has been exercised on a real save, but only by one
person on one machine — please report anything odd.

- **Buttons in the mod menu are captioned "Apply".** The game's mod UI never sets a caption
  on mod buttons, leaving a placeholder ("YOUR TEXT HERE") — this mod overwrites it. The row
  label to the left says what each button does.
- **"Defeat all rivals" has not been tested against active rivals.** It calls the game's own
  `RivalsHelper.DefeatRival`, which shuts down their businesses and sells their real estate,
  but no save with live rivals has run it yet.
- **Freeze the clock is experimental.** Stopping time is the most invasive thing here and may
  interact badly with deliveries, shifts or rent. Try it on a save you do not mind losing.
- **Teleporting while driving** moves the car with you and resets its physics. It lands at
  the building's entrance, or its drive-in entrance where one exists.

## Building

Needs the .NET SDK 8 or newer. The game's assemblies are referenced directly from your
install, so nothing is bundled.

```bash
dotnet build src/BigamstTrainer/BigamstTrainer.csproj -c Release
```

The build copies the DLL and `Locales` into `ModsLocal\Bigamst Trainer\` automatically.

If the game is not at the default path:

```bash
dotnet build src/BigamstTrainer/BigamstTrainer.csproj -c Release -p:GameDir="D:\Games\Big Ambitions"
```

Add `-p:DeployToGame=false` to build without installing.

**Mono caches assemblies for the session**, so fully quit and relaunch the game after every
build. Alt-tabbing will not pick up a new DLL.

`NOTES.md` documents the mod API and the game internals this relies on, all taken from
decompiler output rather than guesswork. Worth reading before changing anything.

## Translating

Copy `Locales/en.json` to `Locales/<locale>.json` and translate the values, leaving the keys
and any `{value}` placeholders alone. The game loads it automatically.

## License

MIT — see [LICENSE](LICENSE).

This mod is not affiliated with Hovgaard Games. Big Ambitions is their trademark.
