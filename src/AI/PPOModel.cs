using System;
using System.Collections.Generic;
using System.Linq;

namespace RamairaBot
{
    public class PPOModel
    {
        private readonly int stateSize;
        private readonly int actionSize;
        private readonly Random rand = new Random();
        private float[,] policyWeights;
        private float learningRate = 0.005f; // Turunin dari 0.01 buat stabilitas
        private float clipEpsilon = 0.2f;

        public PPOModel(int stateSize, int actionSize)
        {
            this.stateSize = stateSize;
            this.actionSize = actionSize;
            policyWeights = new float[stateSize, actionSize];
            InitializeWeights();
        }

        private void InitializeWeights()
        {
            for (int i = 0; i < stateSize; i++)
            {
                for (int j = 0; j < actionSize; j++)
                {
                    policyWeights[i, j] = (float)(rand.NextDouble() - 0.5) * 0.1f;
                }
            }
        }

        public float[] Predict(float[] state)
        {
            float[] logits = new float[actionSize];
            for (int j = 0; j < actionSize; j++)
            {
                float sum = 0f;
                for (int i = 0; i < stateSize; i++)
                {
                    sum += state[i] * policyWeights[i, j];
                }
                logits[j] = sum;
            }
            return Softmax(logits);
        }

        private float[] Softmax(float[] logits)
        {
            float[] probs = new float[logits.Length];
            float sumExp = 0f;
            foreach (float logit in logits)
            {
                sumExp += (float)Math.Exp(logit);
            }
            for (int i = 0; i < logits.Length; i++)
            {
                probs[i] = (float)Math.Exp(logits[i]) / sumExp;
            }
            return probs;
        }

        public void Update(List<(float[] State, float[] Action)> batch, float[] rewards)
        {
            if (batch.Count != rewards.Length)
            {
                Console.WriteLine("Batch dan rewards nggak cocok!");
                return;
            }

            for (int t = 0; t < batch.Count; t++)
            {
                var (state, action) = batch[t];
                float reward = rewards[t];
                float[] oldProbs = Predict(state);

                float advantage = reward;

                for (int i = 0; i < stateSize; i++)
                {
                    for (int j = 0; j < actionSize; j++)
                    {
                        float ratio = action[j] / (oldProbs[j] + 1e-10f);
                        float clippedRatio = Math.Max(Math.Min(ratio, 1 + clipEpsilon), 1 - clipEpsilon);
                        float grad = state[i] * advantage * clippedRatio;
                        policyWeights[i, j] += learningRate * grad;
                    }
                }
            }
            Console.WriteLine("PPO model updated!");
        }

        public int ChooseAction(float[] state)
        {
            float[] probs = Predict(state);
            float r = (float)rand.NextDouble();
            float cumulative = 0f;
            for (int i = 0; i < probs.Length; i++)
            {
                cumulative += probs[i];
                if (r < cumulative) return i;
            }
            return probs.Length - 1;
        }

        // Getter buat hyperparameter
        public float LearningRate => learningRate;
        public float ClipEpsilon => clipEpsilon;
    }
}