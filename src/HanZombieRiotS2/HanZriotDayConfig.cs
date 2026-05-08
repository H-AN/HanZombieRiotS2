namespace HanZombieRiotS2;

public class HanZriotDayConfig
{
    public class Day
    {
        public string DayName { get; set; } = string.Empty;
        public int Count { get; set; } = 0;
        public int HealthBoost { get; set; } = 0;
        public int BeforeZombie { get; set; } = 0;
        public string Storyline { get; set; } = string.Empty;
        public string ZombieOverride { get; set; } = string.Empty;
        public bool InfiniteReserveAmmo { get; set; } = false;
        public bool InfiniteClipAmmo { get; set; } = false;
        public bool NoRecoil { get; set; } = false;
    }

    public List<Day> Days { get; set; } = new List<Day>();

}
