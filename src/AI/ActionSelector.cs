using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using RamairaBot.BotLogic;

namespace RamairaBot
{
    public class ActionSelector
    {
        private readonly PPOModel ppoModel;
        private readonly Pathfinding pathfinding;
        private readonly EconomyManager economy;
        private readonly GamePlugin gamePlugin;
        private readonly StrategyManager strategyManager;
        private readonly Random rand = new Random();
        private float lastFireTime = 0f; // Cooldown buat tembak

        public ActionSelector(PPOModel model, Pathfinding pf, EconomyManager em, GamePlugin plugin, StrategyManager sm)
        {
            ppoModel = model;
            pathfinding = pf;
            economy = em;
            gamePlugin = plugin;
            strategyManager = sm;
        }

        public int SelectAction(float[] state, bool lowHealth, bool nearBombSite, bool bombPlanted, bool hasGrenade, string role)
        {
            int actionIndex = ppoModel.ChooseAction(state);
            bool entryAlive = gamePlugin.IsEntryAlive();
            CCSPlayerController controller = Utilities.GetPlayerFromSteamId((ulong)state[4].GetHashCode()) ?? new CCSPlayerController(0);
            bool nearCover = gamePlugin.IsNearCover(controller);
            TeamStrategy strategy = strategyManager.GetCurrentStrategy();
            float currentTime = Server.CurrentTime;

            if (lowHealth) // Refleks kalo HP < 10
            {
                if (nearCover) return 11; // Hold di cover
                if (state[6] < 300) return 3; // Tembak balik kalo musuh deket banget
                return pathfinding.GetMoveToCover(state[0], state[1], state[2]); // Lari ke cover
            }

            switch (strategy)
            {
                case TeamStrategy.PushA:
                    if (role == "Entry Fragger") return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    if (role == "Support" && !entryAlive && hasGrenade && rand.NextDouble() < 0.95) return rand.Next(2) == 0 ? 4 : 15;
                    break;
                case TeamStrategy.PushB:
                    if (role == "Entry Fragger") return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    if (role == "Support" && !entryAlive && hasGrenade && rand.NextDouble() < 0.95) return rand.Next(2) == 0 ? 4 : 15;
                    break;
                case TeamStrategy.Hold:
                    if (role == "Anchor" || nearBombSite) return 11;
                    break;
                case TeamStrategy.Eco:
                    return 11;
                case TeamStrategy.Rotate:
                    if (role == "Anchor" || role == "Support") return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    break;
            }

            switch (role)
            {
                case "Entry Fragger":
                    if (state[6] < 500 && currentTime - lastFireTime > 0.5f) // Tembak kalo musuh deket, cooldown 0.5s
                    {
                        lastFireTime = currentTime;
                        return 3;
                    }
                    if (!nearBombSite) return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    return pathfinding.GetMoveToAngle(state[0], state[1], state[2]); // Adjust placement
                case "Support":
                    if (!entryAlive && rand.NextDouble() < 0.95)
                    {
                        if (hasGrenade && nearBombSite) return rand.Next(2) == 0 ? 4 : 15;
                        return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    }
                    if (hasGrenade && state[6] < 700 && rand.NextDouble() < 0.9) return rand.Next(2) == 0 ? 4 : 15;
                    if (!nearBombSite) return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    break;
                case "Lurker":
                    if (state[6] < 300 && currentTime - lastFireTime > 0.5f)
                    {
                        lastFireTime = currentTime;
                        return 3;
                    }
                    return pathfinding.GetFlankAction(state[0], state[1], state[2]);
                case "Rifleman":
                    if (state[6] < 700 && currentTime - lastFireTime > 0.5f)
                    {
                        lastFireTime = currentTime;
                        return 3;
                    }
                    if (!nearBombSite) return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    return pathfinding.GetMoveToAngle(state[0], state[1], state[2]);
                case "Anchor":
                    if (state[4] == 2 && nearBombSite) return 11;
                    if (state[4] == 3 && bombPlanted) return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    if (!bombPlanted) return pathfinding.GetMoveAction(state[0], state[1], state[2], nearBombSite);
                    break;
            }

            return actionIndex;
        }
    }
}