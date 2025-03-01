namespace RamairaBot
{
    public class ActionInterpreter
    {
        public string InterpretAction(int actionIndex)
        {
            return actionIndex switch
            {
                0 => "Move Forward",
                1 => "Move Back",
                2 => "Move Right",
                3 => "Fire",
                4 => "Grenade",
                7 => "Crouch",
                10 => "Move Left",
                11 => "Hold",
                15 => "Flash",
                16 => "Drop",
                _ => "Unknown"
            };
        }

        public int InterpretActionToIndex(string action)
        {
            return action.ToLower() switch
            {
                "move forward" => 0,
                "move back" => 1,
                "move right" => 2,
                "fire" => 3,
                "grenade" => 4,
                "crouch" => 7,
                "move left" => 10,
                "hold" => 11,
                "flash" => 15,
                "drop" => 16,
                _ => 0
            };
        }
    }
}