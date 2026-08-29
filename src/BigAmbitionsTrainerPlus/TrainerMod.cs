using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using BigAmbitions.Mods;
using BigAmbitions.Rivals;
using Entities;
using UnityEngine;
using Vehicles.VehicleTypes;

[assembly: RegisterModClass(typeof(BigAmbitionsTrainerPlus.TrainerMod))]

namespace BigAmbitionsTrainerPlus
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
        private const string OptMoneyAmount     = "trainerplus.money.amount";
        private const string OptMoneyFloor      = "trainerplus.money.floor";
        private const string OptTaxPercent      = "trainerplus.econ.tax";
        private const string OptSalaryMult      = "trainerplus.econ.salary_mult";
        private const string OptMarketMult      = "trainerplus.econ.market_mult";
        private const string OptInterestMult    = "trainerplus.econ.interest_mult";
        private const string OptSellMult        = "trainerplus.econ.sell_mult";
        private const string OptNoTradeLimits   = "trainerplus.econ.no_trade_limits";
        private const string OptAllImports      = "trainerplus.econ.all_imports";
        private const string OptAllContacts     = "trainerplus.econ.all_contacts";
        private const string OptAllCourses      = "trainerplus.econ.all_courses";
        private const string OptKeepEnergy      = "trainerplus.player.keep_energy";
        private const string OptKeepHunger      = "trainerplus.player.keep_hunger";
        private const string OptKeepHappiness   = "trainerplus.player.keep_happiness";
        private const string OptNoAging         = "trainerplus.player.no_aging";
        private const string OptNoEnergyDrain   = "trainerplus.player.no_energy";
        private const string OptAutoRestock     = "trainerplus.business.auto_restock";
        private const string OptAutoClean       = "trainerplus.business.auto_clean";
        private const string OptFreeRent        = "trainerplus.business.free_rent";
        private const string OptKeepStaffHappy  = "trainerplus.employee.keep_satisfied";
        private const string OptNoVehicleDamage = "trainerplus.vehicle.no_damage";
        private const string OptNoVehicleFuel   = "trainerplus.vehicle.no_fuel";
        private const string OptRivalDifficulty = "trainerplus.rivals.difficulty";
        private const string OptFreezeClock     = "trainerplus.time.freeze";
        private const string OptSetHour         = "trainerplus.time.hour";

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

        /// <summary>How often to look for freshly spawned option buttons to relabel.</summary>
        private const float UiSweepIntervalSeconds = 0.5f;

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

        /// <summary>Choices for the money amount dropdown, paired with <see cref="MoneyAmounts"/>.</summary>
        private static readonly string[] MoneyAmountChoices =
        {
            "$1,000", "$10,000", "$100,000", "$1,000,000", "$10,000,000", "$100,000,000"
        };

        private static readonly float[] MoneyAmounts =
        {
            1_000f, 10_000f, 100_000f, Million, 10f * Million, 100f * Million
        };

        private static IModLogger _log;

        private bool _keepEnergy;
        private bool _keepHunger;
        private bool _keepHappiness;
        private bool _keepStaffSatisfied;
        private bool _autoRestock;
        private bool _autoClean;

        /// <summary>Money is topped back up to this whenever it drops below. 0 disables.</summary>
        private float _moneyFloor;

        private float _selectedMoneyAmount = MoneyAmounts[1];

        private bool _freezeClock;
        private int _targetHour = 8;
        private int _frozenDay;
        private int _frozenHour;
        private float _frozenMinute;

        private float _staffSweepTimer;
        private float _businessSweepTimer;
        private float _uiSweepTimer;
        private bool _tickSubscribed;

        public override Task OnLoadAsync(ModContext context)
        {
            _log = context.Logger;
            BusinessCheats.Initialize(_log);

            var options = new ModOptions()

                .AddHeader("Trainer Plus — Money")
                .AddDropdown(OptMoneyAmount, "Amount", MoneyAmountChoices, 1, OnMoneyAmountChanged)
                .AddButton("Add the amount above  →", () => AddMoney(_selectedMoneyAmount))
                .AddButton("Subtract the amount above  →", () => AddMoney(-_selectedMoneyAmount))
                .AddSlider(OptMoneyFloor, "Never drop below", 0, 100, 0,
                    value => _moneyFloor = value * Million, LabelDollarsM)
                .AddButton("Pay off all loans  →", ClearLoans)
                .AddSplitter()

                .AddHeader("Trainer Plus — Economy")
                .AddSlider(OptTaxPercent, "Tax rate", 0, 50, 10,
                    value => WithVariables(v => v.taxPercentage = value), LabelPercent)
                .AddSlider(OptSalaryMult, "Employee wages", 0, 200, 100,
                    value => WithVariables(v => v.employeeHourlySalaryMultiplier = value / 100f), LabelPercent)
                .AddSlider(OptMarketMult, "Market prices", 0, 300, 100,
                    value => WithVariables(v => v.marketPriceMultiplier = value / 100f), LabelPercent)
                .AddSlider(OptInterestMult, "Bank interest", 0, 200, 100,
                    value => WithVariables(v => v.bankInterestMultiplier = value / 100f), LabelPercent)
                .AddSlider(OptSellMult, "Selling return", 0, 300, 75,
                    value => WithVariables(v => v.sellingMultiplier = value / 100f), LabelPercent)
                .AddToggle(OptNoTradeLimits, "No wholesale or import limits", false,
                    value => WithVariables(v => v.disableWholesaleAndImportLimits = value))
                .AddToggle(OptAllImports, "All products available from importers", false,
                    value => WithVariables(v => v.allProductsAvailableFromImporters = value))
                .AddToggle(OptAllContacts, "All contacts unlocked", false,
                    value => WithVariables(v => v.allContactsUnlocked = value))
                .AddToggle(OptAllCourses, "All courses unlocked", false,
                    value => WithVariables(v => v.allCoursesUnlocked = value))
                .AddSplitter()

                .AddHeader("Trainer Plus — Player")
                .AddToggle(OptKeepEnergy,    "Keep energy full",    false, v => _keepEnergy = v)
                .AddToggle(OptKeepHunger,    "Keep hunger full",    false, v => _keepHunger = v)
                .AddToggle(OptKeepHappiness, "Keep happiness full", false, v => _keepHappiness = v)
                .AddToggle(OptNoAging, "Disable aging", false,
                    value => WithVariables(v => v.disableAging = value))
                .AddToggle(OptNoEnergyDrain, "Disable energy system entirely", false,
                    value => WithVariables(v => v.disableEnergy = value))
                .AddButton("Restore energy, hunger and happiness  →", RestoreAllStats)
                .AddSplitter()

                .AddHeader("Trainer Plus — Businesses")
                .AddButton("Restock every shelf and fridge  →", () => BusinessCheats.RestockEverything())
                .AddToggle(OptAutoRestock, "Keep everything restocked", false, v => _autoRestock = v)
                .AddButton("Mark all stock as paid for  →", BusinessCheats.MarkStockPaid)
                .AddButton("Remove all dirt  →", () => BusinessCheats.CleanEverything())
                .AddToggle(OptAutoClean, "Keep everything spotless", false, v => _autoClean = v)
                .AddToggle(OptFreeRent, "No rent on owned property", false, BusinessCheats.SetFreeRent)
                .AddSplitter()

                .AddHeader("Trainer Plus — Employees")
                .AddToggle(OptKeepStaffHappy, "Keep all employees fully satisfied", false,
                    v => _keepStaffSatisfied = v)
                .AddButton("Satisfy all employees now  →", SatisfyAllEmployees)
                .AddButton("Clear absences and sick days  →", ClearAbsences)
                .AddButton("Max out every employee skill  →", BusinessCheats.MaxEmployeeSkills)
                .AddSplitter()

                .AddHeader("Trainer Plus — Vehicles")
                .AddToggle(OptNoVehicleDamage, "Disable vehicle damage", false,
                    value => WithVariables(v => v.disableVehicleDamage = value))
                .AddToggle(OptNoVehicleFuel, "Disable fuel consumption", false,
                    value => WithVariables(v => v.disableVehicleFuel = value))
                .AddButton("Repair, refuel and clean all  →", ServiceAllVehicles)
                .AddButton("Clear parking tickets and fines  →", ClearParkingFines)
                .AddSplitter()

                .AddHeader("Trainer Plus — Rivals")
                .AddSlider(OptRivalDifficulty, "Rival difficulty", 0, 200, 100,
                    value => WithVariables(v => v.rivalsDifficultyMultiplier = value / 100f), LabelPercent)
                .AddButton("Defeat all rivals  →", DefeatAllRivals)
                .AddSplitter()

                .AddHeader("Trainer Plus — Time")
                .AddToggle(OptFreezeClock, "Freeze the clock", false, OnFreezeClockChanged)
                // The slider only records the target. Applying it directly would move the
                // clock every time the panel is built, because ModOptionsSliderControl
                // re-invokes OnValueChanged during Initialize.
                .AddSlider(OptSetHour, "Target hour", 0, 23, 8, value => _targetHour = value, LabelHour)
                .AddButton("Jump to the target hour  →", JumpToTargetHour);

            OptionsService.Register(context.ModId, options);

            UnityLifecycleProvider.OnUpdate += OnUpdate;
            _tickSubscribed = true;

            _log.Info("Trainer Plus loaded.");
            return Task.CompletedTask;
        }

        public override Task OnUnloadAsync()
        {
            if (_tickSubscribed)
            {
                UnityLifecycleProvider.OnUpdate -= OnUpdate;
                _tickSubscribed = false;
            }

            _log?.Info("Trainer Plus unloading.");
            BusinessCheats.Reset();
            _log = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Per-frame enforcement for the "keep" toggles. SaveGameManager.Current is null
        /// outside an active save, and this still ticks while the game sits in menus.
        /// </summary>
        private void OnUpdate()
        {
            // Cheap and throttled, and it has to run whether or not a save is loaded,
            // since the options panel is reachable from the menus too.
            _uiSweepTimer += Time.unscaledDeltaTime;
            if (_uiSweepTimer >= UiSweepIntervalSeconds)
            {
                _uiSweepTimer = 0f;
                ButtonLabelFixer.Sweep();
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
        /// </summary>
        private static void WithVariables(System.Action<GameVariables> apply)
        {
            GameVariables variables = SaveGameManager.Current?.gameVariables;
            if (variables == null)
            {
                return;
            }

            apply(variables);
        }

        private void OnMoneyAmountChanged(int index)
        {
            if (index >= 0 && index < MoneyAmounts.Length)
            {
                _selectedMoneyAmount = MoneyAmounts[index];
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
            if (game?.specialRivalStates == null)
            {
                return;
            }

            int changed = 0;
            foreach (SpecialRivalState rival in game.specialRivalStates)
            {
                if (rival.isDefeated)
                {
                    continue;
                }

                rival.isDefeated = true;
                rival.isActive = false;
                changed++;
            }

            _log?.Info($"Defeated {changed} rival(s).");
        }
    }
}
