namespace RamairaBot
{
    public class FeedbackProcessor
    {
        private bool hitEnemyLastShot;
        private float lastFireTime;

        public void RegisterShot(float currentTime) { lastFireTime = currentTime; hitEnemyLastShot = false; }
        public void RegisterHit(float currentTime) { if (currentTime - lastFireTime < 0.5f) hitEnemyLastShot = true; }

        public float ProcessReward(string action, float health, float surviveTime, bool headshot, bool firstHeadshot, bool nearBomb, string role)
        {
            float baseReward = action switch
            {
                "Move Forward" => nearBomb ? 0.4f : 0.2f,
                "Move Back" => nearBomb ? 0.3f : 0.1f,
                "Move Right" => nearBomb ? 0.3f : 0.2f,
                "Move Left" => nearBomb ? 0.3f : 0.2f,
                "Fire" => hitEnemyLastShot ? 0.6f : 0.3f,
                "Grenade" => 0.8f,
                "Flash" => 0.6f,
                "Kill" => headshot ? (firstHeadshot ? 1.5f : 1.2f) : 1.2f,
                "Death" => surviveTime < 10f ? -1f : -0.5f + (surviveTime * 0.01f),
                "Hurt" => -0.5f,
                "Drop" => 0.1f,
                "Crouch" => health < 30 ? 0.3f : 0.1f,
                "Hold" => health < 30 ? 0.3f : 0.05f,
                "BombPlanted" => 2f,
                "BombDefused" => 2f,
                "Survive" => nearBomb ? surviveTime * 0.05f : surviveTime * 0.01f,
                _ => 0f
            };

            float roleBonus = 0f;
            switch (role)
            {
                case "Entry Fragger":
                    if (action == "Kill" && surviveTime < 20f) roleBonus = 0.5f;
                    break;
                case "Support":
                    if ((action == "Grenade" || action == "Flash") && hitEnemyLastShot) roleBonus = 0.4f;
                    break;
                case "Lurker":
                    if (action == "Kill" && !nearBomb) roleBonus = 0.5f;
                    break;
                case "Rifleman":
                    if (action == "Fire" && hitEnemyLastShot) roleBonus = 0.3f;
                    break;
                case "Anchor":
                    if (action == "Hold" && nearBomb && health > 0) roleBonus = 0.4f;
                    break;
            }

            return baseReward + roleBonus;
        }
    }
}