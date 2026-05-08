using SwiftlyS2.Shared.Players;

namespace HanZombieRiotS2;

/// <summary>
/// Han Zombie Riot API.
/// Han 僵尸暴动 API.
/// </summary>
public interface IHanZriotAPI
{
    /// <summary>
    /// Get GameStart.
    /// 获取游戏开始状态.
    /// </summary>
    bool GameStart { get; }

    /// <summary>
    /// Get CurrentDay.
    /// 获取当前天数.
    /// </summary>
    int CurrentDay { get; }

    /// <summary>
    /// Get NeedKillZombie.
    /// 获取当前需要击杀多少只丧尸.
    /// </summary>
    int NeedKillZombie { get; }

    /// <summary>
    /// Get ZombieKill.
    /// 获取当前所有玩家已经击杀了多少只丧尸.
    /// </summary>
    int ZombieKill { get; }

    /// <summary>
    /// Get ZombiesLeft.
    /// 获取还剩余多少只丧尸通关.
    /// </summary>
    int ZombiesLeft { get; }

    /// <summary>
    /// Get HumansAlive.
    /// 获取当前存活人类数量.
    /// </summary>
    int HumansAlive { get; }

    /// <summary>
    /// Get MaxDay.
    /// 获取当前地图最大关卡天数.
    /// </summary>
    int MaxDay { get; }
    /// <summary>
    /// Get whether corpse-mode is enabled for the current day.
    /// 鑾峰彇褰撳墠鍏冲崱鏄惁鍚敤浜虹被姝讳骸鍚庡彉涓у案.
    /// </summary>
    bool AllowHumanZombie { get; }

    /// <summary>
    /// Get the current day corpse-mode threshold.
    /// 鑾峰彇褰撳墠鍏冲崱浜虹被闇€姝讳骸澶氬皯娆″悗鍙樹负涓у案.
    /// </summary>
    int CurrentDayBeforeZombie { get; }

    /// <summary>
    /// Get whether the current map is running high difficulty.
    /// 鑾峰彇褰撳墠鍦板浘鏄惁涓洪珮闅惧害.
    /// </summary>
    bool CurrentMapIsHighDifficulty { get; }

    /// <summary>
    /// Get whether the next map will use high difficulty.
    /// 鑾峰彇涓嬩竴寮犲湴鍥炬槸鍚﹀皢浣跨敤楂橀毦搴︺€?
    /// </summary>
    bool NextMapWillBeHighDifficulty { get; }

    /// <summary>
    /// Get the current zombie class name the player is actively using.
    /// Returns null when the player is not currently an active zombie.
    /// </summary>
    string? ZRiot_GetZombieName(IPlayer player);

    /// <summary>
    /// change player to human.
    /// 指定某个玩家成为人类.
    /// </summary>
    void ZRiot_Human(IPlayer player);

    /// <summary>
    /// change player to human.
    /// 指定某个玩家成为丧尸.
    /// </summary>
    void ZRiot_Zombie(IPlayer player);
    /// <summary>
    /// Jump to the specified day and end the current round safely.
    /// 瀹夊叏璺冲埌鎸囧畾鍏冲崱骞剁粨鏉熷綋鍓嶅洖鍚?
    /// </summary>
    bool ZRiot_JumpToDay(int day);

    /// <summary>
    /// Jump to the next day and end the current round safely.
    /// 瀹夊叏璺冲埌涓嬩竴鍏冲崱骞剁粨鏉熷綋鍓嶅洖鍚?
    /// </summary>
    int ZRiot_SkipToNextDay();

    /// <summary>
    /// End the current day safely.
    /// 瀹夊叏缁撴潫褰撳墠鍏冲崱.
    /// </summary>
    void ZRiot_ForceDayEnd();

    /// <summary>
    /// Set whether the next map should use high difficulty.
    /// 璁剧疆涓嬩竴寮犲湴鍥炬槸鍚﹀惎鐢ㄩ珮闅惧害.
    /// </summary>
    void ZRiot_SetNextMapHighDifficulty(bool enabled);

    /// <summary>
    /// Get the remaining number of human deaths before the player becomes a zombie.
    /// 鑾峰彇鐜╁鍙樹负涓у案鍓嶈繕鍙互浠ヤ汉绫昏韩浠芥浜″嚑娆°
    /// </summary>
    int ZRiot_GetRemainingDeathsBeforeZombie(IPlayer player);

    /// <summary>
    /// Get whether the player is queued to respawn as a zombie.
    /// 鑾峰彇鐜╁涓嬩竴娆″娲绘槸鍚﹀皢浠ヤ抚灏歌韩浠借繑鍥?
    /// </summary>
    bool ZRiot_IsPlayerQueuedToRespawnAsZombie(IPlayer player);

}
