using BepInEx.Configuration;

namespace AvgSellPrice
{
    internal static class PluginConfig
    {
        // Viser præcis pris frem for afrundet (fx "1 234 ₽" i stedet for "1k")
        public static ConfigEntry<bool> PrecisePrice { get; private set; }

        // Vis "Around" foran prisen (slå fra hvis man vil have ren pris)
        public static ConfigEntry<bool> ShowAroundPrefix { get; private set; }

        // Minimum-pris der vises – alt under dette rundes op (default 1000)
        public static ConfigEntry<int> MinimumDisplayPrice { get; private set; }

        public static void Init(ConfigFile config)
        {
            PrecisePrice = config.Bind(
                "Display",
                "PrecisePrice",
                false,
                "Vis den præcise pris i stedet for afrundet (fx '1 234 ₽' i stedet for '1k')."
            );

            ShowAroundPrefix = config.Bind(
                "Display",
                "ShowAroundPrefix",
                true,
                "Vis 'Around' foran prisen. Sæt til false for at få ren pris uden prefix."
            );

            MinimumDisplayPrice = config.Bind(
                "Display",
                "MinimumDisplayPrice",
                1000,
                "Priser under denne grænse vises som denne værdi i stedet (fx 789 vises som 1k). Sæt til 0 for at deaktivere."
            );
        }
    }
}
