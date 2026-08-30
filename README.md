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
    ├── Thumbnail.png
    └── Locales\
        └── en.json
```

The folder name is the mod's display name, so rename it if you like.

`Thumbnail.png` is only read when uploading to the Steam Workshop from inside the game.
Delete it if you just want to play — it costs more disk space than the rest of the mod.

## Uninstall

**Delete the `Bigamst Trainer` folder.**

The enable/disable toggle on the Mods screen does not work for locally installed mods — the
game only stores that state for Steam Workshop items, so it is greyed out and always shows
as on. Deleting the folder is the supported way to remove a local mod. To disable it
temporarily, move the folder somewhere outside `ModsLocal`.

## Two ways in

**In game — the phone.** Open your phone and pick **Trainer**, at the end of the app row.
Everything is reachable while you play, without pausing.

**From the menu — Options → MODS.** The same controls, and the only surface that works
before a save is loaded. If the phone app ever fails to appear after a game update, this
one keeps working.

Both are built from the same list, so they never disagree. Toggles, sliders and dropdowns
remember their values between sessions.

The phone adds two things the menu cannot show, because the game's mod options have no text
field: an exact money amount, and the item spawner.

## Features

### Money
- **Quick add** $10,000, $100,000 or $1,000,000
- **Exact amount** *(phone only)* — type a figure, then **Add**, **Subtract** or **Set**
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
tickets and fines. Repair or refuel just the car you are sitting in.

**Tuning** for the vehicle you are driving: max speed, engine power, brake force, turn
radius, and damage taken — set damage to 0% and it stops caring about walls.

### Gameplay
Game speed from 0 to 500%. Skip an hour, eight hours or a day. Complete the current
objective or quest. Spawn customers into the business you are standing in. Toggle traffic,
pedestrians, seasonal item limits and invincibility.

### Spawn
*(phone only)* Start typing an item name and pick from the suggestions — search matches the
readable name, so "bread" finds it without knowing that its id is `ba:itemname_bread`. The
item appears in your hands, so you need empty hands, no vehicle and no placement mode.

### Rivals
Adjust rival difficulty, or defeat all rivals outright — which shuts down their businesses
and sells off their real estate, not just marks them beaten.

### Teleport
Jump to the destination you marked on the city map, go straight inside it, travel to your
current quest target, or head to the casino. **If you are driving, your car comes with you**
— it lands at the building's entrance, or its drive-in entrance where one exists, with its
physics reset properly.

### Time
Freeze the clock, or jump to a chosen hour. Jumping to an earlier hour moves you to the next
day rather than rewinding the current one.

## Known issues

This is a **0.9 beta**. Everything here has been used on a real save, but only by one person
on one machine — please report anything odd.

- **Buttons are captioned "Apply".** The game's mod UI never sets a caption on mod buttons,
  leaving a placeholder — this mod overwrites it. The label to the left of each button says
  what it does.
- **Freeze the clock is experimental.** Stopping time is the most invasive thing here and may
  interact badly with deliveries, shifts or rent. Try it on a save you do not mind losing.
- **Vehicle tuning follows you, not the car.** The sliders apply to whichever vehicle you are
  in when they run, and they re-apply when the menu is rebuilt — so a second car can pick up
  the first one's settings.
- **The phone app depends on internals.** It borrows the game's own option controls and app
  button, which a future patch could rename. If the Trainer app stops appearing, everything
  is still available under Options → MODS, and the log will say what it could not find.

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

## Publishing to the Steam Workshop

The game uploads mods itself, from **Main Menu → Mods → Upload**.

It scans the mod folder's **top level** for the first `.png`, `.jpg` or `.jpeg` under its
size limit and uses that as the Workshop preview image. The filename does not matter, only
that it sits in the folder root — which is why `Thumbnail.png` lives beside the DLL rather
than in a subfolder. The image is moved out of the folder during upload so it is not
published as mod content, then moved back afterwards.

You also need a title, a description, a changelog and a target build number (**3670** for
EA 0.11). The uploader refuses the submission if any of those are missing, and it rejects a
target build number higher than the game you are running.

## License

MIT — see [LICENSE](LICENSE).

This mod is not affiliated with Hovgaard Games. Big Ambitions is their trademark.
