using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanZombieRiotS2;

public class HanZriotGlobals
{
    // ===== 数值类 =====
    public int NeedKillZombie { get; set; }
    public int ZombieKill { get; set; }
    public int RiotDay { get; set; } = 1;
    public int Countdown { get; set; }
    public int KillCount { get; set; }
    public int RoundGeneration { get; set; }
    public float KillPercent { get; set; }

    // ===== 布尔类 =====
    public bool GameStart { get; set; }
    public bool HightDiff { get; set; } = false;
    public bool CurrentMapIsHighDiff { get; set; }
    public bool AllowHumanZombie { get; set; }
    public bool SafeRoundStart { get; set; }

    // ===== 数组类（玩家状态） =====
    public float[] RebornSec { get; } = new float[65];
    public int[] BeAZombie { get; } = new int[65];
    public int[] HumanRespawnRemaining { get; } = new int[65];

    public bool[] InProtect { get; } = new bool[65];

    public bool[] PlayerHud { get; set; } = new bool[65];
    public bool[] PlayerDmgHud { get; set; } = new bool[65];


    public CancellationTokenSource? SpawnAllZombie { get; set; } = null;

    public CancellationTokenSource? g_DeathCheck { get; set; } = null;
    public CancellationTokenSource? g_hCountdown { get; set; } = null;

    public CancellationTokenSource? g_hAmbMusic { get; set; } = null;



    public CancellationTokenSource?[] SpawnProtect { get; set; } = new CancellationTokenSource?[65];
    public CancellationTokenSource? g_HUDTimer { get; set; } = null;
    public CancellationTokenSource? g_DeathCountDown { get; set; } = null;

    public Dictionary<int, ZombieRegenState> g_ZombieRegenStates = new();
    public Dictionary<int, string> CurrentZombieNames { get; } = new();
    public int[] FireGrenadeRoundUses { get; } = new int[65];
    public int[] FireGrenadeLifeUses { get; } = new int[65];
    public int[] LightGrenadeRoundUses { get; } = new int[65];
    public int[] LightGrenadeLifeUses { get; } = new int[65];
    public int[] FreezeGrenadeRoundUses { get; } = new int[65];
    public int[] FreezeGrenadeLifeUses { get; } = new int[65];
    public Dictionary<int, (CParticleSystem? Particle, CancellationTokenSource? Timer)> ActiveGrenadeBurns { get; } = new();
    public Dictionary<int, CancellationTokenSource?> ActiveFreezeGrenades { get; } = new();
    public Dictionary<uint, COmniLight> ActiveGrenadeLights { get; } = new();
    public Dictionary<uint, CancellationTokenSource> ActiveGrenadeLightTimers { get; } = new();
    public HashSet<short> SpecialFlashbangEntityIds { get; } = new();

    public CancellationTokenSource? g_ZombieRegenTimer = null;

}

public class ZombieRegenState
{
    public int PlayerID;
    public int RegenAmount;       // 每次回血量
    public float RegenInterval;   // 间隔秒数
    public float NextRegenTime;   // 下一次回血时间戳（秒）
}
