using System.Collections.Generic;
using System.IO;

namespace RamairaBot
{
    public partial class DataFormatter
    {
        private readonly List<(float[] State, float[] Action)> dataset = new List<(float[] State, float[] Action)>();

        public void AddBotDataGeneric(float[] state, string actionName)
        {
            float[] action = new float[17]; // 17 aksi dari PPO
            int actionIndex = actionName switch
            {
                "Move" => 0,
                "Fire" => 3,
                "Grenade" => 4,
                "Crouch" => 7,
                "Jump" => 8,
                "LadderUp" => 9,
                "Strafe" => 10,
                "Hold" => 11,
                "Reload" => 12,
                "Peek" => 13,
                "SwitchWeapon" => 14,
                "Flash" => 15,
                "Drop" => 16,
                _ => 0
            };
            action[actionIndex] = 1f;
            dataset.Add((state, action));
        }

        public List<(float[] State, float[] Action)> GetDataset() => dataset;

        public void SaveDatasetToFile(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                foreach (var (state, action) in dataset)
                {
                    writer.WriteLine($"State: {string.Join(",", state)}, Action: {string.Join(",", action)}");
                }
            }
        }

        public void ClearDataset()
        {
            dataset.Clear();
        }
    }
}