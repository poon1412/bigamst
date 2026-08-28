# Big Ambitions — Trainer Mod: Research Notes

Confirmed findings from decompilation. **Nothing here is guessed — all of it comes from
`ilspycmd` output against the shipped game assemblies.** Update this file as we learn more.

## Environment

| Thing | Value |
|---|---|
| Game path | `E:\SteamLibrary\steamapps\common\Big Ambitions\` |
| Managed assemblies | `<game>\Big Ambitions_Data\Managed\` (229 files, 177 DLLs) |
| Game version | EA 0.11 (save folder `SaveGames\EA 0.11`) |
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

## Rules for this project

1. Never invent a class, field, or method name. Every target comes from decompiler output.
2. Native ModAPI only. No BepInEx, no Harmony, no IL2CPP anything.
3. `netstandard2.0` target — the SDK version we build with is irrelevant to the runtime.
4. Exactly one DLL ships in the mod folder. The loader rejects more than one.
5. All game assembly references must be `Private="false"` so they don't get copied.
