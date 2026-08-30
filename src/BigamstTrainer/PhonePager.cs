using System;
using System.Collections.Generic;
using BAModAPI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigamstTrainer
{
    /// <summary>
    /// Pages the BizPhone widget's app grid.
    ///
    /// The widget has no paging of its own — it lays out however many apps exist and the
    /// overflow is simply unreachable. Adding a Trainer icon makes nine, so without this
    /// the last app would be lost. Controls only appear when there is more than one page,
    /// so a vanilla eight-app phone is untouched.
    /// </summary>
    internal static class PhonePager
    {
        /// <summary>Cells in the widget's 2x4 grid.</summary>
        private const int GridCells = 8;

        /// <summary>Apps per page: the final cell is the page control.</summary>
        private const int PageSize = GridCells - 1;

        private const string ControlsName = "BigamstTrainerPager";

        private static readonly Color DotActive = new Color(0.92f, 0.94f, 0.96f, 1f);
        private static readonly Color DotIdle = new Color(0.92f, 0.94f, 0.96f, 0.35f);

        private static IModLogger _log;
        private static readonly List<GameObject> Apps = new List<GameObject>();
        private static readonly List<TMP_Text> _navLabels = new List<TMP_Text>();
        private static GameObject _controls;
        private static int _page;

        internal static void Reset()
        {
            if (_controls != null)
            {
                UnityEngine.Object.Destroy(_controls);
            }

            _controls = null;
            _page = 0;
            Apps.Clear();
            _navLabels.Clear();
            _log = null;
        }

        /// <summary>
        /// Takes over the grid under <paramref name="container"/>. Call after every icon
        /// has been added, since the page count is fixed from what is there.
        /// </summary>
        internal static void Install(Transform container, IModLogger log)
        {
            _log = log;

            try
            {
                Apps.Clear();
                _navLabels.Clear();

                var skipped = new List<string>();
                foreach (Transform child in container)
                {
                    // Skip anything this pager created, so a reinstall cannot swallow its
                    // own controls as if they were apps.
                    if (child.name == ControlsName)
                    {
                        continue;
                    }

                    // The grid is built from a template object that ResetTemplate leaves
                    // disabled, and it sits in here alongside the real icons. Paging must
                    // not adopt it — activating it puts a placeholder "App Title" button
                    // on the phone. Anything already switched off is not a real app.
                    if (!child.gameObject.activeSelf)
                    {
                        skipped.Add(child.name);
                        continue;
                    }

                    Apps.Add(child.gameObject);
                }

                _log?.Info($"BizPhone grid: {Apps.Count} app(s)" +
                           (skipped.Count > 0 ? $", skipped {string.Join(", ", skipped)}" : string.Empty) +
                           $"; controls parented to '{container.parent?.name}'.");

                // Requested order change. Done on the collected list and then pushed to
                // sibling order, because the grid fills cells in sibling order.
                Swap("VoogleMaps", "EconoView");
                for (int i = 0; i < Apps.Count; i++)
                {
                    Apps[i].transform.SetSiblingIndex(i);
                }

                if (Apps.Count <= PageSize)
                {
                    // Everything fits; leave the widget exactly as the game built it.
                    return;
                }

                BuildNavTile(container);
                SetPage(0);

                int pages = (Apps.Count + PageSize - 1) / PageSize;
                _log?.Info($"BizPhone paging enabled: {Apps.Count} apps across {pages} pages.");
            }
            catch (Exception exception)
            {
                _log?.Warn($"Could not add BizPhone paging: {exception.Message}");
            }
        }

        /// <summary>
        /// The page control is a clone of a real app icon occupying the grid's last cell.
        ///
        /// Free-floating arrows anchored to the phone body were invisible — the widget's
        /// visible area is not the rect they were placed against, and it cannot be
        /// measured from here. Sitting in the grid means the game's own layout positions
        /// it, so it is always exactly where an icon would be.
        /// </summary>
        /// <summary>Swaps two apps by GameObject name, if both are present.</summary>
        private static void Swap(string first, string second)
        {
            int a = Apps.FindIndex(x => x != null && x.name == first);
            int b = Apps.FindIndex(x => x != null && x.name == second);
            if (a < 0 || b < 0)
            {
                return;
            }

            GameObject held = Apps[a];
            Apps[a] = Apps[b];
            Apps[b] = held;
        }

        private static void BuildNavTile(Transform container)
        {
            GameObject source = Apps.Count > 0 ? Apps[0] : null;
            if (source == null)
            {
                return;
            }

            _controls = UnityEngine.Object.Instantiate(source, container);
            _controls.name = ControlsName;
            _controls.SetActive(true);

            // Inherited handlers would open whichever app was cloned.
            Button button = _controls.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Step(1));
            }

            var badge = _controls.GetComponent<SmartphoneAppButton>();
            badge?.UpdateBadgeCount(0);

            // Muted and visually distinct: this is navigation, not another app.
            // Left as the cloned icon until a better sprite is chosen.

            _navLabels.Clear();
            foreach (TMP_Text text in _controls.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                var localized = text.GetComponent<Localizor.LanguageChangeEvent.TextLocalizationComponent>();
                if (localized != null)
                {
                    localized.enabled = false;
                }

                _navLabels.Add(text);
            }
        }

        private static void Step(int direction)
        {
            int pages = (Apps.Count + PageSize - 1) / PageSize;
            // Wrap, so both arrows always do something rather than dead-ending.
            SetPage((_page + direction + pages) % pages);
        }

        private static void SetPage(int page)
        {
            _page = page;
            int pages = (Apps.Count + PageSize - 1) / PageSize;

            for (int i = 0; i < Apps.Count; i++)
            {
                if (Apps[i] != null)
                {
                    Apps[i].SetActive(i / PageSize == page);
                }
            }

            if (_controls == null)
            {
                return;
            }

            // The grid fills cells in sibling order among active children, so the control
            // has to be last to land in the final cell.
            _controls.transform.SetAsLastSibling();

            string caption = page < pages - 1 ? "More" : "Back";
            foreach (TMP_Text label in _navLabels)
            {
                if (label != null)
                {
                    label.text = caption;
                }
            }
        }
    }
}
