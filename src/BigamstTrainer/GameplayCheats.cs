using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Helpers;
using Timemachine;
using UI.Notification;
using NWH.VehiclePhysics2.Damage;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;

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

        internal static void Initialize(IModLogger log)
        {
            _log = log;

            // A new city means a new GlobalReferences with stock speeds, so forget the
            // cached originals and let the next tick reapply onto the fresh instance.
            //
            // The requested speed goes back to normal too. This class is static, so it
            // outlives a city; without this a value set earlier in the session would come
            // back even when the player has asked for saved settings not to be applied.
            _walkDefaults = null;
            _appliedMovePercent = 0;
            _requestedMovePercent = 100;

            // Cars respawn from their prefabs on a city load, so any baseline captured for
            // the last city describes vehicles that no longer exist.
            StockTuning.Clear();
        }

        internal static void Reset() => _log = null;

        /// <summary>
        /// Player movement speed, as a percentage of normal.
        ///
        /// The five on-foot speeds live on GlobalReferences and are re-read every time the
        /// game calls SetWalkingSpeed, so scaling them there covers walking, jogging and
        /// running without fighting the game each frame. Scooter speed is left alone: it
        /// is a vehicle, and the vehicle tuning controls cover that.
        /// </summary>
        private static float[] _walkDefaults;
        private static int _requestedMovePercent = 100;
        private static int _appliedMovePercent;

        internal static void SetMovementSpeed(int percent)
        {
            _requestedMovePercent = percent;
            ApplyMovementSpeed();
        }

        /// <summary>
        /// Reapplies the speed if it has not taken effect yet — the singleton does not
        /// exist while the main menu is up, so a value set there lands on city load.
        /// Costs one int comparison per frame once applied.
        /// </summary>
        internal static void EnsureMovementSpeed()
        {
            if (_appliedMovePercent != _requestedMovePercent)
            {
                ApplyMovementSpeed();
            }
        }

        private static void ApplyMovementSpeed()
        {
            try
            {
                GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
                if (references == null)
                {
                    return;
                }

                // Cache the stock values once, and always scale from those, so repeated
                // applies cannot compound.
                if (_walkDefaults == null)
                {
                    _walkDefaults = new[]
                    {
                        references.walkingSpeedZombie,
                        references.walkingSpeedWalk,
                        references.walkingSpeedWalkFast,
                        references.walkingSpeedJog,
                        references.walkingSpeedRun
                    };
                }

                float scale = Math.Max(_requestedMovePercent, 10) / 100f;
                references.walkingSpeedZombie   = _walkDefaults[0] * scale;
                references.walkingSpeedWalk     = _walkDefaults[1] * scale;
                references.walkingSpeedWalkFast = _walkDefaults[2] * scale;
                references.walkingSpeedJog      = _walkDefaults[3] * scale;
                references.walkingSpeedRun      = _walkDefaults[4] * scale;

                // The character keeps the speed it was given, so hand it the new one for
                // whichever gait it is currently in.
                ThirdPersonCharacter character = PlayerHelper.PlayerController?.Character;
                character?.SetWalkingSpeed(character.walkingSpeed);

                _appliedMovePercent = _requestedMovePercent;
            }
            catch (Exception exception)
            {
                _log?.Warn($"Movement speed failed: {exception.Message}");
            }
        }

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
        /// <summary>
        /// Puts a message on screen as well as in the log.
        ///
        /// The log is a file the player never sees, so a button whose only effect is a log
        /// line looks broken. The key must exist in Locales/&lt;locale&gt;.json, and each
        /// entry in the data dictionary fills the matching {placeholder} in it.
        /// </summary>
        private static void Notify(NotificationType type, string key,
                                   Dictionary<string, string> data = null)
        {
            try
            {
                Notifications.Show(type, key, data, 6f);
            }
            catch (Exception exception)
            {
                _log?.Warn($"Could not show the '{key}' notification: {exception.Message}");
            }
        }

        /// <summary>
        /// The car's own tuning values, captured before this mod first overwrote them, so
        /// they can be put back. Keyed by vehicle id; lost when the game closes, since
        /// there is nowhere on the save to keep them.
        /// </summary>
        private static readonly Dictionary<string, float[]> StockTuning =
            new Dictionary<string, float[]>();

        /// <summary>
        /// The car being driven, or null with a warning. Every tuning call needs it.
        /// </summary>
        private static VehicleController CurrentCar(bool quiet = false)
        {
            VehicleController vehicle = VehicleHelper.GetCurrentVehicleBase();
            if (vehicle == null && !quiet)
            {
                _log?.Warn("Get in a car first — tuning applies to the car you are driving.");
                Notify(NotificationType.Warning, "bigamst_not_in_a_car");
            }

            return vehicle;
        }

        /// <summary>
        /// Reads what a car is actually set to. Any of these modules can be absent — the
        /// game says as much when tuning such a vehicle — so each is reported separately.
        /// </summary>
        private static bool TryReadTuning(VehicleController vehicle, out float[] values)
        {
            values = new float[5];
            bool any = false;

            SpeedLimiterModuleWrapper limiter = vehicle.GetComponent<SpeedLimiterModuleWrapper>();
            if (limiter?.module != null)
            {
                values[0] = limiter.module.speedLimit;
                any = true;
            }

            NWH.VehiclePhysics2.VehicleController physics =
                vehicle.GetComponent<NWH.VehiclePhysics2.VehicleController>();
            if (physics != null)
            {
                values[1] = physics.powertrain.engine.maxPower;
                values[2] = physics.brakes.maxTorque;
                values[3] = physics.steering.maximumSteerAngle;
                any = true;
            }

            DamageHandler damage = vehicle.GetComponent<DamageHandler>();
            if (damage != null)
            {
                values[4] = damage.damageIntensity;
                any = true;
            }

            return any;
        }

        /// <summary>Reports what the car you are in is currently set to.</summary>
        internal static void ReportCarTuning()
        {
            VehicleController vehicle = CurrentCar();
            if (vehicle == null)
            {
                return;
            }

            try
            {
                if (!TryReadTuning(vehicle, out float[] values))
                {
                    _log?.Warn("This vehicle has none of the tunable modules.");
                    return;
                }

                _log?.Info($"{vehicle.vehicleType.vehicleTypeName}: max speed {values[0]:0}, " +
                           $"engine power {values[1]:0}, brake force {values[2]:0}, " +
                           $"steering angle {values[3]:0}°, damage {values[4] * 100f:0}%.");

                Notify(NotificationType.Info, "bigamst_car_tuning", new Dictionary<string, string>
                {
                    { "car", vehicle.vehicleType.vehicleTypeName },
                    { "speed", $"{values[0]:0}" },
                    { "power", $"{values[1]:0}" },
                    { "brakes", $"{values[2]:0}" },
                    { "steer", $"{values[3]:0}" },
                    { "damage", $"{values[4] * 100f:0}" }
                });
            }
            catch (Exception exception)
            {
                _log?.Warn($"Could not read this car's tuning: {exception.Message}");
            }
        }

        /// <summary>
        /// Remembers a car's original values the first time it is tuned, so
        /// ResetCarTuning has something to put back.
        /// </summary>
        internal static void RememberStockTuning(bool quiet = false)
        {
            VehicleController vehicle = CurrentCar(quiet);
            if (vehicle == null)
            {
                return;
            }

            try
            {
                string id = vehicle.vehicleInstance?.id;
                if (string.IsNullOrEmpty(id) || StockTuning.ContainsKey(id))
                {
                    return;
                }

                if (TryReadTuning(vehicle, out float[] values))
                {
                    StockTuning[id] = values;
                }
            }
            catch (Exception exception)
            {
                _log?.Warn($"Could not record this car's original tuning: {exception.Message}");
            }
        }

        /// <summary>
        /// Applies tuning as percentages of what the car itself came with.
        ///
        /// Absolute figures cannot work here: a Honza Mimic has 45 engine power and 2500
        /// brake force, so one slider's sane-looking number is another's thirtyfold. Tuning
        /// by absolute value gave cars ten to thirty times their real power, which does not
        /// make them fast — the wheels simply spin and the car sits there. A percentage of
        /// the car's own baseline means 100% is always exactly stock, whatever the vehicle.
        ///
        /// The baseline is whatever the car had before this mod first touched it, so
        /// repeatedly applying 150% gives 150% of stock, never 150% of the last result.
        /// </summary>
        internal static bool ApplyTuningPercent(int speedPercent, int powerPercent,
                                                int brakePercent, int steerPercent,
                                                int damagePercent, bool quiet = false)
        {
            // quiet is for restoring tuning by itself, where the car may not have spawned
            // yet: the caller retries rather than telling the player to get in a car they
            // are already sitting in.
            VehicleController vehicle = CurrentCar(quiet);
            if (vehicle == null)
            {
                return false;
            }

            try
            {
                RememberStockTuning(quiet);

                string id = vehicle.vehicleInstance?.id;
                if (string.IsNullOrEmpty(id) || !StockTuning.TryGetValue(id, out float[] stock))
                {
                    if (!quiet)
                    {
                        _log?.Warn("Could not read this car's own values, so tuning was not applied.");
                        Notify(NotificationType.Warning, "bigamst_no_tuning_modules");
                    }

                    return false;
                }

                int changed = 0;

                SpeedLimiterModuleWrapper limiter = vehicle.GetComponent<SpeedLimiterModuleWrapper>();
                if (limiter?.module != null && speedPercent != 100)
                {
                    limiter.module.speedLimit = stock[0] * speedPercent / 100f;
                    changed++;
                }

                NWH.VehiclePhysics2.VehicleController physics =
                    vehicle.GetComponent<NWH.VehiclePhysics2.VehicleController>();
                if (physics != null)
                {
                    if (powerPercent != 100)
                    {
                        physics.powertrain.engine.maxPower = stock[1] * powerPercent / 100f;
                        changed++;
                    }

                    if (brakePercent != 100)
                    {
                        physics.brakes.maxTorque = stock[2] * brakePercent / 100f;
                        changed++;
                    }

                    if (steerPercent != 100)
                    {
                        physics.steering.maximumSteerAngle = stock[3] * steerPercent / 100f;
                        changed++;
                    }
                }

                DamageHandler damage = vehicle.GetComponent<DamageHandler>();
                if (damage != null && damagePercent != 100)
                {
                    damage.damageIntensity = stock[4] * damagePercent / 100f;
                    changed++;
                }

                _log?.Info($"Tuned {vehicle.vehicleType.vehicleTypeName}: speed {speedPercent}%, " +
                           $"power {powerPercent}%, brakes {brakePercent}%, " +
                           $"steering {steerPercent}%, damage {damagePercent}% of stock " +
                           $"({changed} changed).");

                if (!quiet)
                {
                    ReportTuningApplied(changed);
                }

                return true;
            }
            catch (Exception exception)
            {
                _log?.Warn($"Tuning failed: {exception.Message}");
                return false;
            }
        }

        /// <summary>Says on screen how much of the tuning actually applied.</summary>
        internal static void ReportTuningApplied(int changed)
        {
            if (changed == 0)
            {
                _log?.Info("Nothing applied: every tuning slider is still at its default.");
                Notify(NotificationType.Warning, "bigamst_tuning_nothing_set");
                return;
            }

            _log?.Info($"Applied {changed} tuning value(s) to the car.");
            Notify(NotificationType.Success, "bigamst_tuning_applied",
                   new Dictionary<string, string> { { "count", changed.ToString() } });
        }

        /// <summary>
        /// Puts a car back to what it was before this mod first tuned it. Returns whether
        /// anything was restored, so the caller can put the sliders back to 100% too.
        /// </summary>
        internal static bool ResetCarTuning()
        {
            VehicleController vehicle = CurrentCar();
            if (vehicle == null)
            {
                return false;
            }

            string id = vehicle.vehicleInstance?.id;
            if (string.IsNullOrEmpty(id) || !StockTuning.TryGetValue(id, out float[] values))
            {
                _log?.Warn("This car has not been tuned this session, so there is nothing " +
                           "to undo. Original values are only remembered until the game closes.");
                Notify(NotificationType.Warning, "bigamst_nothing_to_undo");
                return false;
            }

            SetMaxSpeed((int)values[0]);
            SetEnginePower((int)values[1]);
            SetBrakeForce((int)values[2]);
            SetTurnRadius((int)values[3]);

            // values[4] is the raw multiplier as read off the car, so it is restored
            // directly rather than through the percentage-based setter.
            Run("Damage restored", () =>
            {
                DamageHandler handler = vehicle.GetComponent<DamageHandler>();
                if (handler != null)
                {
                    handler.damageIntensity = values[4];
                }
            });
            _log?.Info($"{vehicle.vehicleType.vehicleTypeName} put back to its original tuning.");
            Notify(NotificationType.Success, "bigamst_tuning_undone",
                   new Dictionary<string, string> { { "car", vehicle.vehicleType.vehicleTypeName } });
            return true;
        }

        /// <summary>
        /// Damage taken, as a percentage of normal.
        ///
        /// damageIntensity is a multiplier whose stock value is 1, not a 0-100 figure, so
        /// the game's own SetDamageIntensity console command — which takes an int — cannot
        /// express "normal" at all: passing 100 asks for a hundred times the damage. The
        /// field is written directly so 100% means 100%.
        /// </summary>
        internal static void SetDamageIntensity(int percent) =>
            Run($"Damage taken = {percent}%", () =>
            {
                VehicleController vehicle = VehicleHelper.GetCurrentVehicleBase();
                DamageHandler handler = vehicle?.GetComponent<DamageHandler>();
                if (handler == null)
                {
                    throw new InvalidOperationException("this vehicle has no damage module");
                }

                handler.damageIntensity = percent / 100f;
            });

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
