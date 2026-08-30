using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Helpers;
using Timemachine;

namespace BigamstTrainer
{
    /// <summary>
    /// Wrappers over the game's own developer console commands.
    ///
    /// These are `[ConsoleMethod]` entry points, not a supported API, so every call is
    /// guarded: one broken command must not take the panel down with it.
    ///
    /// Note that the game's Toggle* commands flip state without exposing a getter, so they
    /// are exposed as buttons rather than option toggles. A toggle would desynchronise —
    /// the options panel re-invokes every callback whenever it is rebuilt, which would
    /// flip the game's state each time you opened Options.
    /// </summary>
    internal static class GameplayCheats
    {
        private static IModLogger _log;

        internal static void Initialize(IModLogger log) => _log = log;

        internal static void Reset() => _log = null;

        /// <summary>Runs a console command, reporting rather than throwing on failure.</summary>
        private static void Run(string description, Action action)
        {
            try
            {
                action();
                _log?.Info(description);
            }
            catch (Exception exception)
            {
                _log?.Warn($"{description} failed: {exception.Message}");
            }
        }

        // ---- Time ----------------------------------------------------------------

        /// <summary>100 is normal speed; 0 pauses the simulation.</summary>
        internal static void SetGameSpeed(int percentage) =>
            Run($"Game speed = {percentage}%", () => TimeHelper.Command_SetTimeSpeed(percentage));

        /// <summary>Accepts the game's own format, e.g. "1d2h50m".</summary>
        internal static void SkipTime(string amount) =>
            Run($"Skipped {amount}", () => TimeMachine.SkipTime(amount));

        // ---- World ---------------------------------------------------------------

        internal static void ToggleTraffic() =>
            Run("Toggled traffic", GameManager.Command_ToggleTraffic);

        internal static void TogglePedestrians() =>
            Run("Toggled pedestrians", PedestrianSpawner.Command_ToggleSpawning);

        internal static void ToggleSeasonRestrictions() =>
            Run("Toggled season restrictions on items", BuildingManager.ToggleIgnoreSeasons);

        internal static void SpawnCustomers(int amount) =>
            Run($"Spawned {amount} customer(s)", () => IndoorCustomerSpawner.SpawnCustomers(amount));

        // ---- Player --------------------------------------------------------------

        internal static void ToggleInvincibility() =>
            Run("Toggled invincibility", EnergyHelper.Command_ToggleInvincibility);

        internal static void ChangeAge(float years) =>
            Run($"Age {years:+0;-0} year(s)", () => GameManager.Command_ChangeAge(years));

        internal static void UnlockAllCourses() =>
            Run("Unlocked all courses", EducationHelper.UnlockAllCourses);

        internal static void UnlockAllContacts() =>
            Run("Unlocked all contacts", Entities.ContactsHelper.UnlockAllContacts);

        // ---- Progression ---------------------------------------------------------

        internal static void CompleteObjective() =>
            Run("Completed the current objective", TutorialHelper.Command_CompleteObjective);

        internal static void CompleteQuest() =>
            Run("Completed the current quest", TutorialHelper.Command_CompleteQuest);

        // ---- Current vehicle -----------------------------------------------------
        //
        // These act on the vehicle the player is currently in, and the game warns on its
        // own when there isn't one.

        internal static void RepairCurrentVehicle() =>
            Run("Repaired the current vehicle", VehicleHelper.RepairVehicle);

        internal static void RefuelCurrentVehicle() =>
            Run("Refuelled the current vehicle", VehicleHelper.RefuelVehicle);

        internal static void SetMaxSpeed(int value) =>
            Run($"Max speed = {value}", () => VehicleHelper.SetMaxSpeed(value));

        internal static void SetEnginePower(int value) =>
            Run($"Engine power = {value}", () => VehicleHelper.SetEnginePower(value));

        internal static void SetBrakeForce(int value) =>
            Run($"Brake force = {value}", () => VehicleHelper.SetBrakeForce(value));

        internal static void SetTurnRadius(int value) =>
            Run($"Turn radius = {value}", () => VehicleHelper.SetMaxTurnRadius(value));

        /// <summary>0 makes the vehicle effectively immune to collision damage.</summary>
        internal static void SetDamageIntensity(int value) =>
            Run($"Damage intensity = {value}", () => VehicleHelper.SetDamageIntensity(value));

        /// <summary>Teleport to the casino. Kept as a destination, not a gambling cheat.</summary>
        internal static void GoToCasino() =>
            Run("Teleported to the casino", CasinoBoatManager.Command_GoToCasino);

        // ---- Items ---------------------------------------------------------------

        /// <summary>
        /// Puts an item in the player's hands. Command_GetItem dereferences the resolved
        /// item without a null check, so the name is validated first; it also requires
        /// empty hands, no vehicle and no placement mode, and says so itself.
        /// </summary>
        internal static void SpawnItem(string itemName, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                _log?.Warn("Enter an item name first.");
                return;
            }

            itemName = itemName.Trim();
            if (BigAmbitions.Items.ItemsGetter.GetByName(itemName, suppressError: true) == null)
            {
                _log?.Warn($"No item called '{itemName}'.");
                return;
            }

            Run($"Spawned {amount}x {itemName}", () => ItemHelper.Command_GetItem(itemName, amount));
        }

        /// <summary>
        /// Item names matching <paramref name="query"/>, best matches first.
        ///
        /// Ids look like "ba:itemname_bread", so the search also runs against the
        /// localized display name — typing "bread" should find it without knowing the id.
        /// </summary>
        internal static List<(string Id, string Display)> SearchItems(string query, int limit)
        {
            var results = new List<(string Id, string Display)>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            query = query.Trim();

            try
            {
                IEnumerable<BigAmbitions.Items.Item> all = BigAmbitions.Items.ItemsGetter.AllItems;
                if (all == null)
                {
                    return results;
                }

                var starts = new List<(string, string)>();
                var contains = new List<(string, string)>();

                foreach (BigAmbitions.Items.Item item in all)
                {
                    string id = item?.itemName;
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    string display = id.GetLocalization();
                    if (string.IsNullOrEmpty(display))
                    {
                        display = id;
                    }

                    // Rank a prefix hit on the display name above a hit anywhere else,
                    // so "bre" surfaces Bread before Wholegrain Bread Mix.
                    if (display.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    {
                        starts.Add((id, display));
                    }
                    else if (display.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        contains.Add((id, display));
                    }
                }

                starts.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
                contains.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));

                foreach ((string id, string display) in starts)
                {
                    if (results.Count >= limit) { return results; }
                    results.Add((id, display));
                }

                foreach ((string id, string display) in contains)
                {
                    if (results.Count >= limit) { return results; }
                    results.Add((id, display));
                }
            }
            catch (Exception exception)
            {
                _log?.Warn($"Item search failed: {exception.Message}");
            }

            return results;
        }

        // ---- Personal goals ------------------------------------------------------

        /// <summary>
        /// Marks every personal goal complete.
        ///
        /// The game's own command only writes the id list; the private SetCompleted, which
        /// fires the completion popup, the happiness modifier and the Steam achievement,
        /// runs solely from CheckForCompletion when a goal is genuinely met. So each goal
        /// is offered a real check first — anything you have actually earned unlocks now —
        /// and the rest are filled in. Those remaining achievements appear when the save is
        /// next loaded, which is when the game re-checks them itself.
        /// </summary>
        internal static void CompleteAllPersonalGoals()
        {
            int earned = 0;

            try
            {
                List<GenericPersonalGoal> goals =
                    InstanceBehavior<GameManager>.Instance?.personalGoals;
                if (goals != null)
                {
                    foreach (GenericPersonalGoal goal in goals)
                    {
                        if (goal == null || goal.IsCompleted)
                        {
                            continue;
                        }

                        try
                        {
                            goal.CheckForCompletion();
                            if (goal.IsCompleted)
                            {
                                earned++;
                            }
                        }
                        catch (Exception)
                        {
                            // One goal's own check failing must not stop the rest.
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _log?.Warn($"Goal check pass failed: {exception.Message}");
            }

            Run($"Completed all personal goals ({earned} unlocked now, the rest on next load)",
                UI.Smartphone.Apps.Persona.PersonalGoalsUI.Command_CompleteAll);
        }

        internal static void ResetPersonalGoals() =>
            Run("Cleared all completed personal goals",
                UI.Smartphone.Apps.Persona.PersonalGoalsUI.Command_Reset);

        // ---- Waypoints -----------------------------------------------------------
        //
        // Stored by the game in PlayerPrefs under "tpwWaypoints", as
        // "name|x,y,z" entries joined by ';'. Reading it directly is what lets the
        // panel offer the saved names instead of asking you to remember them.

        private const string WaypointsKey = "tpwWaypoints";

        internal static void AddWaypoint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _log?.Warn("Give the waypoint a name first.");
                return;
            }

            Run($"Saved waypoint '{name.Trim().ToLowerInvariant()}' at your position",
                () => GameManager.Command_AddWaypoint(name.Trim()));
        }

        internal static void TeleportToWaypoint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _log?.Warn("Pick a waypoint first.");
                return;
            }

            Run($"Teleported to waypoint '{name.Trim()}'",
                () => GameManager.Command_TeleportPlayerToWaypoint(name.Trim()));
        }

        internal static void RemoveWaypoint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Run($"Removed waypoint '{name.Trim()}'",
                () => GameManager.Command_RemoveWaypoint(name.Trim()));
        }

        internal static void ClearWaypoints() =>
            Run("Cleared all waypoints", GameManager.Command_ClearWaypoints);

        /// <summary>Saved waypoint names matching a query, for the suggestion list.</summary>
        internal static List<(string Id, string Display)> SearchWaypoints(string query, int limit)
        {
            var results = new List<(string, string)>();

            try
            {
                string raw = UnityEngine.PlayerPrefs.GetString(WaypointsKey, string.Empty);
                if (string.IsNullOrEmpty(raw))
                {
                    return results;
                }

                query = (query ?? string.Empty).Trim();
                foreach (string entry in raw.Split(';'))
                {
                    int bar = entry.IndexOf('|');
                    if (bar <= 0)
                    {
                        continue;
                    }

                    string name = entry.Substring(0, bar);
                    if (query.Length > 0 &&
                        name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    results.Add((name, name));
                    if (results.Count >= limit)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                _log?.Warn($"Could not read waypoints: {exception.Message}");
            }

            return results;
        }

        // ---- Money ---------------------------------------------------------------

        /// <summary>
        /// Goes through the game's own money routine rather than assigning to
        /// GameInstance.Money, so the change is clamped against overflow, recorded as a
        /// transaction, and shown in the top bar.
        /// </summary>
        internal static void ChangeMoney(float amount) =>
            Run($"Money {amount:+#,##0;-#,##0}", () => GameManager.Command_ChangeMoney(amount));

        internal static void SetMoney(float amount) =>
            Run($"Money set to {amount:N0}", () => GameManager.Command_SetMoney(amount));
    }
}
