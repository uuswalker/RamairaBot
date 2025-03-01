namespace RamairaBot
{
    public class EconomyManager
    {
        private int money;

        public EconomyManager() => money = 800;

        public void UpdateMoney(int newMoney) => money = newMoney;

        public bool ShouldEco() => money < 2000;
    }
}