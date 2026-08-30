using System;
using BAModAPI;
using TMPro;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace BigamstTrainer
{
    /// <summary>
    /// Adds a Trainer entry to the in-game phone, so cheats are reachable during play
    /// instead of only from the Options menu.
    ///
    /// The game's own path — SmartphoneUI.OpenApp to FullMenu.ShowApp to SelectApp —
    /// cannot be used, because SelectApp starts with appName.ToStringFast(), whose switch
    /// throws ArgumentOutOfRangeException for any value the base game doesn't define. So
    /// no modded AppName can travel through it.
    ///
    /// Instead this parents its own panel under FullMenu.appsContainer and drives
    /// activation itself, mirroring what SelectApp does: deactivate every sibling, then
    /// activate ours. Clicking any real app afterwards runs the game's own loop, which
    /// deactivates our panel along with the rest — exactly the behaviour we want, and no
    /// AppName value is ever needed.
    /// </summary>
    internal static class PhoneApp
    {
        private const string PanelName = "BigamstTrainerPanel";
        private const string ButtonName = "BigamstTrainerButton";

        /// <summary>
        /// GameObject names FullMenu gives its real app buttons, from AppName.ToStringFast.
        /// Used to tell a genuine button apart from the disabled prefab template.
        /// </summary>
        private static readonly string[] RealAppNames =
        {
            "Persona", "Contacts", "MyEmployees", "BizMan",
            "EconoView", "MarketInsider", "Rivals", "VoogleMaps",
        };

        /// <summary>The phone UI is not alive the instant a save loads, so installation retries.</summary>
        private const float RetryIntervalSeconds = 1f;
        private const int MaxAttempts = 30;

        private static IModLogger _log;
        private static GameObject _panel;
        private static Transform _appsContainer;
        private static float _retryTimer;
        private static int _attempts;
        private static bool _installed;
        private static bool _givenUp;

        internal static void Initialize(IModLogger log)
        {
            _log = log;
            _panel = null;
            _appsContainer = null;
            _retryTimer = 0f;
            _attempts = 0;
            _installed = false;
            _givenUp = false;
        }

        internal static void Reset()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }

            _appsContainer = null;
            PhoneUi.Forget();
            GameOptionsRenderer.Forget();
            _installed = true; // stop any further attempts
            _log = null;
        }

        /// <summary>
        /// Called every frame. Costs one bool check once installed, or once a second while
        /// still waiting for the phone UI to come up.
        /// </summary>
        internal static void Tick()
        {
            if (_installed || _givenUp)
            {
                return;
            }

            _retryTimer += Time.unscaledDeltaTime;
            if (_retryTimer < RetryIntervalSeconds)
            {
                return;
            }

            _retryTimer = 0f;
            _attempts++;

            if (_attempts == 1)
            {
                _log?.Info("Looking for the phone UI to add the Trainer app.");
            }

            try
            {
                if (TryInstall())
                {
                    _installed = true;
                    _log?.Info("Trainer app added to the phone.");
                }
                else if (_attempts >= MaxAttempts)
                {
                    _givenUp = true;
                    _log?.Warn("Could not find the phone UI; the Trainer app was not added. " +
                               "The Options menu still works.");
                }
            }
            catch (Exception exception)
            {
                // Never let a UI experiment break the rest of the mod.
                _givenUp = true;
                _log?.Error($"Phone app install failed: {exception.Message}");
            }
        }

        private static bool TryInstall()
        {
            FullMenu fullMenu = InstanceBehavior<UIs>.Instance?.fullMenu;
            if (fullMenu == null || fullMenu.appsContainer == null)
            {
                // Report periodically rather than every second, so a genuinely missing
                // phone UI is visible in the log without flooding it.
                if (_attempts % 10 == 0)
                {
                    _log?.Warn($"Attempt {_attempts}: phone UI not ready " +
                               $"(UIs={(InstanceBehavior<UIs>.Instance == null ? "null" : "ok")}, " +
                               $"fullMenu={(fullMenu == null ? "null" : "ok")}).");
                }

                return false;
            }

            // FullMenu.Start names each real button after its app
            // (obj.name = app.appName.ToStringFast()) and builds them from a private
            // template that stays disabled. Cloning the template yields an invisible
            // button, so a real one has to be picked out.
            //
            // "Is it active?" cannot be the test: FullMenu deactivates its whole canvas
            // while the phone is closed, which is exactly when this installs, so every
            // button looks inactive. Match on the known app names instead.
            FullMenuAppButton template = null;
            foreach (FullMenuAppButton candidate in
                     fullMenu.GetComponentsInChildren<FullMenuAppButton>(includeInactive: true))
            {
                if (candidate != null && Array.IndexOf(RealAppNames, candidate.gameObject.name) >= 0)
                {
                    template = candidate;
                    break;
                }
            }

            if (template == null)
            {
                if (_attempts % 10 == 0)
                {
                    int seen = fullMenu
                        .GetComponentsInChildren<FullMenuAppButton>(includeInactive: true).Length;
                    _log?.Warn($"Attempt {_attempts}: no known app button among {seen} candidate(s).");
                }

                // The menu has not built its buttons yet; try again next tick.
                return false;
            }

            _appsContainer = fullMenu.appsContainer;

            // Text with no font asset renders as nothing, so borrow one from
            // the menu before building any label.
            PhoneUi.ResolveFont(fullMenu.gameObject);

            BuildPanel(_appsContainer);
            BuildButton(template);
            return true;
        }

        private static void BuildButton(FullMenuAppButton template)
        {
            GameObject button = UnityEngine.Object.Instantiate(
                template.gameObject, template.transform.parent);
            button.name = ButtonName;

            // Guard against cloning something inactive, and make sure the clone is last
            // in the row rather than inheriting the source's sibling position.
            button.SetActive(true);
            button.transform.SetAsLastSibling();

            _log?.Info($"Cloned app button from '{template.name}' " +
                       $"into '{template.transform.parent?.name}'.");

            // Reuse the cloned app's icon and styling; only the label and action change.
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                // The title is driven by a localization component that would overwrite
                // anything set here on its next refresh.
                var localized = label.GetComponent<Localizor.LanguageChangeEvent.TextLocalizationComponent>();
                if (localized != null)
                {
                    localized.Key = "Trainer";
                    localized.enabled = false;
                }

                label.text = "Trainer";
            }

            Button click = button.GetComponent<Button>();
            if (click != null)
            {
                // Drop the cloned app's handler, which would open that app instead.
                click.onClick.RemoveAllListeners();
                click.onClick.AddListener(Show);
            }
        }

        /// <summary>Typed money amount, with add, subtract and set.</summary>
        internal static void BuildMoneyInput(Transform parent)
        {
            bool built = PhoneUi.CreateInputRow(parent, "Exact amount", "e.g. 250000",
                ("Add", value => ApplyMoney(value, GameplayCheats.ChangeMoney)),
                ("Subtract", value => ApplyMoney(value, amount => GameplayCheats.ChangeMoney(-amount))),
                ("Set", value => ApplyMoney(value, GameplayCheats.SetMoney)));

            if (!built)
            {
                _log?.Warn("No text field could be cloned; the money input was skipped.");
            }
        }

        private static void ApplyMoney(string typed, Action<float> apply)
        {
            if (float.TryParse(typed, out float amount) && amount != 0f)
            {
                apply(amount);
            }
            else
            {
                _log?.Warn($"'{typed}' is not a number.");
            }
        }

        /// <summary>Item name with live suggestions, spawning into the player's hands.</summary>
        internal static void BuildItemSpawner(Transform parent)
        {
            bool built = PhoneUi.CreateSearchRow(
                parent, "Item", "start typing, e.g. bread", "Spawn",
                query => GameplayCheats.SearchItems(query, limit: 6),
                value => GameplayCheats.SpawnItem(value));

            if (!built)
            {
                _log?.Warn("No text field could be cloned; the item spawner was skipped.");
            }
        }

        /// <summary>
        /// The game only updates its title and app highlight inside SelectApp, which we
        /// cannot call, so do that part by hand.
        /// </summary>
        private static void SyncMenuChrome()
        {
            try
            {
                FullMenu fullMenu = InstanceBehavior<UIs>.Instance?.fullMenu;
                if (fullMenu == null)
                {
                    return;
                }

                if (fullMenu.appNameLabel != null)
                {
                    fullMenu.appNameLabel.Key = "Bigamst Trainer";
                    fullMenu.appNameLabel.enabled = false;
                }

                foreach (FullMenuAppButton button in
                         fullMenu.GetComponentsInChildren<FullMenuAppButton>(includeInactive: true))
                {
                    if (button == null)
                    {
                        continue;
                    }

                    if (button.gameObject.name == ButtonName)
                    {
                        button.ShowSelectedIcon();
                    }
                    else
                    {
                        button.HideSelectedIcon();
                    }
                }
            }
            catch (Exception)
            {
                // Cosmetic only; never worth failing the panel over.
            }
        }

        private static void BuildPanel(Transform parent)
        {
            _panel = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup));
            _panel.transform.SetParent(parent, worldPositionStays: false);

            // Fill the app area, matching how the game's own app panels sit.
            var rect = (RectTransform)_panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            PhoneUi.PaintBackground(rect);
            AddHeading(rect, "Bigamst Trainer");

            RectTransform content = PhoneUi.CreateScrollBody(rect, topInset: 110f);

            // Prefer the game's own option controls: they match the rest of the UI, and
            // bring sliders, toggles and dropdowns with persistence already handled.
            int spawned = GameOptionsRenderer.Render(content, TrainerMod.BuiltOptions, TrainerMod.ModId);
            if (spawned > 0)
            {
                // These buttons carry the same "Your text here" placeholder as the ones in
                // the Options menu, and nothing spawns them through the rebuild hook.
                ButtonLabelFixer.RelabelUnder(content);
                _log?.Info($"Trainer panel built from {spawned} game option control(s).");
            }
            else
            {
                _log?.Warn("Game option prefabs unavailable; using the built-in button list.");
                PopulateContent(content);
            }

            _panel.SetActive(false);
        }

        /// <summary>
        /// Everything here calls the same code as the Options menu, so behaviour cannot
        /// drift between the two surfaces.
        /// </summary>
        private static void PopulateContent(RectTransform content)
        {
            PhoneUi.CreateHeading(content, "Money");
            PhoneUi.CreateButtonRow(content,
                ("+$10K", () => GameplayCheats.ChangeMoney(10_000f)),
                ("+$100K", () => GameplayCheats.ChangeMoney(100_000f)),
                ("+$1M", () => GameplayCheats.ChangeMoney(1_000_000f)),
                ("+$10M", () => GameplayCheats.ChangeMoney(10_000_000f)));
            PhoneUi.CreateButton(content, "Pay off all loans", TrainerMod.ClearLoansAction);

            PhoneUi.CreateHeading(content, "Business");
            PhoneUi.CreateButton(content, "Restock every shelf and fridge",
                () => BusinessCheats.RestockEverything());
            PhoneUi.CreateButton(content, "Mark all stock as paid for", BusinessCheats.MarkStockPaid);
            PhoneUi.CreateButton(content, "Remove all dirt", () => BusinessCheats.CleanEverything());

            PhoneUi.CreateHeading(content, "Player");
            PhoneUi.CreateButton(content, "Restore energy, hunger and happiness",
                TrainerMod.RestoreAllStatsAction);
            PhoneUi.CreateButton(content, "Toggle invincibility", GameplayCheats.ToggleInvincibility);
            PhoneUi.CreateButton(content, "Unlock all courses", GameplayCheats.UnlockAllCourses);

            PhoneUi.CreateHeading(content, "Employees");
            PhoneUi.CreateButton(content, "Satisfy all employees", TrainerMod.SatisfyAllEmployeesAction);
            PhoneUi.CreateButton(content, "Clear absences and sick days", TrainerMod.ClearAbsencesAction);
            PhoneUi.CreateButton(content, "Max out every employee skill", BusinessCheats.MaxEmployeeSkills);

            PhoneUi.CreateHeading(content, "Vehicles");
            PhoneUi.CreateButton(content, "Repair, refuel and clean all",
                TrainerMod.ServiceAllVehiclesAction);
            PhoneUi.CreateButton(content, "Repair the vehicle you are in",
                GameplayCheats.RepairCurrentVehicle);
            PhoneUi.CreateButton(content, "Refuel the vehicle you are in",
                GameplayCheats.RefuelCurrentVehicle);

            PhoneUi.CreateHeading(content, "Time");
            PhoneUi.CreateButtonRow(content,
                ("Skip 1h", () => GameplayCheats.SkipTime("1h")),
                ("Skip 8h", () => GameplayCheats.SkipTime("8h")),
                ("Skip 1d", () => GameplayCheats.SkipTime("1d")));
            PhoneUi.CreateButtonRow(content,
                ("Speed 0%", () => GameplayCheats.SetGameSpeed(0)),
                ("100%", () => GameplayCheats.SetGameSpeed(100)),
                ("300%", () => GameplayCheats.SetGameSpeed(300)),
                ("500%", () => GameplayCheats.SetGameSpeed(500)));

            PhoneUi.CreateHeading(content, "Teleport");
            PhoneUi.CreateButton(content, "Go to map destination", TeleportCheats.ToDestination);
            PhoneUi.CreateButton(content, "Go inside map destination", TeleportCheats.InsideDestination);
            PhoneUi.CreateButton(content, "Go to quest target", TeleportCheats.ToQuestTarget);
            PhoneUi.CreateButton(content, "Go to the casino", GameplayCheats.GoToCasino);

            PhoneUi.CreateHeading(content, "Rivals");
            PhoneUi.CreateButton(content, "Defeat all rivals", TrainerMod.DefeatAllRivalsAction);
        }

        private static void AddHeading(RectTransform parent, string text)
        {
            var go = new GameObject("Heading", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(40f, -120f);
            rect.offsetMax = new Vector2(-40f, -40f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Left;
        }


        /// <summary>
        /// Mirrors FullMenu.SelectApp: hide every sibling app panel, then show ours.
        /// Also updates the menu title and button highlight, which the game would
        /// normally do and which otherwise keep showing the previously opened app.
        /// </summary>
        private static void Show()
        {
            if (_panel == null || _appsContainer == null)
            {
                return;
            }

            foreach (Transform child in _appsContainer)
            {
                child.gameObject.SetActive(child.gameObject == _panel);
            }

            var group = _panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
            }

            SyncMenuChrome();
            _log?.Info("Opened the Trainer app.");
        }
    }
}
