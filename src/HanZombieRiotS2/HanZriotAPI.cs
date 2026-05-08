
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace HanZombieRiotS2;

public class HanZriotAPI : IHanZriotAPI, IDisposable
{

    private bool _disposed = false;
    private readonly ISwiftlyCore _core;
    private readonly HanZriotGlobals _globals;
    private readonly HanZriotService _services;
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HanZriotAPI));
        }
    }
    public HanZriotAPI(ISwiftlyCore core,
        HanZriotGlobals globals,
        HanZriotService services)
    {
        _core = core;
        _globals = globals;
        _services = services;
    }

    public bool GameStart => _globals.GameStart;
    public int CurrentDay => _services.GetCurrentDay();
    public int NeedKillZombie => _globals.NeedKillZombie;
    public int ZombieKill => _globals.ZombieKill;
    public int ZombiesLeft => Math.Max(0, _globals.NeedKillZombie - _globals.ZombieKill);
    public int HumansAlive => _core.PlayerManager.GetCTAlive().Count();
    public int MaxDay => _services.GetMaxDay();
    public bool AllowHumanZombie => _globals.AllowHumanZombie;
    public int CurrentDayBeforeZombie => _services.GetCurrentDayBeforeZombieCount();
    public bool CurrentMapIsHighDifficulty => _services.CurrentMapIsHighDifficulty();
    public bool NextMapWillBeHighDifficulty => _services.NextMapWillBeHighDifficulty();

    public void ZRiot_Human(IPlayer player)
    {
        ThrowIfDisposed();
        _services.ForcePlayerHuman(player);
    }

    public void ZRiot_Zombie(IPlayer player)
    {
        ThrowIfDisposed();
        _services.ForcePlayerZombie(player);
    }

    public bool ZRiot_JumpToDay(int day)
    {
        ThrowIfDisposed();
        return _services.JumpToDayAndEnd(day);
    }

    public int ZRiot_SkipToNextDay()
    {
        ThrowIfDisposed();
        return _services.SkipToNextDayAndEnd();
    }

    public void ZRiot_ForceDayEnd()
    {
        ThrowIfDisposed();
        _services.ForceDayEnd();
    }

    public void ZRiot_SetNextMapHighDifficulty(bool enabled)
    {
        ThrowIfDisposed();
        _services.SetNextMapHighDifficulty(enabled);
    }

    public int ZRiot_GetRemainingDeathsBeforeZombie(IPlayer player)
    {
        ThrowIfDisposed();
        if (player is not { IsValid: true })
            return 0;

        return _services.GetPlayerRemainingDeathsBeforeZombie(player.PlayerID);
    }

    public bool ZRiot_IsPlayerQueuedToRespawnAsZombie(IPlayer player)
    {
        ThrowIfDisposed();
        if (player is not { IsValid: true })
            return false;

        return _services.IsPlayerMarkedForZombieRespawn(player.PlayerID);
    }

    public void Dispose()
    {
        _disposed = true;
        
    }

    
}
