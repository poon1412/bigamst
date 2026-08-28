using System.Threading.Tasks;
using BAModAPI;
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
        private const string OptTestToggle = "trainerplus.test.toggle";
        private const string OptTestSlider = "trainerplus.test.slider";

        private static IModLogger _log;

        public override Task OnLoadAsync(ModContext context)
        {
            _log = context.Logger;
            _log.Info($"Trainer Plus loading. ModId='{context.ModId}' Root='{context.ModRootPath}'");

            // Proof of life: register options and confirm the game renders them.
            // Real cheats replace this once the target fields are mapped.
            var options = new ModOptions()
                .AddHeader("Trainer Plus")
                .AddSplitter()
                .AddToggle(OptTestToggle, "Test toggle", false,
                    value => _log.Info($"Test toggle -> {value}"))
                .AddSlider(OptTestSlider, "Test slider", 0, 100, 50,
                    value => _log.Info($"Test slider -> {value}"))
                .AddButton("Test button",
                    () => _log.Info("Test button clicked"));

            OptionsService.Register(context.ModId, options);

            _log.Info("Trainer Plus loaded.");
            return Task.CompletedTask;
        }

        public override Task OnUnloadAsync()
        {
            _log?.Info("Trainer Plus unloading.");
            _log = null;
            return Task.CompletedTask;
        }
    }
}
