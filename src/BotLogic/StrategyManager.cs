using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace RamairaBot.BotLogic
{
    public enum TeamStrategy
    {
        PushA,
        PushB,
        Hold,
        Eco,
        Rotate
    }

    public class StrategyManager
    {
        private readonly GamePlugin gamePlugin;
        private readonly EconomyManager economyManager;
        private TeamStrategy currentStrategy;

        public StrategyManager(GamePlugin plugin, EconomyManager economy)
        {
            gamePlugin = plugin;
            economyManager = economy;
            currentStrategy = TeamStrategy.PushA; // Default
        }

        public TeamStrategy GetCurrentStrategy()
        {
            return currentStrategy;
        }

        public void UpdateStrategy(bool bombPlanted, int aliveBots, float nearestEnemyDist)
        {
            if (economyManager.ShouldEco() && aliveBots > 2) // Eco cuma kalo duit kurang dan tim cukup
            {
                currentStrategy = TeamStrategy.Eco;
                return;
            }

            if (bombPlanted)
            {
                currentStrategy = gamePlugin.IsNearCover(GetAnyBot()) ? TeamStrategy.Hold : TeamStrategy.Rotate;
                return;
            }

            if (aliveBots <= 2)
            {
                currentStrategy = TeamStrategy.Hold; // Bertahan kalo tinggal sedikit
                return;
            }

            if (nearestEnemyDist < 700) // Push kalo musuh deket
            {
                currentStrategy = (Utilities.GetPlayers().Count(p => p.IsBot && p.IsValid && p.PlayerPawn.Value?.AbsOrigin != null && p.PlayerPawn.Value.AbsOrigin.X < 0) > 2) ? TeamStrategy.PushA : TeamStrategy.PushB;
            }
            else
            {
                currentStrategy = (Utilities.GetPlayers().Count(p => p.IsBot && p.IsValid && p.PlayerPawn.Value?.AbsOrigin != null && p.PlayerPawn.Value.AbsOrigin.X < 0) > 3) ? TeamStrategy.PushA : TeamStrategy.PushB;
            }
        }

        private CCSPlayerController GetAnyBot()
        {
            return Utilities.GetPlayers().FirstOrDefault(p => p.IsBot && p.IsValid) ?? new CCSPlayerController(0);
        }
    }
}