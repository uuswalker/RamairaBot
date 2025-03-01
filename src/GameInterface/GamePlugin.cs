using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using RamairaBot.BotLogic;

namespace RamairaBot
{
    public class GamePlugin : BasePlugin
    {
        public override string ModuleName => "RamairaBot";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "You";

        private string? demoPath = null;
        private readonly StringBuilder log = new StringBuilder();
        private readonly DataFormatter dataFormatter = new DataFormatter();
        private readonly PPOModel ppoModel = new PPOModel(8, 17);
        private readonly PPOTrainer ppoTrainer;
        private float roundStartTime;
        private readonly string modelPath = @"C:\RamairaBot\ppo_model.bin";
        private int roundsWon = 0;
        private int roundsPlayed = 0;
        private int kills = 0;
        private int deaths = 0;
        private int bombsPlanted = 0;
        private int bombsDefused = 0;
        private bool firstHeadshotInRound = true;
        private bool bombPlanted = false;

        private readonly Vector bombSiteA = new Vector(-2000f, -1000f, -263f);
        private readonly Vector bombSiteB = new Vector(1000f, -1500f, -164f);
        private readonly Vector midWaypoint = new Vector(-1000f, -500f, -200f);

        private readonly ActionSelector actionSelector;
        private readonly ActionExecutor actionExecutor;
        private readonly Pathfinding pathfinding;
        private readonly FeedbackProcessor feedbackProcessor;
        private readonly ActionInterpreter actionInterpreter;
        private readonly EconomyManager economyManager;
        private readonly StrategyManager strategyManager;

        private List<(float Time, string Action, float X, float Y, float Z)> demoActions = new List<(float, string, float, float, float)>();
        private bool useDemo = false;

        private readonly Dictionary<int, string> botRoles = new Dictionary<int, string>();
        private readonly Dictionary<int, bool> botAliveStatus = new Dictionary<int, bool>();
        private int botSpawnCounter = 0;
        private readonly Random rand = new Random();

        public GamePlugin()
        {
            ppoTrainer = new PPOTrainer(ppoModel, dataFormatter);
            pathfinding = new Pathfinding(bombSiteA, bombSiteB, midWaypoint);
            actionInterpreter = new ActionInterpreter();
            actionExecutor = new ActionExecutor(actionInterpreter);
            feedbackProcessor = new FeedbackProcessor();
            economyManager = new EconomyManager();
            strategyManager = new StrategyManager(this, economyManager);
            actionSelector = new ActionSelector(ppoModel, pathfinding, economyManager, this, strategyManager);
        }

        public override void Load(bool isReload)
        {
            Console.WriteLine($"RamairaBot loaded on .NET 8.0! Reload: {isReload}");
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
            RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
            RegisterEventHandler<EventBombDefused>(OnBombDefused);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.EndsWith(".dem") && File.Exists(arg))
                {
                    demoPath = arg;
                    log.AppendLine($"Demo file detected: {demoPath}. Looking for corresponding CSV...");
                    string csvPath = Path.ChangeExtension(demoPath, ".csv");
                    if (File.Exists(csvPath))
                    {
                        LoadDemoActions(csvPath);
                        useDemo = demoActions.Count > 0;
                        if (useDemo) log.AppendLine($"Demo mode activated with {demoActions.Count} actions.");
                        else log.AppendLine("No valid actions loaded from CSV. Falling back to live mode.");
                    }
                    else
                    {
                        log.AppendLine($"No CSV found for {demoPath}. Run demo converter first. Falling back to live mode.");
                    }
                    break;
                }
            }
            if (demoPath == null)
                log.AppendLine("No .dem file provided. Running bot with live data.");

            ppoTrainer.LoadModel(modelPath);
            Server.ExecuteCommand("bot_quota 10");
            Server.ExecuteCommand("bot_add_t");
            Server.ExecuteCommand("bot_add_ct");
            Server.ExecuteCommand("bot_difficulty 2");
            Server.ExecuteCommand("mp_restartgame 1");
            Console.WriteLine(log.ToString());
            Console.WriteLine($"PPO Hyperparameters: LearningRate = {ppoModel.LearningRate}, ClipEpsilon = {ppoModel.ClipEpsilon}");
        }

        private void LoadDemoActions(string csvPath)
        {
            demoActions.Clear();
            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length == 0 || !lines[0].StartsWith("Time,Action,X,Y,Z"))
                {
                    log.AppendLine($"Invalid CSV format in {csvPath}. Expected header: Time,Action,X,Y,Z.");
                    return;
                }

                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length == 5 &&
                        float.TryParse(parts[0], out float time) &&
                        float.TryParse(parts[2], out float x) &&
                        float.TryParse(parts[3], out float y) &&
                        float.TryParse(parts[4], out float z))
                    {
                        demoActions.Add((time, parts[1], x, y, z));
                    }
                }
                log.AppendLine($"Loaded {demoActions.Count} valid actions from {csvPath}.");
            }
            catch (Exception ex)
            {
                log.AppendLine($"Failed to load demo actions from {csvPath}: {ex.Message}");
            }
        }

        private void AssignRole(CCSPlayerController controller)
        {
            int botId = controller.SteamID.GetHashCode();
            string role = (botSpawnCounter % 5) switch
            {
                0 => "Entry Fragger",
                1 => "Support",
                2 => "Lurker",
                3 => "Rifleman",
                4 => "Anchor",
                _ => "Rifleman"
            };
            botRoles[botId] = role;
            botAliveStatus[botId] = true;
            botSpawnCounter++;
            log.AppendLine($"Bot {controller.PlayerName} assigned role: {role}");
        }

        public string GetBotRole(CCSPlayerController controller)
        {
            int botId = controller.SteamID.GetHashCode();
            return botRoles.ContainsKey(botId) ? botRoles[botId] : "Rifleman";
        }

        public bool IsEntryAlive()
        {
            return botAliveStatus.Any(kv => kv.Value && botRoles[kv.Key] == "Entry Fragger");
        }

        private float GetNearestEnemyDistance(CCSPlayerController controller)
        {
            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return float.MaxValue;

            var enemies = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.TeamNum != controller.TeamNum);
            float minDist = float.MaxValue;
            foreach (var enemy in enemies)
            {
                var enemyPawn = enemy.PlayerPawn.Value;
                if (enemyPawn != null && enemyPawn.IsValid && enemyPawn.AbsOrigin != null)
                {
                    float dist = pathfinding.VectorDistance(pawn.AbsOrigin, enemyPawn.AbsOrigin);
                    if (dist < minDist) minDist = dist;
                }
            }
            return minDist;
        }

        public bool IsNearCover(CCSPlayerController controller)
        {
            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return false;
            float distToA = pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA);
            float distToB = pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteB);
            float distToMid = pathfinding.VectorDistance(pawn.AbsOrigin, midWaypoint);
            return distToA < 300 || distToB < 300 || distToMid < 300;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid || !controller.IsBot) return HookResult.Continue;

            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null)
            {
                log.AppendLine("OnPlayerSpawn: Pawn invalid or null.");
                Console.WriteLine(log.ToString());
                return HookResult.Continue;
            }

            try
            {
                AssignRole(controller);
                float velocity = CalculateVelocity(pawn.Velocity);
                float enemyDist = GetNearestEnemyDistance(controller);
                float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, controller.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };
                log.AppendLine($"Bot spawned at X: {pawn.AbsOrigin.X}, Y: {pawn.AbsOrigin.Y}, Z: {pawn.AbsOrigin.Z}, Health: {pawn.Health}, Team: {controller.TeamNum}, Role: {GetBotRole(controller)}, Action: Move");
                Console.WriteLine(log.ToString());
                dataFormatter.AddBotDataGeneric(state, "Move");
                float reward = feedbackProcessor.ProcessReward("Move", pawn.Health, 0, false, false, pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA) < 500, GetBotRole(controller));
                ppoTrainer.AddReward(reward, "Move");

                int aliveBots = botAliveStatus.Count(kv => kv.Value);
                strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
                int actionIndex = useDemo ? ReplayDemoAction(controller, Server.CurrentTime - roundStartTime) :
                    actionSelector.SelectAction(state, pawn.Health < 10, pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA) < 500, bombPlanted, true, GetBotRole(controller));
                actionExecutor.ExecuteAction(controller, actionIndex, log);
            }
            catch (Exception ex)
            {
                log.AppendLine($"Exception in OnPlayerSpawn: {ex.Message}");
                Console.WriteLine(log.ToString());
            }

            return HookResult.Continue;
        }

        private int ReplayDemoAction(CCSPlayerController controller, float currentTime)
        {
            var closestAction = demoActions.OrderBy(a => Math.Abs(a.Time - currentTime)).FirstOrDefault(a => Math.Abs(a.Time - currentTime) < 0.5f);
            if (closestAction.Action == null) return 0;

            int actionIndex = actionInterpreter.InterpretActionToIndex(closestAction.Action);
            log.AppendLine($"Replaying demo action: {closestAction.Action} at time {currentTime}, pos X: {closestAction.X}, Y: {closestAction.Y}, Z: {closestAction.Z}");
            float[] state = new float[8] { closestAction.X, closestAction.Y, closestAction.Z, controller.PlayerPawn.Value?.Health ?? 100f, controller.TeamNum, 0f, 1000f, bombPlanted ? 1f : 0f };
            dataFormatter.AddBotDataGeneric(state, closestAction.Action);
            ppoTrainer.AddReward(feedbackProcessor.ProcessReward(closestAction.Action, 100f, currentTime, false, false, false, GetBotRole(controller)), closestAction.Action);
            return actionIndex;
        }

        private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid || !controller.IsBot) return HookResult.Continue;

            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;

            try
            {
                float velocity = CalculateVelocity(pawn.Velocity);
                float enemyDist = GetNearestEnemyDistance(controller);
                float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, controller.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };
                feedbackProcessor.RegisterShot(Server.CurrentTime);
                actionExecutor.ExecuteAction(controller, 3, log); // Fire
                float reward = feedbackProcessor.ProcessReward("Fire", pawn.Health, Server.CurrentTime - roundStartTime, false, false, enemyDist < 500, GetBotRole(controller));
                dataFormatter.AddBotDataGeneric(state, "Fire");
                ppoTrainer.AddReward(reward, "Fire");

                int aliveBots = botAliveStatus.Count(kv => kv.Value);
                strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");

                // Adjust aim setelah tembak
                int actionIndex = useDemo ? ReplayDemoAction(controller, Server.CurrentTime - roundStartTime) :
                    actionSelector.SelectAction(state, pawn.Health < 10, pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA) < 500, bombPlanted, true, GetBotRole(controller));
                if (enemyDist < 500 && rand.NextDouble() < 0.5) // 50% chance adjust ke headshot angle
                    actionIndex = pathfinding.GetMoveToAngle(state[0], state[1], state[2]);
                actionExecutor.ExecuteAction(controller, actionIndex, log);
            }
            catch (Exception ex)
            {
                log.AppendLine($"Exception in OnWeaponFire: {ex.Message}");
                Console.WriteLine(log.ToString());
            }

            return HookResult.Continue;
        }

        private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid || !controller.IsBot) return HookResult.Continue;

            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;

            try
            {
                float velocity = CalculateVelocity(pawn.Velocity);
                float enemyDist = GetNearestEnemyDistance(controller);
                float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, controller.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };
                actionExecutor.ExecuteAction(controller, 4, log); // Grenade
                float reward = feedbackProcessor.ProcessReward("Grenade", pawn.Health, Server.CurrentTime - roundStartTime, false, false, enemyDist < 500, GetBotRole(controller));
                dataFormatter.AddBotDataGeneric(state, "Grenade");
                ppoTrainer.AddReward(reward, "Grenade");
                log.AppendLine($"Reward added: {reward} untuk aksi Grenade");

                int aliveBots = botAliveStatus.Count(kv => kv.Value);
                strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
                int actionIndex = useDemo ? ReplayDemoAction(controller, Server.CurrentTime - roundStartTime) :
                    actionSelector.SelectAction(state, pawn.Health < 10, pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA) < 500, bombPlanted, true, GetBotRole(controller));
                actionExecutor.ExecuteAction(controller, actionIndex, log);
            }
            catch (Exception ex)
            {
                log.AppendLine($"Exception in OnGrenadeThrown: {ex.Message}");
                Console.WriteLine(log.ToString());
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;

            if (victim != null && victim.IsValid && victim.IsBot)
            {
                var pawn = victim.PlayerPawn.Value;
                if (pawn != null && pawn.IsValid && pawn.AbsOrigin != null)
                {
                    int botId = victim.SteamID.GetHashCode();
                    botAliveStatus[botId] = false;
                    float surviveTime = Server.CurrentTime - roundStartTime;
                    float velocity = CalculateVelocity(pawn.Velocity);
                    float enemyDist = GetNearestEnemyDistance(victim);
                    float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, victim.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };
                    log.AppendLine($"Bot died at X: {pawn.AbsOrigin.X}, Y: {pawn.AbsOrigin.Y}, Z: {pawn.AbsOrigin.Z}, Survive Time: {surviveTime}, Role: {GetBotRole(victim)}, Action: Death");
                    Console.WriteLine(log.ToString());
                    dataFormatter.AddBotDataGeneric(state, "Death");
                    float reward = feedbackProcessor.ProcessReward("Death", pawn.Health, surviveTime, false, false, false, GetBotRole(victim));
                    ppoTrainer.AddReward(reward, "Death");
                    deaths++;

                    int aliveBots = botAliveStatus.Count(kv => kv.Value);
                    strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                    log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
                }
            }

            if (attacker != null && attacker.IsValid && attacker.IsBot)
            {
                var pawn = attacker.PlayerPawn.Value;
                if (pawn != null && pawn.IsValid && pawn.AbsOrigin != null)
                {
                    float velocity = CalculateVelocity(pawn.Velocity);
                    float enemyDist = GetNearestEnemyDistance(attacker);
                    float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, attacker.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };
                    log.AppendLine($"Bot killed at X: {pawn.AbsOrigin.X}, Y: {pawn.AbsOrigin.Y}, Z: {pawn.AbsOrigin.Z}, Headshot: {@event.Headshot}, Role: {GetBotRole(attacker)}, Action: Kill");
                    Console.WriteLine(log.ToString());
                    dataFormatter.AddBotDataGeneric(state, "Kill");
                    float reward = feedbackProcessor.ProcessReward("Kill", pawn.Health, Server.CurrentTime - roundStartTime, @event.Headshot, firstHeadshotInRound, enemyDist < 500, GetBotRole(attacker));
                    ppoTrainer.AddReward(reward, @event.Headshot ? "Headshot" : "Kill");
                    kills++;
                    if (@event.Headshot) firstHeadshotInRound = false;

                    int aliveBots = botAliveStatus.Count(kv => kv.Value);
                    strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                    log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            log.AppendLine($"Round started at game time: {Server.CurrentTime}");
            Console.WriteLine(log.ToString());
            ppoTrainer.Reset();
            roundStartTime = Server.CurrentTime;
            firstHeadshotInRound = true;
            bombPlanted = false;
            economyManager.UpdateMoney(800);
            botRoles.Clear();
            botAliveStatus.Clear();
            botSpawnCounter = 0;
            strategyManager.UpdateStrategy(false, botAliveStatus.Count, float.MaxValue);
            log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
            return HookResult.Continue;
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            var victim = @event.Userid;
            if (victim == null || !victim.IsValid || !victim.IsBot) return HookResult.Continue;

            var pawn = victim.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;

            try
            {
                feedbackProcessor.RegisterHit(Server.CurrentTime);
                float velocity = CalculateVelocity(pawn.Velocity);
                float enemyDist = GetNearestEnemyDistance(victim);
                int healthBefore = pawn.Health; // Ubah ke int
                int healthAfter = @event.Health; // Ubah ke int
                int healthDrop = healthBefore > healthAfter ? healthBefore - healthAfter : 0; // Pastiin int
                float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, healthAfter, victim.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };
                log.AppendLine($"Bot hurt at X: {pawn.AbsOrigin.X}, Y: {pawn.AbsOrigin.Y}, Z: {pawn.AbsOrigin.Z}, Health: {healthAfter}, HealthDrop: {healthDrop}, Role: {GetBotRole(victim)}, Action: Hurt");
                Console.WriteLine(log.ToString());
                dataFormatter.AddBotDataGeneric(state, "Hurt");
                float reward = feedbackProcessor.ProcessReward("Hurt", healthAfter, Server.CurrentTime - roundStartTime, false, false, false, GetBotRole(victim));
                ppoTrainer.AddReward(reward, "Hurt");

                int aliveBots = botAliveStatus.Count(kv => kv.Value);
                strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");

                // Refleks cepet
                int actionIndex;
                if (healthDrop > 15 || healthAfter < 40) // Trigger refleks lebih cepet
                {
                    if (enemyDist < 300 && rand.NextDouble() < 0.8) actionIndex = 3; // Tembak balik kalo musuh deket
                    else if (IsNearCover(victim)) actionIndex = 11; // Hold di cover
                    else actionIndex = pathfinding.GetMoveToCover(state[0], state[1], state[2]); // Lari ke cover
                }
                else
                {
                    actionIndex = useDemo ? ReplayDemoAction(victim, Server.CurrentTime - roundStartTime) :
                        actionSelector.SelectAction(state, healthAfter < 10, pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA) < 500, bombPlanted, true, GetBotRole(victim));
                }
                actionExecutor.ExecuteAction(victim, actionIndex, log);
                pawn.Health = healthAfter; // Update health biar konsisten
            }
            catch (Exception ex)
            {
                log.AppendLine($"Exception in OnPlayerHurt: {ex.Message}");
                Console.WriteLine(log.ToString());
            }

            return HookResult.Continue;
        }

        private HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid || !controller.IsBot) return HookResult.Continue;

            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;

            try
            {
                bombPlanted = true;
                pathfinding.UpdateBombPosition(pawn.AbsOrigin);
                float velocity = CalculateVelocity(pawn.Velocity);
                float enemyDist = GetNearestEnemyDistance(controller);
                float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, controller.TeamNum, velocity, enemyDist, 1f };
                actionExecutor.ExecuteAction(controller, 11, log); // Hold
                float reward = feedbackProcessor.ProcessReward("BombPlanted", pawn.Health, Server.CurrentTime - roundStartTime, false, false, true, GetBotRole(controller));
                dataFormatter.AddBotDataGeneric(state, "BombPlanted");
                ppoTrainer.AddReward(reward, "BombPlanted");
                bombsPlanted++;

                int aliveBots = botAliveStatus.Count(kv => kv.Value);
                strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
                int actionIndex = useDemo ? ReplayDemoAction(controller, Server.CurrentTime - roundStartTime) :
                    actionSelector.SelectAction(state, pawn.Health < 10, true, true, true, GetBotRole(controller));
                actionExecutor.ExecuteAction(controller, actionIndex, log);
            }
            catch (Exception ex)
            {
                log.AppendLine($"Exception in OnBombPlanted: {ex.Message}");
                Console.WriteLine(log.ToString());
            }

            return HookResult.Continue;
        }

        private HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid || !controller.IsBot) return HookResult.Continue;

            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;

            try
            {
                float velocity = CalculateVelocity(pawn.Velocity);
                float enemyDist = GetNearestEnemyDistance(controller);
                float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, controller.TeamNum, velocity, enemyDist, 0f };
                log.AppendLine($"Bot defused bomb at X: {pawn.AbsOrigin.X}, Y: {pawn.AbsOrigin.Y}, Z: {pawn.AbsOrigin.Z}, Role: {GetBotRole(controller)}, Action: BombDefused");
                Console.WriteLine(log.ToString());
                dataFormatter.AddBotDataGeneric(state, "BombDefused");
                float reward = feedbackProcessor.ProcessReward("BombDefused", pawn.Health, Server.CurrentTime - roundStartTime, false, false, false, GetBotRole(controller));
                ppoTrainer.AddReward(reward, "BombDefused");
                bombsDefused++;

                int aliveBots = botAliveStatus.Count(kv => kv.Value);
                strategyManager.UpdateStrategy(bombPlanted, aliveBots, enemyDist);
                log.AppendLine($"Strategy updated to: {strategyManager.GetCurrentStrategy()}");
                int actionIndex = useDemo ? ReplayDemoAction(controller, Server.CurrentTime - roundStartTime) :
                    actionSelector.SelectAction(state, pawn.Health < 10, pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA) < 500, false, true, GetBotRole(controller));
                actionExecutor.ExecuteAction(controller, actionIndex, log);
            }
            catch (Exception ex)
            {
                log.AppendLine($"Exception in OnBombDefused: {ex.Message}");
                Console.WriteLine(log.ToString());
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            roundsPlayed++;
            var bots = Utilities.GetPlayers().Where(p => p.IsBot && p.IsValid).ToList();
            if (bots.Any(b => b.TeamNum == @event.Winner)) roundsWon++;

            foreach (var controller in bots.Where(b => b.PlayerPawn.Value?.Health > 0))
            {
                var pawn = controller.PlayerPawn.Value;
                if (pawn != null && pawn.IsValid && pawn.AbsOrigin != null)
                {
                    float surviveTime = Server.CurrentTime - roundStartTime;
                    float distanceToA = pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteA);
                    float distanceToB = pathfinding.VectorDistance(pawn.AbsOrigin, bombSiteB);
                    float minDistance = Math.Min(distanceToA, distanceToB);
                    float velocity = CalculateVelocity(pawn.Velocity);
                    float enemyDist = GetNearestEnemyDistance(controller);
                    float[] state = new float[8] { pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z, pawn.Health, controller.TeamNum, velocity, enemyDist, bombPlanted ? 1f : 0f };

                    float reward = feedbackProcessor.ProcessReward("Survive", pawn.Health, surviveTime, false, false, minDistance < 500, GetBotRole(controller));
                    dataFormatter.AddBotDataGeneric(state, "Survive");
                    ppoTrainer.AddReward(reward, "Survive");
                }
            }

            float winRate = roundsPlayed > 0 ? (float)roundsWon / roundsPlayed : 0f;
            float kdRatio = deaths > 0 ? (float)kills / deaths : kills;
            log.AppendLine($"Round ended at game time: {Server.CurrentTime}, Winner: {@event.Winner}, Reason: {@event.Reason}");
            log.AppendLine($"Stats: Rounds Played = {roundsPlayed}, Win Rate = {winRate:F2}, K/D Ratio = {kdRatio:F2}, Bombs Planted = {bombsPlanted}, Bombs Defused = {bombsDefused}");
            Console.WriteLine(log.ToString());
            dataFormatter.SaveDatasetToFile(@"C:\RamairaBot\bot_data.txt");

            var dataset = dataFormatter.GetDataset();
            log.AppendLine($"Dataset count: {dataset.Count}, Rewards count: {rewards.Count}");
            if (dataset.Count > 0 && dataset.Count == rewards.Count)
            {
                ppoTrainer.Train(epochs: 5);
                ppoTrainer.SaveModel(modelPath);
            }
            else
            {
                Console.WriteLine($"Dataset ({dataset.Count}) atau rewards ({rewards.Count}) nggak cukup/sinkron buat training!");
            }
            return HookResult.Continue;
        }

        public override void Unload(bool hotReload)
        {
            float winRate = roundsPlayed > 0 ? (float)roundsWon / roundsPlayed : 0f;
            float kdRatio = deaths > 0 ? (float)kills / deaths : kills;
            log.AppendLine($"Final Stats: Rounds Played = {roundsPlayed}, Win Rate = {winRate:F2}, K/D Ratio = {kdRatio:F2}, Bombs Planted = {bombsPlanted}, Bombs Defused = {bombsDefused}");
            File.WriteAllText(@"C:\RamairaBot\bot_log.txt", log.ToString());
            dataFormatter.SaveDatasetToFile(@"C:\RamairaBot\bot_data.txt");
            ppoTrainer.SaveModel(modelPath);
            Console.WriteLine("RamairaBot unloaded, log, data, dan model saved");
        }

        private List<(float Reward, string Action)> rewards => ppoTrainer.GetType()
            .GetField("rewards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(ppoTrainer) as List<(float Reward, string Action)> ?? new List<(float Reward, string Action)>();

        private float CalculateVelocity(CNetworkVelocityVector? velocity)
        {
            if (velocity == null) return 0f;
            float vx = velocity.X;
            float vy = velocity.Y;
            float vz = velocity.Z;
            return (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
        }
    }
}