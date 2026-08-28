using System;
using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using BigAmbitions.Mods;

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
        // Option ids are persisted by the game as PlayerPrefs keys "m:{modId}:{optionId}",
        // so they must stay stable across releases or saved values are silently dropped.
        private const string OptKeepEnergy    = "trainerplus.player.keep_energy";
        private const string OptKeepHunger    = "trainerplus.player.keep_hunger";
        private const string OptKeepHappiness = "trainerplus.player.keep_happiness";
        private const string OptMoneyFloor    = "trainerplus.money.floor";

        /// <summary>
        /// Energy, Hunger and Happiness are normalised 0..100 by EnergySettings
        /// (maxEnergyHungerHappinessValue = 100f, minEnergyHungerHappinessValue = 0f).
        /// </summary>
        private const float StatMax = 100f;

        private static IModLogger _log;

        private bool _keepEnergy;
        private bool _keepHunger;
        private bool _keepHappiness;

        /// <summary>Money is topped back up to this whenever it drops below. 0 disables.</summary>
        private float _moneyFloor;

        private bool _tickSubscribed;

        public override Task OnLoadAsync(ModContext context)
        {
            _log = context.Logger;

            var options = new ModOptions()
                .AddHeader("Trainer Plus")
                .AddSplitter()

                .AddHeader("Money")
                .AddButton("Add $10,000",    () => AddMoney(10_000f))
                .AddButton("Add $100,000",   () => AddMoney(100_000f))
                .AddButton("Add $1,000,000", () => AddMoney(1_000_000f))
                // Slider values are int, so this is expressed in millions.
                .AddSlider(OptMoneyFloor, "Keep at least ($M)", 0, 100, 0,
                    value => _moneyFloor = value * 1_000_000f)
                .AddSplitter()

                .AddHeader("Player")
                .AddToggle(OptKeepEnergy,    "Keep energy full",    false, v => _keepEnergy = v)
                .AddToggle(OptKeepHunger,    "Keep hunger full",    false, v => _keepHunger = v)
                .AddToggle(OptKeepHappiness, "Keep happiness full", false, v => _keepHappiness = v)
                .AddButton("Restore all now", RestoreAllStats);

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
            _log = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Per-frame enforcement for the "keep" toggles. SaveGameManager.Current is null
        /// outside an active save, and this still ticks while the game sits in menus.
        /// </summary>
        private void OnUpdate()
        {
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
        }

        private static void AddMoney(float amount)
        {
            GameInstance game = SaveGameManager.Current;
            if (game == null)
            {
                _log?.Warn("Add money ignored: no save is loaded.");
                return;
            }

            game.Money += amount;
            _log?.Info($"Money +{amount:N0} -> {game.Money:N0}");
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
    }
}
