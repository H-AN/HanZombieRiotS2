using SwiftlyS2.Shared.Players;

namespace HanZombieRiotS2;

/// <summary>
/// Han Zombie Riot API.
/// Han 僵尸暴动 API。
/// </summary>
public interface IHanZriotAPI
{
    /// <summary>
    /// Get whether the game has started.
    /// 获取游戏是否已经开始。
    /// </summary>
    bool GameStart { get; }

    /// <summary>
    /// Get the current day.
    /// 获取当前天数。
    /// </summary>
    int CurrentDay { get; }

    /// <summary>
    /// Get the number of zombies that need to be killed this day.
    /// 获取当前天需要击杀的丧尸数量。
    /// </summary>
    int NeedKillZombie { get; }

    /// <summary>
    /// Get the number of zombies already killed this day.
    /// 获取当前天已经击杀的丧尸数量。
    /// </summary>
    int ZombieKill { get; }

    /// <summary>
    /// Get the remaining number of zombies for the current day.
    /// 获取当前天剩余需要击杀的丧尸数量。
    /// </summary>
    int ZombiesLeft { get; }

    /// <summary>
    /// Get the number of alive humans.
    /// 获取当前存活的人类数量。
    /// </summary>
    int HumansAlive { get; }

    /// <summary>
    /// Get the maximum number of days on the current map.
    /// 获取当前地图的最大天数。
    /// </summary>
    int MaxDay { get; }

    /// <summary>
    /// Get whether corpse-mode is enabled for the current day.
    /// 获取当前天是否启用人类死亡后变丧尸模式。
    /// </summary>
    bool AllowHumanZombie { get; }

    /// <summary>
    /// Get the current day corpse-mode threshold.
    /// 获取当前天人类还需死亡多少次后会变成丧尸。
    /// </summary>
    int CurrentDayBeforeZombie { get; }

    /// <summary>
    /// Get the current map difficulty key.
    /// Currently returns "high" or "normal".
    /// 获取当前地图难度标识。
    /// 当前返回 "high" 或 "normal"。
    /// </summary>
    string CurrentMapDifficulty { get; }

    /// <summary>
    /// Get whether the current map is running high difficulty.
    /// 获取当前地图是否处于高难度。
    /// </summary>
    bool CurrentMapIsHighDifficulty { get; }

    /// <summary>
    /// Get whether the next map will use high difficulty.
    /// 获取下一张地图是否会使用高难度。
    /// </summary>
    bool NextMapWillBeHighDifficulty { get; }

    /// <summary>
    /// Get the current zombie class name the player is actively using.
    /// Returns null when the player is not currently an active zombie.
    /// 获取玩家当前正在使用的丧尸类型名称。
    /// 如果玩家当前不是激活状态的丧尸，则返回 null。
    /// </summary>
    string? ZRiot_GetZombieName(IPlayer player);

    /// <summary>
    /// Change the player to a human.
    /// 将指定玩家变为人类。
    /// </summary>
    void ZRiot_Human(IPlayer player);

    /// <summary>
    /// Change the player to a zombie.
    /// 将指定玩家变为丧尸。
    /// </summary>
    void ZRiot_Zombie(IPlayer player);

    /// <summary>
    /// Jump to the specified day and end the current round safely.
    /// 安全跳转到指定天并结束当前回合。
    /// </summary>
    bool ZRiot_JumpToDay(int day);

    /// <summary>
    /// Jump to the next day and end the current round safely.
    /// 安全跳转到下一天并结束当前回合。
    /// </summary>
    int ZRiot_SkipToNextDay();

    /// <summary>
    /// End the current day safely.
    /// 安全结束当前天。
    /// </summary>
    void ZRiot_ForceDayEnd();

    /// <summary>
    /// Set whether the next map should use high difficulty.
    /// 设置下一张地图是否使用高难度。
    /// </summary>
    void ZRiot_SetNextMapHighDifficulty(bool enabled);

    /// <summary>
    /// Get the remaining number of human deaths before the player becomes a zombie.
    /// 获取该玩家在变成丧尸前还剩余多少次人类死亡次数。
    /// </summary>
    int ZRiot_GetRemainingDeathsBeforeZombie(IPlayer player);

    /// <summary>
    /// Get whether the player is queued to respawn as a zombie.
    /// 获取该玩家是否已被标记为以丧尸身份重生。
    /// </summary>
    bool ZRiot_IsPlayerQueuedToRespawnAsZombie(IPlayer player);
}
