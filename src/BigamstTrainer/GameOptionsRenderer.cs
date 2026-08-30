using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Mods;
using BigAmbitions.ModsInternal;
using UnityEngine;

namespace BigamstTrainer
{
    /// <summary>
    /// Renders a <see cref="ModOptions"/> list using the game's own option controls.
    ///
    /// ModOptionsViewController holds the header, slider, toggle, dropdown, button and
    /// splitter prefabs as private SerializeFields. Borrowing them means the phone panel
    /// looks exactly like the Options menu and gets sliders, toggles and dropdowns —
    /// along with the persistence those controls already implement via ModOptionPrefs —
    /// instead of hand-drawn buttons that only approximate the style.
    ///
    /// This mirrors ModOptionsViewController.SpawnOption, which is private.
    /// </summary>
    internal static class GameOptionsRenderer
    {
        private static readonly string[] PrefabFields =
        {
            "modOptionsHeaderPrefab",
            "modOptionsSliderPrefab",
            "modOptionsTogglePrefab",
            "modOptionsDropdownPrefab",
            "modOptionsButtonPrefab",
            "modOptionsSplitterPrefab",
        };

        private static Dictionary<string, GameObject> _prefabs;
        private static bool _searched;

        internal static void Forget()
        {
            _prefabs = null;
            _searched = false;
        }

        /// <summary>True when the game's controls are available to render with.</summary>
        internal static bool PrefabsAvailable => ResolvePrefabs() != null;

        /// <summary>
        /// Finds the prefabs once. FindObjectsOfTypeAll is used deliberately: the options
        /// view is inactive whenever the Options menu is closed, so an active-only search
        /// finds nothing during normal play. It runs at most once per session.
        /// </summary>
        private static Dictionary<string, GameObject> ResolvePrefabs()
        {
            if (_searched)
            {
                return _prefabs;
            }

            _searched = true;

            try
            {
                ModOptionsViewController[] views =
                    Resources.FindObjectsOfTypeAll<ModOptionsViewController>();
                if (views == null || views.Length == 0)
                {
                    return null;
                }

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                foreach (ModOptionsViewController view in views)
                {
                    var found = new Dictionary<string, GameObject>();
                    foreach (string name in PrefabFields)
                    {
                        FieldInfo field = typeof(ModOptionsViewController)
                            .GetField(name, flags);
                        if (field?.GetValue(view) is GameObject prefab)
                        {
                            found[name] = prefab;
                        }
                    }

                    // A view that has not been initialised yet may hold nulls; keep looking.
                    if (found.Count == PrefabFields.Length)
                    {
                        _prefabs = found;
                        return _prefabs;
                    }
                }
            }
            catch (Exception)
            {
                // Reflection over private fields is inherently fragile across patches.
                // Callers fall back to the hand-built UI.
                _prefabs = null;
            }

            return _prefabs;
        }

        /// <summary>Mirrors ModOptionsViewController.ResolvePrefab.</summary>
        private static GameObject PrefabFor(ModOption option, Dictionary<string, GameObject> prefabs)
        {
            switch (option)
            {
                case HeaderOption _:   return prefabs["modOptionsHeaderPrefab"];
                case SliderOption _:   return prefabs["modOptionsSliderPrefab"];
                case ToggleOption _:   return prefabs["modOptionsTogglePrefab"];
                case DropdownOption _: return prefabs["modOptionsDropdownPrefab"];
                case ButtonOption _:   return prefabs["modOptionsButtonPrefab"];
                case SplitterOption _: return prefabs["modOptionsSplitterPrefab"];
                default:               return null;
            }
        }

        /// <summary>
        /// Spawns every option into <paramref name="parent"/>. Returns how many controls
        /// were created, or -1 if the game's prefabs could not be found.
        /// </summary>
        internal static int Render(Transform parent, ModOptions options, string modId)
        {
            Dictionary<string, GameObject> prefabs = ResolvePrefabs();
            if (prefabs == null || options == null)
            {
                return -1;
            }

            int created = 0;
            foreach (ModOption option in options.Options)
            {
                if (option is InlineUiOption inline)
                {
                    // Phone-only controls, positioned by where they sit in the list.
                    try
                    {
                        inline.BuildForPhone?.Invoke(parent);
                        created++;
                    }
                    catch (Exception)
                    {
                        // A custom row must not abort the rest of the panel.
                    }

                    continue;
                }

                GameObject prefab = PrefabFor(option, prefabs);
                if (prefab == null)
                {
                    // Other custom options (the rebuild hook) render nothing.
                    continue;
                }

                try
                {
                    GameObject spawned = UnityEngine.Object.Instantiate(prefab, parent);
                    spawned.SetActive(true);

                    if (spawned.TryGetComponent(out IModOptionsControl control))
                    {
                        // Initialize wires the label, current value and callbacks, and
                        // loads any persisted value through ModOptionPrefs.
                        control.Initialize(option);
                        created++;
                    }
                }
                catch (Exception)
                {
                    // One bad control must not abort the panel.
                }
            }

            return created;
        }
    }
}
