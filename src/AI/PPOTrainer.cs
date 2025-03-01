using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace RamairaBot
{
    public class PPOTrainer
    {
        private readonly PPOModel model;
        private readonly DataFormatter dataFormatter;
        private List<(float Reward, string Action)> rewards = new List<(float, string)>();
        private float gamma = 0.99f;
        private float lambda = 0.95f;

        public PPOTrainer(PPOModel model, DataFormatter dataFormatter)
        {
            this.model = model;
            this.dataFormatter = dataFormatter;
        }

        public void AddReward(float reward, string action)
        {
            rewards.Add((reward, action));
            Console.WriteLine($"Reward added: {reward} untuk aksi {action}");
        }

        public void Train(int epochs = 5)
        {
            var dataset = dataFormatter.GetDataset();
            if (dataset.Count == 0 || dataset.Count != rewards.Count)
            {
                Console.WriteLine("Dataset kosong atau nggak sinkron sama rewards!");
                return;
            }

            float[] rewardValues = rewards.Select(r => r.Reward).ToArray();
            float[] discountedRewards = ComputeDiscountedRewards(rewardValues);

            for (int epoch = 0; epoch < epochs; epoch++)
            {
                model.Update(dataset, discountedRewards);
                Console.WriteLine($"Training epoch {epoch + 1}/{epochs} complete");
            }

            dataFormatter.ClearDataset();
            rewards.Clear();
            Console.WriteLine("Training selesai, dataset dan rewards direset");
        }

        private float[] ComputeDiscountedRewards(float[] rawRewards)
        {
            float[] discounted = new float[rawRewards.Length];
            float runningAdd = 0f;

            for (int t = rawRewards.Length - 1; t >= 0; t--)
            {
                runningAdd = runningAdd * gamma * lambda + rawRewards[t];
                discounted[t] = runningAdd;
            }

            float mean = discounted.Average();
            float std = (float)Math.Sqrt(discounted.Select(r => (r - mean) * (r - mean)).Sum() / discounted.Length);
            for (int t = 0; t < discounted.Length; t++)
            {
                discounted[t] = std != 0 ? (discounted[t] - mean) / std : discounted[t];
            }

            return discounted;
        }

        public void Reset()
        {
            rewards.Clear();
            dataFormatter.ClearDataset();
            Console.WriteLine("Trainer reset");
        }

        public void SaveModel(string filePath)
        {
            var stateSizeField = model.GetType().GetField("stateSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actionSizeField = model.GetType().GetField("actionSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var weightsField = model.GetType().GetField("policyWeights", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (stateSizeField == null || actionSizeField == null || weightsField == null)
            {
                Console.WriteLine("Gagal akses field model, skip saving.");
                return;
            }

            int? stateSizeNullable = stateSizeField.GetValue(model) as int?;
            int? actionSizeNullable = actionSizeField.GetValue(model) as int?;
            int stateSize = stateSizeNullable ?? 20;
            int actionSize = actionSizeNullable ?? 17;
            float[,]? weights = weightsField.GetValue(model) as float[,];

            if (weights == null)
            {
                Console.WriteLine("Weights null, skip saving.");
                return;
            }

            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write(stateSize);
                writer.Write(actionSize);

                for (int i = 0; i < stateSize; i++)
                {
                    for (int j = 0; j < actionSize; j++)
                    {
                        writer.Write(weights[i, j]);
                    }
                }
            }
            Console.WriteLine($"Model PPO tersimpan di {filePath}");
        }

        public void LoadModel(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File model {filePath} nggak ada, skip loading.");
                return;
            }

            var stateSizeField = model.GetType().GetField("stateSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actionSizeField = model.GetType().GetField("actionSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var weightsField = model.GetType().GetField("policyWeights", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (stateSizeField == null || actionSizeField == null || weightsField == null)
            {
                Console.WriteLine("Gagal akses field model, skip loading.");
                return;
            }

            using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                int savedStateSize = reader.ReadInt32();
                int savedActionSize = reader.ReadInt32();
                int? currentStateSizeNullable = stateSizeField.GetValue(model) as int?;
                int? currentActionSizeNullable = actionSizeField.GetValue(model) as int?;
                int currentStateSize = currentStateSizeNullable ?? 20;
                int currentActionSize = currentActionSizeNullable ?? 17;

                if (savedStateSize != currentStateSize || savedActionSize != currentActionSize)
                {
                    Console.WriteLine("Ukuran state/action nggak cocok, skip loading.");
                    return;
                }

                float[,] weights = new float[savedStateSize, savedActionSize];
                for (int i = 0; i < savedStateSize; i++)
                {
                    for (int j = 0; j < savedActionSize; j++)
                    {
                        weights[i, j] = reader.ReadSingle();
                    }
                }
                weightsField.SetValue(model, weights);
            }
            Console.WriteLine($"Model PPO loaded dari {filePath}");
        }
    }
}