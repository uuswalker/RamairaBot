using CounterStrikeSharp.API.Core;
using System.Text;

namespace RamairaBot
{
    public class ActionExecutor
    {
        private readonly ActionInterpreter interpreter;

        public ActionExecutor(ActionInterpreter ai) => interpreter = ai;

        public void ExecuteAction(CCSPlayerController controller, int actionIndex, StringBuilder log)
        {
            if (controller == null || !controller.IsValid) return;
            var pawn = controller.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return;

            string actionName = interpreter.InterpretAction(actionIndex);
            switch (actionIndex)
            {
                case 0: controller.ExecuteClientCommand("+forward"); controller.ExecuteClientCommand("-forward"); break;
                case 1: controller.ExecuteClientCommand("+back"); controller.ExecuteClientCommand("-back"); break;
                case 2: controller.ExecuteClientCommand("+right"); controller.ExecuteClientCommand("-right"); break;
                case 3: controller.ExecuteClientCommand("+attack"); controller.ExecuteClientCommand("-attack"); break;
                case 4: controller.ExecuteClientCommand("use weapon_hegrenade; +attack"); controller.ExecuteClientCommand("-attack"); break;
                case 7: controller.ExecuteClientCommand("+crouch"); controller.ExecuteClientCommand("-crouch"); break;
                case 10: controller.ExecuteClientCommand("+left"); controller.ExecuteClientCommand("-left"); break;
                case 11: controller.ExecuteClientCommand("holdpos"); break;
                case 15: controller.ExecuteClientCommand("use weapon_flashbang; +attack"); controller.ExecuteClientCommand("-attack"); break;
                case 16: controller.ExecuteClientCommand("drop"); break;
            }
            log.AppendLine($"Bot executed action at X: {pawn.AbsOrigin.X}, Y: {pawn.AbsOrigin.Y}, Z: {pawn.AbsOrigin.Z}, Action: {actionName}");
        }
    }
}