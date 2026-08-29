# Big Ambitions — Trainer Mod: Research Notes

Confirmed findings from decompilation. **Nothing here is guessed — all of it comes from
`ilspycmd` output against the shipped game assemblies.** Update this file as we learn more.

## Environment

| Thing | Value |
|---|---|
| Game path | `E:\SteamLibrary\steamapps\common\Big Ambitions\` |
| Managed assemblies | `<game>\Big Ambitions_Data\Managed\` (229 files, 177 DLLs) |
| Game version | EA 0.11, **Build 3669** (from `Player.log`: `Loaded Big Ambitions (Build 3669)`) |
| Unity runtime | **Mono** — `MonoBleedingEdge\` present, **no `GameAssembly.dll`** |
| persistentDataPath | `C:\Users\<user>\AppData\LocalLow\Hovgaard Games\Big Ambitions\` |
| Local mods folder | `<persistentDataPath>\ModsLocal\` |

The game ships `.pdb` files next to every `BigAmbitions.*.dll`. Not obfuscated —
decompiled output has real type, member, and parameter names.

## Architecture decision: native mod, NOT BepInEx

The game has a **first-class official mod API**. We use it. No BepInEx, no Harmony,
no ImGui, no injection.

### Why not BepInEx

Originally planned, then obsoleted by the ModAPI discovery. Kept for the record:
BepInEx 5 (Mono) would have worked, BepInEx 6 (IL2CPP) never could.

### Why the existing Nexus trainer doesn't load

`com.thrasher.bigambitions.trainer` v3.0.0, from `Plugins-1-3-0-0-1780570985.zip`.
Strings extracted from `BigAmbitionsTrainer.dll`:

```
.NETCoreApp,Version=v6.0
BepInEx.Unity.IL2CPP
Il2CppInterop.Runtime / Il2CppSystem / Il2Cppmscorlib / RegisterTypeInIl2Cpp
D:\Modding\BigAmbitions-DearImGui\obj\Release\net6.0\BigAmbitionsTrainer.pdb
```

It targets .NET 6 and IL2CPP interop. This game is Mono. It is structurally incapable
of loading here — not a config problem.

Its UI is Dear ImGui via BULLETBOT's DearImGuiInjection (34 files: cimgui natives,
Silk.NET D3D11/D3D12/Vulkan/OpenGL, MinHook, ImageSharp).

Game namespaces it references — i.e. its whole feature surface:
`BigAmbitions`, `BigAmbitions.Characters`, `BigAmbitions.Characters.Skills`,
`BigAmbitions.Rivals`. Nothing else.

## The mod API — `BigAmbitions.ModAPI.dll` (namespace `BAModAPI`)

### Entry point

```csharp
[assembly: RegisterModClass(typeof(MyMod))]   // AttributeTargets.Assembly, AllowMultiple

[ModEntryOnCityLoad]                          // scope attribute — required
public sealed class MyMod : ModBigAmbitionsBase
{
    public override Task OnLoadAsync(ModContext context) { ... }
    public override Task OnUnloadAsync() { ... }
}
```

`ModBigAmbitionsBase` implements `IModBigAmbitions`:
- `string[] RelativeAssetBundlePaths` (virtual, defaults empty)
- `Task OnLoadAsync(ModContext)`
- `Task OnUnloadAsync()`

`ModContext` → `ModRootPath`, `ModId`, `Logger` (`IModLogger`: Info/Warn/Error).

### Activation scopes

Exactly one scope attribute per entry class (`ModDiscoveryRegistry.ScopeAttributeMappings`):

| Attribute | `ModActivationScope` |
|---|---|
| `ModEntryOnInitializationLoadAttribute` | `Initialization` |
| `ModEntryMainMenuAttribute` | `MainMenu` |
| `ModEntryOnCityLoadAttribute` | `City` |
| `ModEntryOnIntroLoadAttribute` | `Intro` |
| `ModEntryOnBlueprintCreatorLoadAttribute` | `BlueprintCreator` |

`ModLifecycleLoader.LifetimeScope == ModActivationScope.Initialization`.

A class with no recognized scope attribute is rejected:
`"...does not have any recognized mod entry scope attribute."`

### Options UI — the game renders it for us

`BigAmbitions.Mods.OptionsService.Register(string modId, ModOptions options)`.
Fluent builder, each method returns `ModOptions`:

```csharp
new ModOptions()
    .AddHeader("Economy")
    .AddSplitter()
    .AddToggle(id, label, defaultValue, Action<bool> onValueChanged)
    .AddSlider(id, label, int min, int max, int defaultValue, Action<int> onValueChanged, string valueLabelKey)
    .AddDropdown(id, label, string[] choiceKeys, int defaultIndex, Action<int> onValueChanged)
    .AddButton(label, Action onClick)
    .AddCustom(ModOption)
```

- Options implementing `IPersistableOption` (Toggle, Slider, Dropdown) **persist by `Id`**.
  Duplicate or empty ids are logged as errors and won't persist.
- `ButtonOption`, `HeaderOption`, `SplitterOption` are not persisted (id is `null`).
- **Sliders are `int` only.** No float, no text input.
- `OptionsService.OnChanged` / `OnReset` events; `ResetAllToDefaults()`;
  `RemoveModOptions(modId)`.
- `ModOption.SpawnUi(Transform parent, string modId)` is `virtual` — subclass +
  `AddCustom` to build arbitrary UI (e.g. a money text field). Base impl logs an error.

Rendered by `ModOptionsViewController : MonoBehaviour` in `ModsInternal`.

### Other API surface

- `BAModAPI.Services.AssetService` — AssetBundle loading, shader registry.
- `ServiceHelper`, `UnityLifecycleProvider : MonoBehaviour`.
- `ModEvents.onModsLoaded` / `onModsUnloaded` (public `Action` fields).
- `ModEnumHash.GetSafeHash(string)` — SHA256-derived int for mod-defined enum values,
  avoids the reserved range 0..2000. Used with `ModEnumDefinitions` to add new enum
  members (business types, item types) without colliding with the base game.

## Mod loading — `BigAmbitions.ModsInternal.dll`

### Local mod install format

`ModDiscoveryRegistry.ModsLocalPath => Path.Combine(Application.persistentDataPath, "ModsLocal")`

From `GetLocalMods()` / `IsModFolder()` / `TryGetRootDllPath()`:

- Each mod is **one subfolder** directly under `ModsLocal\`.
- That folder must contain **exactly one `.dll`** at the top level.
  Zero → `"No DLL file was found in the mod root folder."`
  Two or more → `"Multiple DLL files were found... Expected exactly one mod DLL."`
- **The folder name is the mod's display title.** No manifest file needed locally.
- Mod key/id for a local mod is the full path of the mod folder.

So install = drop `ModsLocal\<Mod Name>\<Mod>.dll`. That's the whole install story.

### Hot reload

`FileSystemWatcher` on both `ModsLocal` and the Steam Workshop path; re-discovery is
triggered on application focus change (`Application.focusChanged`). Alt-tabbing out and
back may pick up a rebuilt DLL without restarting the game — worth testing.

### Compatibility validation

`ModCompatibilityValidator.TryValidateGameAssemblyCompatibility` checks references whose
simple name starts with `BAModAPI` or `BigAmbitions`, and compares **major version only**:

> `{name} requires major {X} but game is {Y}`

Only a major-version bump breaks us. Minor/patch drift is fine.
A second pass validates inter-mod dependencies the same way, with deferred load ordering
and circular-dependency detection.

### Steam Workshop

`SteamModUploader`, `SteamModDownloader`, `SteamModLoadingService`, `SteamModMetadataHandler`.
Manifest of subscribed ids: `<persistentDataPath>\steam_mod_manifest.txt`, one ulong per line.
`ModMetadata` (`[Serializable]`) → `int targetBuildNumber`, `int modVersion`.
`ModInfo` → `steamItemId`, `modFolder`, `thumbnailUrl`, `thumbnail`, `title`,
`description`, `targetBuildNumber`, `modVersion`, `changeLog`.

Publishing to Workshop is supported from inside the game.

## `BigAmbitions.DebugMode.dll` — dead end

107 lines total. Contains only:
- `BigAmbitions.DebugMode.DebuggableSystem` — abstract, one `protected bool SetDebugMode(DebugMode)`
- `enum DebugMode { None, PlayMode, DebugMode }`
- `BigAmbitions.Factories.OnGuiDummy : MonoBehaviour` — `SetOnGui(Action)`, calls it from `OnGUI()`

No dev cheat menu. **But `OnGuiDummy` is useful**: a public, shipped MonoBehaviour that
runs an arbitrary `Action` inside Unity's `OnGUI`. If we ever need a custom overlay
beyond the ModAPI options panel, that's a free IMGUI hook.

## Cheat targets — `GameInstance` (the save state)

**`SaveGameManager.Current`** (static, in `BigAmbitions.dll`) returns a **`GameInstance`**.
Both types are in the **global namespace** — no `using` needed, just reference
`BigAmbitions.dll`.

Every member is a **plain public mutable field**. No properties, no setters, no
encapsulation, so a trainer just assigns to them. The game does this itself, e.g.
`SaveGameManager.Current.Money += CompensationMoney;`. **No Harmony patching required
for any of this.**

```csharp
public class GameInstance
{
    public int Day; public int Hour; public float Minute;
    public float Money;
    public float Energy; public float Hunger; public float Happiness;
    public float EnergyGeneratedFromConsumables;
    public float NetWorth;
    public List<Loan> Loans;
    public List<EmployeeInstance> EmployeeInstances;
    public List<EmployeeInstance> CandidateEmployeeInstances;
    public List<VehicleInstance> VehicleInstances;
    public List<NeighbourhoodStats> NeighbourhoodStats;
    public List<BuildingRegistration> BuildingRegistrations;
    public List<Contact> Contacts;
    public Queue<Transaction> Transactions;
    public List<TaxDeductibleExpense> currentTaxPeriodDeductibleExpenses;
    public float CurrentTaxPeriodGamblingWinnings;
    public float CurrentTaxPeriodGamblingLosses;
    public List<string> CompletedQuestEntries;
    public string ActiveVehicleId;
    public SerializableVector3 LastPlayerPosition;
    // ...
}
```

`SaveGameManager.Current` is **null outside an active save** — null-check every access.
`SaveGameManager.CurrentDay` / `CurrentHour` are convenience statics that already do.

### `GameInstance.gameVariables` — the sandbox config, live and mutable

`SaveGameManager.Current.gameVariables` is a **`GameVariables`**, the game's own
difficulty/custom-game settings object, editable at runtime. This is the single richest
cheat surface in the game and needs no patching at all.

```csharp
public class GameVariables
{
    public Difficulty difficulty = Difficulty.Normal;
    public int startingAge = 18;
    public bool disableAging;
    public bool disableEnergy;
    public bool disableHappiness;
    public bool allCoursesUnlocked;
    public int startingMoney = 4200;
    public int taxPercentage = 10;
    public int daysPerYear = 60;
    public float marketPriceMultiplier = 1f;
    public float employeeHourlySalaryMultiplier = 1f;
    public float bankInterestMultiplier = 1f;
    public bool tutorialEnabled = true;
    public float rivalsDifficultyMultiplier = 1f;
    public bool disableVehicleDamage;
    public bool disableVehicleFuel;
    public bool allContactsUnlocked;
    public float baseCustomerPromotionMultiplier = 0.5f;
    public float wholesaleUrgentFeeMultiplier = 0.2f;
    public float importerUrgentFeeMultiplier = 0.75f;
    public bool disableWholesaleAndImportLimits;
    public bool allProductsAvailableFromImporters;
    public float exportMultiplier = 0.65f;
    public float sellingMultiplier = 0.75f;
}
```

The defaults above are the game's own, and are what the sliders should reset to.

### `GameInstance.modData`

`public Dictionary<string, string> modData` — a **per-save** key/value store the game
serialises for mods. Worth using for anything that should follow the save rather than the
install, since `PlayerPrefs` option values are keyed by the mod's folder path.

### Type and namespace map

Everything below is in `BigAmbitions.dll` unless noted. Namespaces are not obvious, and
`ilspycmd` output only shows them via the enclosing `namespace` block:

| Type | Namespace |
|---|---|
| `SaveGameManager`, `GameInstance`, `GameVariables`, `Loan`, `Skill` | *(global)* |
| `EmployeeInstance` | `Entities` |
| `VehicleInstance` | *(global)* |
| `VehicleType`, `VehicleTypeHelper` | `Vehicles.VehicleTypes` |
| `RivalState`, `SpecialRivalState`, `RivalData` | `BigAmbitions.Rivals` |
| `Timestamp` | *(global)*, in **`DayNightCycle.dll`** |
| `TaggedScriptableObject` (base of `VehicleType`) | *(global)*, in **`HGPlugins.dll`** |
| `TextLocalizationComponent`, `LocalizorManager` | in **`HGPlugins.dll`** |

Touching vehicles therefore drags in `DayNightCycle.dll` (via `parkingTickets`) and
`HGPlugins.dll` (via `VehicleType`'s base class), even though neither is used directly.

### Other per-entity fields worth cheating

`EmployeeInstance` (`Entities`): `satisfaction` (clamped `0..100`), `hourlyWage`,
`isAbsent`, `nextSickDay`, `hasSendQuitWarning`, `skills`, `workedHoursToday`,
`workedHoursThisWeek`, `poached`, `isReplaced`, `dayHired`.

`VehicleInstance`: `fuel`, `damage`, `dirtiness`, `deformations`, `unpaidParkingAmount`,
`parkingTickets`, `parkingState`, `cargoInstances`. **Fuel capacity is per type** —
`VehicleTypeHelper.GetVehicleType(vehicle.vehicleTypeName).maxFuel`, and
`IsMotorVehicle => maxFuel > 0f` so bicycles must be skipped.

`SpecialRivalState` (`BigAmbitions.Rivals`): `isActive`, `isDefeated`,
`completedTimelineEntryIds`, `defenseStates`. Stored on
`GameInstance.specialRivalStates`; `GameInstance.rivalStates` holds the plain
`RivalState` history entries.

`BuildingRegistration`: `RentPerDay`, `RentedByPlayer`, `AvailableForRent`,
`temporarilyClosed`, `retailPrices`, `itemInstances`, `dirtSpots`, `orderHistory`.

### Stat ranges

From `EnergySettings` (ScriptableObject):

| Field | Value |
|---|---|
| `maxEnergyHungerHappinessValue` | `100f` |
| `minEnergyHungerHappinessValue` | `0f` |
| `hospitalizationEnergyThreshold` | `-20f` |
| `maxDailyEnergyGeneratedFromConsumables` | `30f` |

So Energy / Hunger / Happiness are normalised **0..100**. Note **higher Hunger is better**
— `maxEnergyBurnIncreaseAtZeroHunger = 0.5f` means energy drains faster at *zero* hunger,
so "full" is 100, not 0.

### Per-frame tick

`BAModAPI.Services.UnityLifecycleProvider` exposes static events **`OnUpdate`**,
`OnFixedUpdate`, `OnLateUpdate`. Subscribe in `OnLoadAsync`, unsubscribe in
`OnUnloadAsync`. This is how "keep X full" toggles enforce themselves without Harmony.

`ServiceHelper.RunOnMainThreadAsync<T>` marshals onto the Unity main thread if ever needed.

## Feature gap vs the existing trainer

Untouched by `com.thrasher.bigambitions.trainer`, all present as game assemblies:

| Assembly | Trainer opportunity |
|---|---|
| `BigAmbitions.Factories` | production speed, instant output, raw materials — reworked in EA 0.10 |
| `BigAmbitions.Items` | inventory, instant restock, stock quality |
| `BigAmbitions.Neighborhoods` | unlock districts, foot traffic, popularity |
| `BigAmbitions.PlacementSystem` | place anywhere, ignore collision/build rules |
| `BigAmbitions.InteriorDesigner` | unlock/free furniture, ignore room requirements |
| `BigAmbitions.Seasons` | force season, seasonal demand |
| `BigAmbitions.AI` | customer spawn rate, traffic multipliers |

**Not yet decompiled.** These are targets to investigate, not confirmed capabilities.

## Verified working (Build 3669)

Load path proven end to end with the no-op test mod:

```
[Mod:Bigamst Trainer] Bigamst Trainer loading. ModId='C:\...\ModsLocal\Bigamst Trainer' Root='C:/...\ModsLocal\Bigamst Trainer'
[Mod:Bigamst Trainer] Bigamst Trainer loaded.
[Mod:Bigamst Trainer] Test slider -> 63
[Mod:Bigamst Trainer] Test toggle -> True
```

Confirmed behaviour:

- **The options panel is in the in-game Options/Settings menu**, and it renders registered
  mod options there. Reached while a city is loaded.
- `ModId` for a local mod is the **full path of the mod folder**, as `GetModKey` implies.
  Since `PlayerPrefs` keys are `m:{modId}:{optionId}`, **renaming or moving the mod folder
  discards every saved option value.** Needs handling before release.
- Slider `onValueChanged` fires continuously while dragging, not just on release. Anything
  expensive in that callback must be debounced.
- `[ModEntryOnCityLoad]` activates once a save is loaded — correct scope for a trainer.

### Game bug: mod buttons always read "YOUR TEXT HERE"

`ModOptionsButtonControl.Initialize` assigns our `Label` to the **row label** and never
touches the button's own text:

```csharp
ButtonOption buttonOption = (ButtonOption)option;
label.Key = buttonOption.Label;        // row label — ours
button.onClick.RemoveAllListeners();   // button text is never set
```

So every mod button in the game keeps its prefab placeholder. **Nothing we pass through
`AddButton` can change it.** This affects all mods, not just ours.

Workarounds:
- Put the whole instruction in the label and suffix an arrow ("Pay off all loans  →"),
  so the row reads sensibly next to the meaningless button.
- Prefer toggles and sliders, which render correctly.
- A custom `ModOption` with a `SpawnUi` override could build a proper button, at the cost
  of hand-building Unity UI.

Worth reporting upstream at `github.com/hovgaardgames/bigambitions` — it's a one-line fix
on their side.

### Slider value labels work via the localization fallback

`SliderOption.ValueLabelKey` is only shown when non-empty
(`valueLabel.gameObject.SetActive(showValueLabel)`), and it is passed
`Arguments = new { value = num }`.

A missing key falls back to the raw string (`LocalizorManager.GetLocalization` returns
`label` when `Mode == 0`, else the lowercased `text`), and arguments are applied as a
literal `stringBuilder.Replace("{" + key + "}", value)`. So substitution happens **even
for keys that don't exist**:

- `ValueLabelKey: "${value}M"` renders `$5M`
- `ValueLabelKey: "{value}:00"` renders `14:00`

Casing of labels is preserved in practice, so `Mode == 0` on this build. Don't rely on
that for anything load-bearing.

### Sliders, toggles and dropdowns re-fire their callback on every panel build

`ModOptionsSliderControl.Initialize` ends with `data.OnValueChanged?.Invoke(num)`, and the
toggle and dropdown controls do the same. `ModOptionsViewController.Rebuild` runs on every
`OnEnable`, so **opening the Options menu re-invokes every callback with its stored value**.

That is correct for settings that should be re-applied (tax rate, multipliers) but wrong
for one-shot actions. A "jump to hour" slider moved the clock every time the panel opened.

**Rule: a slider callback must only record state.** Any action goes behind a button.

### Restocking: use the game's own capacity rule

Stock lives at `BuildingRegistration.itemInstances` → `ItemInstance.cargoInstances` →
`CargoInstance { itemName, amount, pricePerUnit, paid }`.

Capacity is **not** a constant. Call `CargoInstance.GetMaxStockCapacity(ItemInstance holder)`,
and mirror the game's nested-cargo special case from `ItemInstance.TryAddCargo`:

```csharp
int capacity = cargo.nestedCargoInstances.Count > 0 ? 1 : cargo.GetMaxStockCapacity(holder);
```

`GetMaxStockCapacity` dereferences `ItemCached` on both the cargo and the holder, and
`ItemCached` is null whenever `ItemsGetter.GetByName` cannot resolve the name — so null-check
both and wrap the call, or one bad item aborts the whole sweep. Setting `paid = true` makes
the restock free.

Filter properties on `BuildingRegistration.RentedByPlayer`; the list also holds world
scenery and rivals' buildings.

### `EmployeeInstance.skills` is obsolete — use `characterData.skills`

```csharp
[Obsolete] public List<Skill> skills;          // still present, still compiles, not live
```

The live list is `EmployeeInstance.characterData.skills` (`CharacterData.skills`).
`Skill { string name; float value; }`, capped at `100f` per `EmployeeInstance`'s own private
`MaxSkillValue`. Writing to the obsolete list silently does nothing.

Also `[Obsolete]` on `EmployeeInstance`: `hired`, `declined`, `assignedHRManager`, `presetId`.

### Button placeholder text is `"Your text here"`, sentence case

The panel renders it in caps through TMP font styling, so matching on the visible
`"YOUR TEXT HERE"` never fires. Compare case-insensitively, and **only** rewrite captions
that still hold the placeholder — other buttons in the same panel carry real captions like
`"Reset windows"` and must be left alone.

The caption is driven by a `TextLocalizationComponent`
(`Localizor.LanguageChangeEvent`, in `HGPlugins.dll`), which overwrites the text on its next
refresh — set its `Key` and disable it as well as assigning `.text`.

### Never poll the scene from the update tick

`FindObjectsOfType` / `FindObjectOfType` walk the **entire scene graph**. Calling either on
a timer from `UnityLifecycleProvider.OnUpdate` caused clearly noticeable stutter in the
city — confirmed by removing the mod folder and comparing.

Making the call cheaper is not the fix; the fix is not calling it. `ModOption.SpawnUi` is
invoked during `ModOptionsViewController.Rebuild`, so a custom `ModOption` registered
**last** in the list is a free notification that the panel was just built, at which point
every button above it exists:

```csharp
internal sealed class HookOption : ModOption
{
    internal HookOption() : base(null, string.Empty) { }
    public override void SpawnUi(Transform parent, string modId) => _pendingRoot = parent;
}
```

The per-frame cost then drops to two field reads, and UI work is scoped to the panel's
content root instead of the scene. Anything else needing "run when the options panel
opens" should use the same trick.

### Mods can ship localization — `<modFolder>\Locales\<locale>.json`

`ModDiscoveryRegistry.SyncDiscoveredLocalizationPaths` registers `Path.Combine(ModFolder,
"Locales")` for every discovered mod and hands it to
`LocalizorManager.SyncExternalLocalizationPaths`, which loads `<locale>.json`
(`Path.Combine(value, text + ".json")`) and merges it into the localization tables.

This is the correct fix for missing-key warnings: **provide the keys** rather than
suppressing warnings via `LocalizorManager.showNonCriticalWarnings`, which is global and
would hide the game's own diagnostics. It also makes the mod translatable for free.

Keys are matched **lowercased** (`GetLocalization` does `label.ToLower()`), so the JSON keys
must be lowercase. The file is a flat `{"key": "value"}` object, and `{value}` placeholders
must survive into the value for slider argument substitution.

The single-DLL rule only inspects the mod root (`SearchOption.TopDirectoryOnly`), so a
`Locales` subfolder is fine.

**Getting the key list right:** don't transcribe labels by hand. Run once and harvest them
from the log, which prints exactly what was looked up, already lowercased:

```bash
grep -ohE "Localization for key '[^']+' not found" Player.log \
  | sed -E "s/Localization for key '([^']+)' not found/\1/" | sort -u
```

### Local mods cannot be disabled from the Mods screen

`SubscribedModUI.UpdateConflicts`:

```csharp
bool flag = _modInfo.steamItemId != 0;                    // Workshop only
modEnabledToggle.isOn = !flag || ModManifest.Contains(currentSteamItemId);
modEnabledToggle.interactable = flag && !_hasConflicts;   // local mods: not interactable
```

Enable/disable state lives in `ModManifest`, a list of **ulong Workshop ids**. A local mod
has no id, so the toggle is greyed out and always displays as on, and the version field
reads `"main_menu_mods_local_mod"` instead of a version.

**Uninstalling a local mod means deleting its folder.** Say so in user-facing docs, because
the obvious in-game control looks broken otherwise. Publishing to Steam Workshop gives users
a working toggle — a real argument for Workshop as the primary channel.

Note also that detected mod conflicts (`ModEnumDefinitions.GetModConflictsList`) force the
mod off and remove it from the manifest.

### Option labels are localization keys

```
Localization for key 'test toggle' not found on gameobject 'Label'
```

Labels are lowercased and looked up in the game's localization table; a miss falls back to
displaying the raw string, so plain English labels *look* right but log a warning every
time the panel builds. This is also what `SliderOption.ValueLabelKey` is for. Cosmetic,
but fix before release rather than spamming users' logs.

### Assemblies are cached for the session

Mono cannot unload an assembly from the AppDomain. The `FileSystemWatcher` re-discovery on
focus change will **not** pick up a rebuilt DLL if a previous version already loaded this
session — it silently keeps the old one. **Fully quit and relaunch the game after every
build.** A stale error in `Player.log` after a fix usually means this, not a failed fix.

## Target framework: `net472`, NOT `netstandard2.0`

Learned the hard way. **There is no `netstandard.dll` in `Big Ambitions_Data\Managed\`** —
the game uses Unity's ".NET Framework" API compatibility level, not .NET Standard.

A `netstandard2.0` build is rejected before any of our code runs. From `Player.log`:

```
[ModDiscovery] Failed to read registered mod classes from 'BigamstTrainer.dll'.
FileNotFoundException: Could not load file or assembly
  'netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
  at System.MonoCustomAttrs.GetCustomAttributesInternal(...)
  at BigAmbitions.ModsInternal.ModDiscoveryRegistry+<GetRegisteredModTypes>d__45.MoveNext()
```

The loader calls `GetCustomAttributes<RegisterModClassAttribute>()` on our assembly to find
the entry class. Resolving those attributes needs the `netstandard` facade, which isn't
there, so discovery throws before reading a single type. A follow-on
`TypeLoadException: VTable setup of type ... failed` appears later from the in-game debug
console scanning the same broken assembly — that one is a symptom, not a separate bug.

Fix: `<TargetFramework>net472</TargetFramework>` plus the
`Microsoft.NETFramework.ReferenceAssemblies` package (build-time only, `PrivateAssets=all`)
so no .NET Framework targeting pack needs installing. The output then binds straight to
`mscorlib`, which the game does have.

Verify after any csproj change — the emitted assembly must reference `mscorlib`, never
`netstandard`:

```bash
tr -c '[:print:]' '\n' < bin/Release/BigamstTrainer.dll | grep -E '^(netstandard|mscorlib)' | sort -u
```

## Rules for this project

1. Never invent a class, field, or method name. Every target comes from decompiler output.
2. Native ModAPI only. No BepInEx, no Harmony, no IL2CPP anything.
3. `net472` target — see above. The SDK version we build with is irrelevant to the runtime.
4. Exactly one DLL ships in the mod folder. The loader rejects more than one.
5. All game assembly references must be `Private="false"` so they don't get copied.
