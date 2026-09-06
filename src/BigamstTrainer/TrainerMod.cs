using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using BigAmbitions.Mods;
using BigAmbitions.Rivals;
using Entities;
using UnityEngine;
using Vehicles.VehicleTypes;

[assembly: RegisterModClass(typeof(BigamstTrainer.TrainerMod))]

namespace BigamstTrainer
{
    /// <summary>
    /// Entry point. The City scope means this loads once a save is actually in play,
    /// which is what a trainer wants — there is nothing to cheat at in the main menu.
    /// </summary>
    [ModEntryOnCityLoad]
    public sealed class TrainerMod : ModBigAmbitionsBase
    {
        // Option ids become PlayerPrefs keys "m:{modId}:{optionId}". They must stay stable
        // across releases or previously saved values are silently dropped.
        //
        // The five tuning ids gained a _pct suffix when those sliders stopped being
        // absolute figures and became percentages of the car's own values. Dropping the
        // old values is the point: a saved brake force of 196 would otherwise come back
        // as 196% of stock.
        private const string OptMoneyAmount     = "bigamst.money.amount";
        private const string OptMoneyFloor      = "bigamst.money.floor";
        private const string OptTaxPercent      = "bigamst.econ.tax";
        private const string OptSalaryMult      = "bigamst.econ.salary_mult";
        private const string OptMarketMult      = "bigamst.econ.market_mult";
        private const string OptInterestMult    = "bigamst.econ.interest_mult";
        private const string OptSellMult        = "bigamst.econ.sell_mult";
        private const string OptNoTradeLimits   = "bigamst.econ.no_trade_limits";
        private const string OptAllImports      = "bigamst.econ.all_imports";
        private const string OptAllContacts     = "bigamst.econ.all_contacts";
        private const string OptAllCourses      = "bigamst.econ.all_courses";
        private const string OptKeepEnergy      = "bigamst.player.keep_energy";
        private const string OptKeepHunger      = "bigamst.player.keep_hunger";
        private const string OptKeepHappiness   = "bigamst.player.keep_happiness";
        private const string OptNoAging         = "bigamst.player.no_aging";
        private const string OptNoEnergyDrain   = "bigamst.player.no_energy";
        private const string OptMoveSpeed       = "bigamst.player.move_speed";
        private const string OptApplyOnLoad     = "bigamst.general.apply_on_load";
        private const string OptAutoRestock     = "bigamst.business.auto_restock";
        private const string OptAutoClean       = "bigamst.business.auto_clean";
        private const string OptFreeRent        = "bigamst.business.free_rent";
        private const string OptKeepStaffHappy  = "bigamst.employee.keep_satisfied";
        private const string OptNoVehicleDamage = "bigamst.vehicle.no_damage";
        private const string OptNoVehicleFuel   = "bigamst.vehicle.no_fuel";
        private const string OptRivalDifficulty = "bigamst.rivals.difficulty";
        private const string OptGameSpeed       = "bigamst.gameplay.speed";
        private const string OptCarSpeed        = "bigamst.vehicle.max_speed_pct";
        private const string OptCarPower        = "bigamst.vehicle.engine_power_pct";
        private const string OptCarBrakes       = "bigamst.vehicle.brake_force_pct";
        private const string OptCarTurn         = "bigamst.vehicle.steer_angle_pct";
        private const string OptCarDamage       = "bigamst.vehicle.damage_pct";
        private const string OptFreezeClock     = "bigamst.time.freeze";
        private const string OptSetHour         = "bigamst.time.hour";

        /// <summary>
        /// Energy, Hunger and Happiness are normalised 0..100 by EnergySettings
        /// (maxEnergyHungerHappinessValue = 100f). Note higher Hunger is better:
        /// energy burns faster at zero hunger. EmployeeInstance.satisfaction shares
        /// the range — the game clamps it with Mathf.Clamp(satisfaction, 0f, 100f).
        /// </summary>
        private const float StatMax = 100f;

        private const float Million = 1_000_000f;

        /// <summary>
        /// Sweeping every employee each frame is wasteful on a large save, so the
        /// satisfaction pass runs on this interval instead.
        /// </summary>
        private const float StaffSweepIntervalSeconds = 1f;

        /// <summary>
        /// Restock and cleaning walk every container in every owned property, which is
        /// far heavier than the employee pass, so they run much less often.
        /// </summary>
        private const float BusinessSweepIntervalSeconds = 5f;

        /// <summary>
        /// A missing localization key falls back to the raw string, and arguments are then
        /// substituted with a literal "{value}" replace. So these render real numbers even
        /// though none of them are real keys. See NOTES.md.
        /// </summary>
        private const string LabelDollarsM = "${value}M";
        private const string LabelPercent  = "{value}%";
        private const string LabelHour     = "{value}:00";



        /// <summary>
        /// Last value logged per setting. The options panel re-invokes every callback
        /// on each rebuild, so without this simply opening Options would spam the log.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, object> LastApplied =
            new System.Collections.Generic.Dictionary<string, object>();

        /// <summary>
        /// Options the phone panel skips. The Options menu and the phone need slightly
        /// different controls for the same job, so a few entries belong to one surface.
        /// </summary>
        internal static readonly System.Collections.Generic.HashSet<ModOption> MenuOnlyOptions =
            new System.Collections.Generic.HashSet<ModOption>();

        private static ModOption MenuOnly(ModOption option)
        {
            MenuOnlyOptions.Add(option);
            return option;
        }

        /// <summary>Choices for the Options-menu amount dropdown.</summary>
        private static readonly string[] MoneyAmountChoices =
        {
            "$1,000", "$10,000", "$100,000", "$1,000,000", "$10,000,000", "$100,000,000"
        };

        private static readonly float[] MoneyAmounts =
        {
            1_000f, 10_000f, 100_000f, Million, 10f * Million, 100f * Million
        };

        private float _selectedMoneyAmount = MoneyAmounts[1];

        private static IModLogger _log;

        /// <summary>
        /// The option definition, shared with the phone panel so both surfaces render the
        /// same list rather than maintaining two of them.
        /// </summary>
        internal static ModOptions BuiltOptions { get; private set; }

        internal static string ModId { get; private set; }


        private bool _keepEnergy;
        private bool _keepHunger;
        private bool _keepHappiness;
        private bool _keepStaffSatisfied;
        private bool _autoRestock;
        private bool _autoClean;
        private bool _freeRent;

        // Vehicle tuning targets, applied on demand rather than as the sliders move.
        private int _carSpeed = 160;
        private int _carPower = 200;
        private int _carBrakes = 200;
        private int _carTurn = 35;
        private int _carDamage = 100;

        /// <summary>Money is topped back up to this whenever it drops below. 0 disables.</summary>
        private float _moneyFloor;


        private bool _freezeClock;
        private int _targetHour = 8;
        private int _frozenDay;
        private int _frozenHour;
        private float _frozenMinute;

        private float _staffSweepTimer;
        private float _businessSweepTimer;
        private bool _tickSubscribed;
        private bool _applyOnLoad = true;
        private string _lastVehicleId;
        private const float PendingTuningTimeoutSeconds = 30f;
        private int[] _pendingTuning;
        private float _pendingTuningSeconds;

        public override Task OnLoadAsync(ModContext context)
        {
            _log = context.Logger;
            BusinessCheats.Initialize(_log);
            GameplayCheats.Initialize(_log);
            PhoneApp.Initialize(_log);
            TeleportCheats.Initialize(_log);

            var options = new ModOptions()

                .AddHeader("Money")
                .AddButton("Quick add $10,000  →", () => GameplayCheats.ChangeMoney(10_000f))
                .AddButton("Quick add $100,000  →", () => GameplayCheats.ChangeMoney(100_000f))
                .AddButton("Quick add $1,000,000  →", () => GameplayCheats.ChangeMoney(Million))
                // Phone only: the options API has no text field, so an exact amount can
                // only be typed here. Sits inside Money rather than at the end of the list.
                .AddCustom(new InlineUiOption(PhoneApp.BuildMoneyInput))
                // Menu only: the counterpart for the Options screen, where a typed amount
                // is not possible. Hidden on the phone so it is not shown twice.
                .AddCustom(MenuOnly(new DropdownOption(
                    OptMoneyAmount, "Amount", MoneyAmountChoices, 1, OnMoneyAmountChanged)))
                .AddCustom(MenuOnly(new ButtonOption("Add the amount above  →",
                    () => GameplayCheats.ChangeMoney(_selectedMoneyAmount))))
                .AddCustom(MenuOnly(new ButtonOption("Subtract the amount above  →",
                    () => GameplayCheats.ChangeMoney(-_selectedMoneyAmount))))
                .AddCustom(MenuOnly(new ButtonOption("Set money to the amount above  →",
                    () => GameplayCheats.SetMoney(_selectedMoneyAmount))))
                .AddSlider(OptMoneyFloor, "Never drop below", 0, 100, 0,
                    value => { _moneyFloor = value * Million; LogSetting("Money floor ($M)", value); }, LabelDollarsM)
                .AddButton("Pay off all loans  →", ClearLoans)

                .AddSplitter()
                .AddHeader("Time")
                .AddToggle(OptFreezeClock, "Freeze the clock", false, OnFreezeClockChanged)
                // The slider only records the target. Applying it directly would move the
                // clock every time the panel is built, because ModOptionsSliderControl
                // re-invokes OnValueChanged during Initialize.
                .AddSlider(OptSetHour, "Target hour", 0, 23, 8, value => { _targetHour = value; LogSetting("Target hour", value); }, LabelHour)
                .AddButton("Jump to the target hour  →", JumpToTargetHour)

                .AddSplitter()
                .AddHeader("Economy")
                .AddSlider(OptTaxPercent, "Tax rate", 0, 50, 10,
                    value => WithVariables("Tax rate", value, v => v.taxPercentage = value), LabelPercent)
                .AddSlider(OptSalaryMult, "Employee wages", 0, 200, 100,
                    value => WithVariables("Employee wages", value, v => v.employeeHourlySalaryMultiplier = value / 100f), LabelPercent)
                .AddSlider(OptMarketMult, "Market prices", 0, 300, 100,
                    value => WithVariables("Market prices", value, v => v.marketPriceMultiplier = value / 100f), LabelPercent)
                .AddSlider(OptInterestMult, "Bank interest", 0, 200, 100,
                    value => WithVariables("Bank interest", value, v => v.bankInterestMultiplier = value / 100f), LabelPercent)
                .AddSlider(OptSellMult, "Selling return", 0, 300, 75,
                    value => WithVariables("Selling return", value, v => v.sellingMultiplier = value / 100f), LabelPercent)
                .AddToggle(OptNoTradeLimits, "No wholesale or import limits", false,
                    value => WithVariables("No trade limits", value, v => v.disableWholesaleAndImportLimits = value))
                .AddToggle(OptAllImports, "All products available from importers", false,
                    value => WithVariables("All importer products", value, v => v.allProductsAvailableFromImporters = value))
                // Setting the flag only records the choice — the game unlocks by calling
                // ContactsHelper.UnlockAllContacts alongside it, as ApplyNewDifficulty
                // does. Without that the toggle appears to do nothing.
                .AddToggle(OptAllContacts, "All contacts unlocked", false, value =>
                {
                    bool wasOff = SaveGameManager.Current?.gameVariables?.allContactsUnlocked == false;
                    WithVariables("All contacts", value, v => v.allContactsUnlocked = value);
                    if (value && wasOff)
                    {
                        GameplayCheats.UnlockAllContacts();
                    }
                })
                .AddToggle(OptAllCourses, "All courses unlocked", false, value =>
                {
                    bool wasOff = SaveGameManager.Current?.gameVariables?.allCoursesUnlocked == false;
                    WithVariables("All courses", value, v => v.allCoursesUnlocked = value);
                    if (value && wasOff)
                    {
                        GameplayCheats.UnlockAllCourses();
                    }
                })

                .AddSplitter()
                .AddHeader("Player")
                .AddToggle(OptKeepEnergy,    "Keep energy full",    false, v => { _keepEnergy = v; LogSetting("Keep energy full", v); })
                .AddToggle(OptKeepHunger,    "Keep hunger full",    false, v => { _keepHunger = v; LogSetting("Keep hunger full", v); })
                .AddToggle(OptKeepHappiness, "Keep happiness full", false, v => { _keepHappiness = v; LogSetting("Keep happiness full", v); })
                .AddToggle(OptNoAging, "Disable aging", false,
                    value => WithVariables("Disable aging", value, v => v.disableAging = value))
                .AddToggle(OptNoEnergyDrain, "Disable energy system entirely", false,
                    value => WithVariables("Disable energy system", value, v => v.disableEnergy = value))
                // 50-300 rather than 10-500: on a slider this wide every step was about a
                // pixel, so landing back on 100% by dragging was almost impossible.
                .AddSlider(OptMoveSpeed, "Movement speed", 50, 300, 100,
                    GameplayCheats.SetMovementSpeed, LabelPercent)
                .AddButton("Movement speed back to normal  →", ResetMovementSpeed)
                .AddButton("Restore energy, hunger and happiness  →", RestoreAllStats)
                .AddButton("Unlock all courses  →", GameplayCheats.UnlockAllCourses)
                .AddButton("Unlock all contacts  →", GameplayCheats.UnlockAllContacts)
                .AddButton("Get 1 year younger  →", () => GameplayCheats.ChangeAge(-1f))
                .AddButton("Complete all personal goals  →", GameplayCheats.CompleteAllPersonalGoals)
                .AddButton("Clear completed personal goals  →", GameplayCheats.ResetPersonalGoals)

                .AddSplitter()
                .AddHeader("Businesses")
                .AddButton("Restock every shelf and fridge  →", () => BusinessCheats.RestockEverything())
                .AddToggle(OptAutoRestock, "Keep everything restocked", false, v => { _autoRestock = v; LogSetting("Auto restock", v); })
                .AddButton("Mark all stock as paid for  →", BusinessCheats.MarkStockPaid)
                .AddButton("Remove all dirt  →", () => BusinessCheats.CleanEverything())
                .AddToggle(OptAutoClean, "Keep everything spotless", false, v => { _autoClean = v; LogSetting("Auto clean", v); })
                .AddToggle(OptFreeRent, "No rent on owned property", false, value =>
                {
                    // The panel re-invokes this on every rebuild, so skip the
                    // property walk unless the setting actually changed.
                    if (_freeRent == value) { return; }
                    _freeRent = value;
                    BusinessCheats.SetFreeRent(value);
                })

                .AddSplitter()
                .AddHeader("Employees")
                .AddToggle(OptKeepStaffHappy, "Keep all employees fully satisfied", false,
                    v => { _keepStaffSatisfied = v; LogSetting("Keep staff satisfied", v); })
                .AddButton("Satisfy all employees now  →", SatisfyAllEmployees)
                .AddButton("Clear absences and sick days  →", ClearAbsences)
                .AddButton("Max out every employee skill  →", BusinessCheats.MaxEmployeeSkills)

                .AddSplitter()
                .AddHeader("Vehicles")
                .AddToggle(OptNoVehicleDamage, "Disable vehicle damage", false,
                    value => WithVariables("Disable vehicle damage", value, v => v.disableVehicleDamage = value))
                .AddToggle(OptNoVehicleFuel, "Disable fuel consumption", false,
                    value => WithVariables("Disable fuel use", value, v => v.disableVehicleFuel = value))
                .AddButton("Repair, refuel and clean all  →", ServiceAllVehicles)
                .AddButton("Clear parking tickets and fines  →", ClearParkingFines)
                .AddButton("Repair the vehicle you are in  →", GameplayCheats.RepairCurrentVehicle)
                .AddButton("Refuel the vehicle you are in  →", GameplayCheats.RefuelCurrentVehicle)
                // These only record a target. Applying on change would re-fire whenever the
                // panel is rebuilt, so getting into a second car and opening the menu would
                // silently give it the first car's tuning.
                // All five are percentages of what the car itself came with, so 100% is
                // stock for every vehicle and there is no way to ask for thirty times an
                // engine by accident.
                .AddSlider(OptCarSpeed, "Tune: max speed", 10, 300, 100, v => _carSpeed = v, LabelPercent)
                .AddSlider(OptCarPower, "Tune: engine power", 10, 300, 100, v => _carPower = v, LabelPercent)
                .AddSlider(OptCarBrakes, "Tune: brake force", 10, 300, 100, v => _carBrakes = v, LabelPercent)
                .AddSlider(OptCarTurn, "Tune: steering angle", 25, 200, 100, v => _carTurn = v, LabelPercent)
                .AddSlider(OptCarDamage, "Tune: damage taken", 0, 300, 100, v => _carDamage = v, LabelPercent)
                .AddButton("Show this car's current tuning  →", GameplayCheats.ReportCarTuning)
                .AddButton("Apply tuning to the car you are in  →", ApplyCarTuning)
                .AddButton("Undo tuning on the car you are in  →", UndoCarTuning)

                .AddSplitter()
                .AddHeader("Rivals")
                .AddSlider(OptRivalDifficulty, "Rival difficulty", 0, 200, 100,
                    value => WithVariables("Rival difficulty", value, v => v.rivalsDifficultyMultiplier = value / 100f), LabelPercent)
                .AddButton("Defeat all rivals  →", DefeatAllRivals)

                .AddSplitter()
                .AddHeader("Gameplay")
                .AddSlider(OptGameSpeed, "Game speed", 0, 500, 100,
                    GameplayCheats.SetGameSpeed, LabelPercent)
                .AddButton("Skip 1 hour  →", () => GameplayCheats.SkipTime("1h"))
                .AddButton("Skip 8 hours  →", () => GameplayCheats.SkipTime("8h"))
                .AddButton("Skip 1 day  →", () => GameplayCheats.SkipTime("1d"))
                .AddButton("Complete current objective  →", GameplayCheats.CompleteObjective)
                .AddButton("Complete current quest  →", GameplayCheats.CompleteQuest)
                .AddButton("Spawn 10 customers here  →", () => GameplayCheats.SpawnCustomers(10))
                // The game's Toggle* commands flip state with no getter, so these stay
                // buttons: an option toggle would flip them again on every panel rebuild.
                .AddButton("Toggle traffic  →", GameplayCheats.ToggleTraffic)
                .AddButton("Toggle pedestrians  →", GameplayCheats.TogglePedestrians)
                .AddButton("Toggle seasonal item limits  →", GameplayCheats.ToggleSeasonRestrictions)
                .AddButton("Toggle invincibility  →", GameplayCheats.ToggleInvincibility)

                .AddSplitter()
                .AddHeader("Teleport")
                .AddButton("Go to map destination  →", TeleportCheats.ToDestination)
                .AddButton("Go inside map destination  →", TeleportCheats.InsideDestination)
                .AddButton("Go to quest target  →", TeleportCheats.ToQuestTarget)
                .AddButton("Go to the casino  →", GameplayCheats.GoToCasino)
                // Phone only: naming and choosing a waypoint both need text entry.
                .AddCustom(new InlineUiOption(PhoneApp.BuildWaypoints))

                .AddSplitter()
                .AddHeader("Utility")
                .AddCustom(new InlineUiOption(PhoneApp.BuildItemSpawner))
                // Stores only. Read straight from PlayerPrefs by ApplySavedSettings
                // before any other setting is looked at.
                .AddToggle(OptApplyOnLoad, "Apply my settings when the game loads", true,
                    v => { _applyOnLoad = v; LogSetting("Apply settings on load", v); })
                .AddButton("Reset all Bigamst Trainer settings  →", ResetSettings)

                // Renders nothing. Must stay last: its SpawnUi is the signal that the
                // panel finished rebuilding, and by then every button above exists.
                .AddCustom(new ButtonLabelFixer.HookOption());

            BuiltOptions = options;
            ModId = context.ModId;
            OptionsService.Register(context.ModId, options);

            ApplySavedSettings(options, context.ModId);

            // Always, saved settings or not: car tuning cannot outlive a city load.
            ResetTuningSliders();

            UnityLifecycleProvider.OnUpdate += OnUpdate;
            _tickSubscribed = true;

            _log.Info("Bigamst Trainer loaded.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Puts last session's saved settings into force at load.
        ///
        /// The game only invokes an option's callback when a control for that option is
        /// built, and it restores the stored value with SetIsOnWithoutNotify. So until the
        /// player opened Options or the phone, every setting saved last session was shown
        /// as on while doing nothing at all — the box was reporting what was saved, not
        /// what was active. Read the same PlayerPrefs keys the game reads and invoke the
        /// same callbacks, so the two always agree.
        ///
        /// Invoking a callback here is safe precisely because the panel does the same on
        /// every rebuild: any handler that could not survive being called twice would
        /// already be broken.
        /// </summary>
        private void ApplySavedSettings(ModOptions options, string modId)
        {
            // Opting out is itself a saved setting, so it has to be read the same way,
            // before anything else is considered.
            if (UnityEngine.PlayerPrefs.GetInt($"m:{modId}:{OptApplyOnLoad}", 1) == 0)
            {
                ForgetSavedSettings(options, modId);
                return;
            }

            int applied = 0;

            foreach (ModOption option in options.Options)
            {
                if (string.IsNullOrEmpty(option.Id) || option.Id == OptApplyOnLoad ||
                    IsTuningOption(option.Id))
                {
                    continue;
                }

                // Same key the game builds in ModOptionPrefs, which is internal to it.
                string key = $"m:{modId}:{option.Id}";

                try
                {
                    switch (option)
                    {
                        case ToggleOption toggle:
                            bool on = UnityEngine.PlayerPrefs.GetInt(key, toggle.DefaultValue ? 1 : 0) != 0;
                            if (on != toggle.DefaultValue)
                            {
                                toggle.OnValueChanged?.Invoke(on);
                                applied++;
                            }
                            break;

                        case SliderOption slider:
                            int value = UnityEngine.PlayerPrefs.GetInt(key, slider.DefaultValue);
                            if (value != slider.DefaultValue)
                            {
                                slider.OnValueChanged?.Invoke(value);
                                applied++;
                            }
                            break;

                        case DropdownOption dropdown:
                            int index = UnityEngine.PlayerPrefs.GetInt(key, dropdown.DefaultIndex);
                            if (index != dropdown.DefaultIndex)
                            {
                                dropdown.OnValueChanged?.Invoke(index);
                                applied++;
                            }
                            break;
                    }
                }
                catch (System.Exception exception)
                {
                    _log?.Warn($"Could not restore '{option.Id}': {exception.Message}");
                }
            }

            if (applied > 0)
            {
                _log?.Info($"Restored {applied} saved setting(s).");
            }
        }

        /// <summary>
        /// Clears every saved value so a session that does not apply them also does not
        /// display them.
        ///
        /// The game reads each control's stored value straight back into the UI, so
        /// leaving the values in place while refusing to act on them would show ticked
        /// boxes for cheats that are not running — the same lie, in the other direction,
        /// as the bug this whole mechanism exists to fix. "Do not apply on load" therefore
        /// means a genuinely clean start: nothing set, nothing shown as set.
        /// </summary>
        private void ForgetSavedSettings(ModOptions options, string modId)
        {
            int cleared = 0;

            foreach (ModOption option in options.Options)
            {
                // The opt-out itself has to survive, or it would switch itself back on.
                if (string.IsNullOrEmpty(option.Id) || option.Id == OptApplyOnLoad)
                {
                    continue;
                }

                if (!(option is ToggleOption || option is SliderOption || option is DropdownOption))
                {
                    continue;
                }

                string key = $"m:{modId}:{option.Id}";
                if (UnityEngine.PlayerPrefs.HasKey(key))
                {
                    UnityEngine.PlayerPrefs.DeleteKey(key);
                    cleared++;
                }
            }

            if (cleared > 0)
            {
                UnityEngine.PlayerPrefs.Save();
            }

            _log?.Info($"Starting clean: \"Apply my settings when the game loads\" is off, " +
                       $"so {cleared} saved setting(s) were cleared back to their defaults.");
        }

        public override Task OnUnloadAsync()
        {
            if (_tickSubscribed)
            {
                UnityLifecycleProvider.OnUpdate -= OnUpdate;
                _tickSubscribed = false;
            }

            BuiltOptions = null;
            ModId = null;
            _log?.Info("Bigamst Trainer unloading.");
            BusinessCheats.Reset();
            GameplayCheats.Reset();
            PhoneApp.Reset();
            TeleportCheats.Reset();
            LastApplied.Clear();
            ButtonLabelFixer.Reset();
            _log = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Per-frame enforcement for the "keep" toggles. SaveGameManager.Current is null
        /// outside an active save, and this still ticks while the game sits in menus.
        /// </summary>
        private void OnUpdate()
        {
            // Two field reads unless the options panel was rebuilt since the last frame.
            ButtonLabelFixer.ProcessPendingRebuild();

            // One bool check once the phone app is installed.
            PhoneApp.Tick();

            // One int comparison once the requested speed has taken effect.
            GameplayCheats.EnsureMovementSpeed();

            // One string comparison a frame, so tuning can follow the car you get into.
            string vehicleId = SaveGameManager.Current?.ActiveVehicleId;
            if (vehicleId != _lastVehicleId)
            {
                _lastVehicleId = vehicleId;
                OnEnteredVehicle(vehicleId);
            }

            if (_pendingTuning != null)
            {
                RetryPendingTuning();
            }

            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                return;
            }

            // Assign only when the value actually differs. These fields are read every
            // frame by UI and simulation code, so needless writes are pure churn.
            if (_keepEnergy && game.Energy < StatMax)
            {
                game.Energy = StatMax;
            }

            if (_keepHunger && game.Hunger < StatMax)
            {
                game.Hunger = StatMax;
            }

            if (_keepHappiness && game.Happiness < StatMax)
            {
                game.Happiness = StatMax;
            }

            if (_moneyFloor > 0f && game.Money < _moneyFloor)
            {
                game.Money = _moneyFloor;
            }

            if (_freezeClock)
            {
                game.Day = _frozenDay;
                game.Hour = _frozenHour;
                game.Minute = _frozenMinute;
            }

            if (_keepStaffSatisfied)
            {
                _staffSweepTimer += Time.unscaledDeltaTime;
                if (_staffSweepTimer >= StaffSweepIntervalSeconds)
                {
                    _staffSweepTimer = 0f;
                    SatisfyAllEmployees(quiet: true);
                }
            }

            if (_autoRestock || _autoClean)
            {
                // Walking every container in every property is far heavier than the
                // employee pass, so it runs on a much longer interval.
                _businessSweepTimer += Time.unscaledDeltaTime;
                if (_businessSweepTimer >= BusinessSweepIntervalSeconds)
                {
                    _businessSweepTimer = 0f;

                    if (_autoRestock)
                    {
                        BusinessCheats.RestockEverything(quiet: true);
                    }

                    if (_autoClean)
                    {
                        BusinessCheats.CleanEverything(quiet: true);
                    }
                }
            }
        }

        /// <summary>
        /// Applies a change to the save's GameVariables, the game's own sandbox config.
        /// Null outside an active save, so every caller funnels through here.
        ///
        /// The options panel re-invokes every callback whenever it is rebuilt, so this
        /// logs only when a value actually changes — otherwise simply opening Options
        /// would write a dozen lines.
        /// </summary>
        private static void WithVariables(string name, object value, System.Action<GameVariables> apply)
        {
            GameVariables variables = SaveGameManager.Current?.gameVariables;
            if (variables == null)
            {
                return;
            }

            apply(variables);

            if (!LastApplied.TryGetValue(name, out object previous) || !Equals(previous, value))
            {
                LastApplied[name] = value;
                _log?.Info($"{name} = {value}");
            }
        }

        /// <summary>
        /// Records a state toggle that is enforced by this mod rather than by
        /// GameVariables, so flipping it leaves a trace in the log.
        /// </summary>
        private static void LogSetting(string name, object value)
        {
            if (!LastApplied.TryGetValue(name, out object previous) || !Equals(previous, value))
            {
                LastApplied[name] = value;
                _log?.Info($"{name} = {value}");
            }
        }



        private void OnFreezeClockChanged(bool enabled)
        {
            _freezeClock = enabled;
            if (!enabled)
            {
                return;
            }

            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                _freezeClock = false;
                _log?.Warn("Freeze clock ignored: no save is loaded.");
                return;
            }

            _frozenDay = game.Day;
            _frozenHour = game.Hour;
            _frozenMinute = game.Minute;
            _log?.Info($"Clock frozen at day {_frozenDay}, {_frozenHour:00}:{(int)_frozenMinute:00}.");
        }

        private void JumpToTargetHour()
        {
            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                _log?.Warn("Time jump ignored: no save is loaded.");
                return;
            }

            // Jumping backwards past midnight would otherwise rewind the calendar
            // relative to everything scheduled for today.
            if (_targetHour < game.Hour)
            {
                game.Day++;
            }

            game.Hour = _targetHour;
            game.Minute = 0f;

            // Otherwise the freeze would immediately undo the jump.
            if (_freezeClock)
            {
                _frozenDay = game.Day;
                _frozenHour = _targetHour;
                _frozenMinute = 0f;
            }

            _log?.Info($"Time set to day {game.Day}, {_targetHour:00}:00.");
        }

        // Exposed so the phone panel drives the same code paths as the Options menu
        // rather than duplicating them.
        internal static readonly System.Action ClearLoansAction = ClearLoans;
        internal static readonly System.Action RestoreAllStatsAction = RestoreAllStats;
        internal static readonly System.Action SatisfyAllEmployeesAction = () => SatisfyAllEmployees();
        internal static readonly System.Action ClearAbsencesAction = ClearAbsences;
        internal static readonly System.Action ServiceAllVehiclesAction = ServiceAllVehicles;
        internal static readonly System.Action DefeatAllRivalsAction = DefeatAllRivals;

        private void OnMoneyAmountChanged(int index)
        {
            if (index >= 0 && index < MoneyAmounts.Length)
            {
                _selectedMoneyAmount = MoneyAmounts[index];
            }
        }

        /// <summary>
        /// Deletes this mod's saved option values, so both surfaces fall back to their
        /// declared defaults on their next rebuild.
        ///
        /// OptionsService.ResetAllToDefaults would also clear every other mod's settings,
        /// so the keys are removed directly instead. The format is ModOptionPrefs's:
        /// "m:{modId}:{optionId}".
        /// </summary>
        internal static void ClearSavedOptionValues()
        {
            if (BuiltOptions == null || string.IsNullOrEmpty(ModId))
            {
                return;
            }

            int cleared = 0;
            foreach (ModOption option in BuiltOptions.Options)
            {
                if (option is IPersistableOption persistable &&
                    !string.IsNullOrEmpty(persistable.Id))
                {
                    UnityEngine.PlayerPrefs.DeleteKey("m:" + ModId + ":" + persistable.Id);
                    cleared++;
                }
            }

            UnityEngine.PlayerPrefs.Save();
            _log?.Info($"Cleared {cleared} saved setting(s). Reopen to see the defaults.");
        }

        private static void ResetSettings() => PhoneApp.ResetToDefaults();

        /// <summary>
        /// Pushes every tuning value to the vehicle the player is currently in. The game's
        /// commands each report "You need to be inside a vehicle" themselves, and some
        /// vehicles legitimately lack the speed limiter or damage modules.
        /// </summary>
        /// <summary>
        /// Restores a car's tuning when the player gets into it, and points the sliders at
        /// whatever that car is currently set to.
        ///
        /// Tuning lives on the spawned car and nothing about it reaches the save file, so
        /// without this every car is stock again after a city load. Percentages make
        /// reapplying safe: a fresh car is stock, so "138% of stock" lands on the same
        /// figure every time rather than compounding.
        /// </summary>
        private void OnEnteredVehicle(string vehicleId)
        {
            _pendingTuning = null;

            if (string.IsNullOrEmpty(vehicleId))
            {
                return;
            }

            int[] tuning = LoadVehicleTuning(vehicleId);
            if (tuning == null)
            {
                // An untuned car: the sliders should say so rather than describing the
                // last car the player was in.
                ResetTuningSliders();
                PhoneApp.RefreshIfOpen();
                return;
            }

            _carSpeed = StoreSlider(OptCarSpeed, tuning[0]);
            _carPower = StoreSlider(OptCarPower, tuning[1]);
            _carBrakes = StoreSlider(OptCarBrakes, tuning[2]);
            _carTurn = StoreSlider(OptCarTurn, tuning[3]);
            _carDamage = StoreSlider(OptCarDamage, tuning[4]);
            UnityEngine.PlayerPrefs.Save();
            PhoneApp.RefreshIfOpen();

            if (!_applyOnLoad)
            {
                _log?.Info("This car has saved tuning, left unapplied because " +
                           "\"Apply my settings when the game loads\" is off.");
                return;
            }

            // Not applied here: on a city load the player can already be in a car that the
            // game has not spawned yet, and tuning something that does not exist fails.
            _pendingTuning = tuning;
            _pendingTuningSeconds = 0f;
        }

        /// <summary>
        /// Keeps trying to restore a car's tuning until the car exists.
        ///
        /// Vehicles are spawned some frames after a city finishes loading, so the tuning
        /// for the car the player is sitting in cannot be applied at the moment the save
        /// comes up. Retrying quietly avoids both losing the tuning and telling the player
        /// to get into a car they are already driving.
        /// </summary>
        private void RetryPendingTuning()
        {
            _pendingTuningSeconds += Time.unscaledDeltaTime;

            if (GameplayCheats.ApplyTuningPercent(_pendingTuning[0], _pendingTuning[1],
                                                  _pendingTuning[2], _pendingTuning[3],
                                                  _pendingTuning[4], quiet: true))
            {
                _log?.Info("Restored this car's saved tuning.");
                _pendingTuning = null;
                return;
            }

            if (_pendingTuningSeconds > PendingTuningTimeoutSeconds)
            {
                _log?.Warn("Gave up restoring this car's saved tuning: the car never " +
                           "became available.");
                _pendingTuning = null;
            }
        }

        /// <summary>Where a car's tuning is kept. Our own key, not the game's save.</summary>
        private string VehicleTuningKey(string vehicleId) => $"m:{ModId}:tune:{vehicleId}";

        private void SaveVehicleTuning(string vehicleId)
        {
            if (string.IsNullOrEmpty(vehicleId))
            {
                return;
            }

            string value = $"{_carSpeed};{_carPower};{_carBrakes};{_carTurn};{_carDamage}";
            UnityEngine.PlayerPrefs.SetString(VehicleTuningKey(vehicleId), value);
            UnityEngine.PlayerPrefs.Save();
        }

        private void ForgetVehicleTuning(string vehicleId)
        {
            if (!string.IsNullOrEmpty(vehicleId))
            {
                UnityEngine.PlayerPrefs.DeleteKey(VehicleTuningKey(vehicleId));
                UnityEngine.PlayerPrefs.Save();
            }
        }

        private int[] LoadVehicleTuning(string vehicleId)
        {
            string value = UnityEngine.PlayerPrefs.GetString(VehicleTuningKey(vehicleId), null);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            string[] parts = value.Split(';');
            if (parts.Length != 5)
            {
                return null;
            }

            int[] tuning = new int[5];
            for (int index = 0; index < 5; index++)
            {
                if (!int.TryParse(parts[index], out tuning[index]))
                {
                    return null;
                }
            }

            return tuning;
        }

        /// <summary>The five sliders that describe car tuning.</summary>
        private static bool IsTuningOption(string optionId)
        {
            return optionId == OptCarSpeed || optionId == OptCarPower ||
                   optionId == OptCarBrakes || optionId == OptCarTurn ||
                   optionId == OptCarDamage;
        }

        /// <summary>
        /// Returns the tuning sliders to 100%, which is what the cars themselves are.
        ///
        /// Tuning is written onto the spawned car and nowhere else — VehicleInstance saves
        /// position, colour and cargo, but nothing about the engine — so every car is back
        /// to stock as soon as a city loads. Sliders left showing last session's 218% would
        /// therefore describe a car that does not exist, and Undo would rightly report
        /// there was nothing to undo.
        /// </summary>
        private void ResetTuningSliders()
        {
            _carSpeed = StoreSlider(OptCarSpeed, 100);
            _carPower = StoreSlider(OptCarPower, 100);
            _carBrakes = StoreSlider(OptCarBrakes, 100);
            _carTurn = StoreSlider(OptCarTurn, 100);
            _carDamage = StoreSlider(OptCarDamage, 100);
            UnityEngine.PlayerPrefs.Save();
        }

        /// <summary>
        /// Puts the car back to stock and the sliders back to 100% together.
        ///
        /// Restoring only the car left the sliders showing the tuning that had just been
        /// undone, so the next Apply silently put it all back.
        /// </summary>
        private void UndoCarTuning()
        {
            if (!GameplayCheats.ResetCarTuning())
            {
                return;
            }

            ForgetVehicleTuning(SaveGameManager.Current?.ActiveVehicleId);

            _carSpeed = StoreSlider(OptCarSpeed, 100);
            _carPower = StoreSlider(OptCarPower, 100);
            _carBrakes = StoreSlider(OptCarBrakes, 100);
            _carTurn = StoreSlider(OptCarTurn, 100);
            _carDamage = StoreSlider(OptCarDamage, 100);
            UnityEngine.PlayerPrefs.Save();

            // Last, once the new values are stored: the rebuild reads them, so refreshing
            // any earlier just redraws what was there before.
            PhoneApp.RefreshIfOpen();
        }

        /// <summary>Walking speed straight back to normal, without hunting for 100%.</summary>
        private void ResetMovementSpeed()
        {
            StoreSlider(OptMoveSpeed, 100);
            UnityEngine.PlayerPrefs.Save();
            GameplayCheats.SetMovementSpeed(100);
            PhoneApp.RefreshIfOpen();
        }

        /// <summary>
        /// Writes a value where the matching slider reads it from, so the control shows it
        /// the next time the panel is built.
        /// </summary>
        private int StoreSlider(string optionId, int value)
        {
            UnityEngine.PlayerPrefs.SetInt($"m:{ModId}:{optionId}", value);
            return value;
        }

        /// <summary>
        /// Tunes the car being driven, relative to its own stock figures.
        /// </summary>
        private void ApplyCarTuning()
        {
            GameplayCheats.ApplyTuningPercent(_carSpeed, _carPower, _carBrakes,
                                              _carTurn, _carDamage);

            // Kept so the car is still tuned after a city load, which wipes it otherwise.
            SaveVehicleTuning(SaveGameManager.Current?.ActiveVehicleId);
        }

        private static void AddMoney(float amount)
        {
            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                _log?.Warn("Money change ignored: no save is loaded.");
                return;
            }

            game.Money += amount;
            _log?.Info($"Money {amount:+#,##0;-#,##0} -> {game.Money:N0}");
        }

        private static void ClearLoans()
        {
            GameInstance game = SaveGameManager.Current;
            if (game?.Loans == null || game.Loans.Count == 0)
            {
                _log?.Info("No outstanding loans.");
                return;
            }

            // Zero the balances rather than dropping the entries, so anything still
            // holding a reference to a Loan keeps a valid object.
            int count = game.Loans.Count;
            float cleared = 0f;
            foreach (Loan loan in game.Loans)
            {
                cleared += loan.remainingAmount;
                loan.remainingAmount = 0f;
                loan.dailyInterest = 0;
                loan.dailyPayment = 0;
            }

            _log?.Info($"Cleared {cleared:N0} of debt across {count} loan(s).");
        }

        private static void RestoreAllStats()
        {
            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                _log?.Warn("Restore ignored: no save is loaded.");
                return;
            }

            game.Energy = StatMax;
            game.Hunger = StatMax;
            game.Happiness = StatMax;
            _log?.Info("Energy, hunger and happiness restored to full.");
        }

        private static void SatisfyAllEmployees() => SatisfyAllEmployees(quiet: false);

        private static void SatisfyAllEmployees(bool quiet)
        {
            GameInstance game = SaveGameManager.Current;
            if (game?.EmployeeInstances == null)
            {
                return;
            }

            int changed = 0;
            foreach (EmployeeInstance employee in game.EmployeeInstances)
            {
                if (employee.satisfaction < StatMax)
                {
                    employee.satisfaction = StatMax;
                    changed++;
                }
            }

            if (!quiet)
            {
                _log?.Info($"Satisfied {changed} employee(s).");
            }
        }

        private static void ClearAbsences()
        {
            GameInstance game = SaveGameManager.Current;
            if (game?.EmployeeInstances == null)
            {
                return;
            }

            int changed = 0;
            foreach (EmployeeInstance employee in game.EmployeeInstances)
            {
                if (!employee.isAbsent && employee.nextSickDay == 0 && !employee.hasSendQuitWarning)
                {
                    continue;
                }

                employee.isAbsent = false;
                employee.nextSickDay = 0;
                employee.hasSendQuitWarning = false;
                changed++;
            }

            _log?.Info($"Cleared absences for {changed} employee(s).");
        }

        private static void ServiceAllVehicles()
        {
            GameInstance game = SaveGameManager.Current;
            if (game?.VehicleInstances == null)
            {
                return;
            }

            int count = 0;
            foreach (VehicleInstance vehicle in game.VehicleInstances)
            {
                vehicle.damage = 0f;
                vehicle.dirtiness = 0f;
                vehicle.deformations?.Clear();

                // Fuel capacity is per vehicle type, not a global constant.
                VehicleType type = VehicleTypeHelper.GetVehicleType(vehicle.vehicleTypeName);
                if (type != null && type.maxFuel > 0f)
                {
                    vehicle.fuel = type.maxFuel;
                }

                count++;
            }

            _log?.Info($"Repaired, refuelled and cleaned {count} vehicle(s).");
        }

        private static void ClearParkingFines()
        {
            GameInstance game = SaveGameManager.Current;
            if (game?.VehicleInstances == null)
            {
                return;
            }

            int count = 0;
            float cleared = 0f;
            foreach (VehicleInstance vehicle in game.VehicleInstances)
            {
                if (vehicle.unpaidParkingAmount <= 0f && (vehicle.parkingTickets?.Count ?? 0) == 0)
                {
                    continue;
                }

                cleared += vehicle.unpaidParkingAmount;
                vehicle.unpaidParkingAmount = 0f;
                vehicle.parkingTickets?.Clear();
                count++;
            }

            _log?.Info($"Cleared {cleared:N0} in fines across {count} vehicle(s).");
        }

        private static void DefeatAllRivals()
        {
            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                _log?.Warn("Defeat rivals ignored: no save is loaded.");
                return;
            }

            // Setting isDefeated by hand is not enough: the rival keeps trading and
            // attacking. RivalsHelper.DefeatRival also runs OnRivalDefeat, which shuts
            // down their businesses, sells their real estate, stops special-rival attacks
            // and clears what they own. Let the game do its own bookkeeping.
            int defeated = 0;
            int failed = 0;

            foreach (RivalData rival in RivalsHelper.GetAllRivalData())
            {
                if (rival == null)
                {
                    continue;
                }

                try
                {
                    RivalsHelper.DefeatRival(rival);
                    defeated++;
                }
                catch (System.Exception exception)
                {
                    // One rival in a bad state must not abort the rest.
                    failed++;
                    _log?.Warn($"Could not defeat rival '{rival.id}': {exception.Message}");
                }
            }

            int stillActive = 0;
            if (game.specialRivalStates != null)
            {
                foreach (SpecialRivalState state in game.specialRivalStates)
                {
                    if (state != null && state.isActive && !state.isDefeated)
                    {
                        stillActive++;
                    }
                }
            }

            _log?.Info($"Processed {defeated} rival(s), {failed} failed. " +
                       $"Special rivals still active: {stillActive}.");
        }
    }
}
