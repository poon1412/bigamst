# Bigamst Trainer

A trainer mod for [Big Ambitions](https://store.steampowered.com/app/1331550/Big_Ambitions/),
built on the game's **own mod API** — no BepInEx, no injectors, no dependency folder.

**Version 1.0.1.** Tested against EA 0.11, Build 3674.

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

**The phone.** A **Trainer** icon sits on your phone, right after Persona — one click and
you are in. Everything is reachable while you play, laid out in tabs.

**Options → MODS.** The same controls, and the only surface that works before a save is
loaded.

Both are built from one list, so they never disagree. Your settings persist between
sessions and are put back into force as soon as the mod loads, without you having to open
anything — so a box that shows as on really is on. If you would rather start each session
clean, turn off **Apply my settings when the game loads** under Utility: every control then
returns to its default each time a save loads, so nothing is running and nothing shows as
running. Two things appear only on the phone, because the game's mod options have no text
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

**Movement speed** from 50% to 300%, applied to walking, jogging and running alike, with
**Movement speed back to normal** to return to 100% without hunting for it on the slider.
Riding a scooter is unaffected — that is a vehicle, and the vehicle tuning covers it.

### Businesses
Restock every shelf and fridge across everything you own, and mark the stock paid for.
Remove all dirt. Waive rent — switching it back off restores the original amounts. Restocking
and cleaning can both run automatically.

### Employees
Keep everyone fully satisfied, clear absences and sick days, max out every skill.

### Vehicles
Disable damage and fuel use. Repair, refuel and clean everything you own, or just the car
you are in. Clear parking tickets and fines.

**Tuning** applies to one car at a time, and every slider is a **percentage of what that
car itself came with**. 100% is stock, 150% is half again, 50% is half. Set what you want
and press **Apply tuning to the car you are in** — nothing changes until you press it.

Percentages rather than raw figures because raw figures cannot mean anything across
vehicles: one car has 45 engine power and 2500 brake force, so a number that looks sensible
for one is thirty times another. Asking for far more power than a car has does not make it
fast — the wheels spin and it goes nowhere.

The baseline is what the car had before this mod first touched it, so applying 150% twice
still gives 150% of stock, not 225%.

Press **Show this car's current tuning** to see the car's actual figures on screen.

**Tuning sticks to the car.** The game's save has no room for it — a saved vehicle records
its position, colour and cargo, nothing about its engine — so the trainer keeps each car's
percentages itself and puts them back when you get in. Get into a different car and the
sliders follow it, showing that car's tuning rather than the last one's.

**Undo tuning on the car you are in** returns it to stock, sets the sliders back to 100%,
and forgets the saved tuning for that car so it does not come back later.

The sliders redraw as soon as any of this happens, so they always describe the car you are
in rather than what you last typed. The Options → MODS copy catches up when you next open
it, since that panel is the game's own.

Turning off **Apply my settings when the game loads** also stops saved tuning being
reapplied; the sliders still show what a car is set to.

Raising max speed only lifts the ceiling; whether a car reaches it depends on its power. And
power well past 100% mostly spins the wheels — a car that looks fast at the tyres while
going nowhere has too much of it, not too little.

Steering angle is how far the wheels turn, not a turning circle. Well above 100% a car
handles like nothing on earth, which is either the point or a mistake.

These map to the car's real speed limiter, engine and brake modules, so wildly mismatched
values — a lot of power against weak brakes, or a very high speed limit — give the physics
solver more than it can settle and the car judders. Keep them roughly in proportion, and
lower them again if a car starts behaving badly.

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
building's entrance, with its physics reset properly, and anything parked in the way is
cleared as the game itself does when it places a vehicle.

Not every quest marker sits on a road; some point at an object indoors. If there is no
ground for the car where you are going, nothing is moved and the trainer says so — get out
and teleport on foot instead.

**Waypoints** let you save the spot you are standing on under a name, then travel back to it
or delete it later. Saving does not work inside a building.

### Utility
*(phone only)* Start typing an item name and pick from the suggestions — search matches the
readable name, so "bread" finds it without knowing its id is `ba:itemname_bread`. The item
appears in your hands, so you need empty hands, no vehicle and no placement mode.

**Apply my settings when the game loads** is on by default, and is what makes a saved
setting active at startup. Turned off, every control resets to its default on each load —
a real clean start, rather than settings that look on while doing nothing. Your settings
apply each time a save loads, not once per launch, so leaving to the menu and coming back
applies them again.

**Reset all settings** returns every control to its default. It does not undo cheats already
applied to your save.

## Known issues

Everything here has been used on a real save, but only by one person on one machine — please
report anything odd.

- **Buttons are captioned "Apply".** The game's mod UI never sets a caption on mod buttons,
  leaving a placeholder that this mod overwrites. The label to the left says what each does.
- **F2 bug reporting stops working, and stays off.** This is the game's own doing, not the
  trainer's: it refuses bug reports from a modded save. The save also records that mods
  were used at all, so removing the mod does not bring F2 back — only an earlier save from
  before mods, or a new game, will. Report game bugs from an unmodded save.
- **Extreme car tuning makes the car judder.** See the note under Vehicles: those sliders
  drive the real physics modules, and far-out values are more than the solver can settle.
- **Steam achievements lag behind.** Goals you had not genuinely earned unlock only after you
  reload the save; the game grants those during its own check on load.
- **Freezing the clock is experimental.** It is the most invasive feature here and may
  interact badly with deliveries, shifts or rent. Try it on a save you do not mind losing.
- **Some vehicles have no speed limiter or damage module**, and the game says so when tuning
  them. That is normal.
- **Pay before teleporting out of a shop.** Teleporting leaves the building properly, but
  it skips the exit check that normally stops you leaving with unpaid goods. Anything you
  have not paid for is still the shop's, so it does not come with you. Paid items, held
  ones included, travel fine.
- **The phone app relies on game internals** a future patch could rename. If the Trainer app
  stops appearing, everything is still under Options → MODS.

## Translating

Copy `Locales/en.json` to `Locales/<locale>.json` and translate the values, leaving the keys
and any `{value}` placeholders alone. The game loads it automatically. Name the file the way
the game names its own, in `Big Ambitions_Data/StreamingAssets/locale` — lowercase, like
`zh-cn.json`.

Simplified Chinese is included, contributed by [cod919](https://github.com/cod919).
Corrections and further languages are welcome.

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
