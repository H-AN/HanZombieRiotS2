using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanZombieRiotS2;

public class HanZriotService
{
    private readonly ILogger<HanZriotService> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanZriotCFG> _mainConfig;
    private readonly IStageConfigProvider _dayConfig;
    private readonly IZombieConfigProvider _zombieConfig;
    private readonly HanZriotHelpers _helpers;
    private readonly HanZriotGlobals _globals;

    public HanZriotService(
        ISwiftlyCore core,
        ILogger<HanZriotService> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig,
        HanZriotHelpers helpers,
        HanZriotGlobals globals)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
        _helpers = helpers;
        _globals = globals;
    }

    private static CancellationTokenSource? CancelTimer(CancellationTokenSource? timer)
    {
        if (timer == null)
            return null;

        try
        {
            timer.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        return null;
    }

    public int BeginRoundGeneration()
    {
        _globals.RoundGeneration++;
        return _globals.RoundGeneration;
    }

    public int InvalidateRoundGeneration()
    {
        _globals.RoundGeneration++;
        return _globals.RoundGeneration;
    }

    public bool IsRoundGenerationCurrent(int generation)
    {
        return generation == _globals.RoundGeneration;
    }

    public int GetMaxDay()
    {
        return _dayConfig.GetConfig().Days?.Count ?? 0;
    }

    public int GetCurrentDay()
    {
        int maxDay = GetMaxDay();
        if (maxDay <= 0)
        {
            _globals.RiotDay = 1;
            return 1;
        }

        _globals.RiotDay = Math.Clamp(_globals.RiotDay, 1, maxDay);
        return _globals.RiotDay;
    }

    public HanZriotDayConfig.Day? TryGetCurrentDayConfig()
    {
        var config = _dayConfig.GetConfig();
        if (config.Days == null || config.Days.Count == 0)
            return null;

        int currentDay = GetCurrentDay();
        return config.Days[currentDay - 1];
    }

    public bool TrySetCurrentDay(int day)
    {
        int maxDay = GetMaxDay();
        if (maxDay <= 0 || day < 1 || day > maxDay)
            return false;

        _globals.RiotDay = day;
        RefreshCurrentDayCorpseMode();
        return true;
    }

    public int SetCurrentDayClamped(int day)
    {
        int maxDay = GetMaxDay();
        if (maxDay <= 0)
        {
            _globals.RiotDay = 1;
            _globals.AllowHumanZombie = false;
            return 1;
        }

        _globals.RiotDay = Math.Clamp(day, 1, maxDay);
        RefreshCurrentDayCorpseMode();
        return _globals.RiotDay;
    }

    public int AdvanceToNextDay()
    {
        return SetCurrentDayClamped(GetCurrentDay() + 1);
    }

    public bool IsFinalDay()
    {
        int maxDay = GetMaxDay();
        return maxDay > 0 && GetCurrentDay() >= maxDay;
    }

    public bool JumpToDayAndEnd(int day)
    {
        if (!TrySetCurrentDay(day))
            return false;

        ForceDayEnd();
        return true;
    }

    public int SkipToNextDayAndEnd()
    {
        int nextDay = AdvanceToNextDay();
        ForceDayEnd();
        return nextDay;
    }

    public bool CurrentMapIsHighDifficulty()
    {
        return _globals.CurrentMapIsHighDiff;
    }

    public string GetCurrentMapDifficulty()
    {
        return _globals.CurrentMapIsHighDiff ? "high" : "normal";
    }

    public bool NextMapWillBeHighDifficulty()
    {
        return _globals.HightDiff;
    }

    public void SetNextMapHighDifficulty(bool enabled)
    {
        _globals.HightDiff = enabled;

        if (enabled)
        {
            _globals.KillCount = Math.Max(_globals.KillCount, 1000);
            _globals.KillPercent = 100f;
        }
        else if (!_globals.CurrentMapIsHighDiff)
        {
            _globals.KillCount = 0;
            _globals.KillPercent = 0f;
        }
    }

    public int GetCurrentDayBeforeZombieCount()
    {
        var currentDay = TryGetCurrentDayConfig();
        return currentDay == null ? 0 : Math.Max(0, currentDay.BeforeZombie);
    }

    public void RefreshCurrentDayCorpseMode()
    {
        _globals.AllowHumanZombie = GetCurrentDayBeforeZombieCount() > 0;
    }

    public void ResetPlayerCorpseModeToCurrentDay(int playerId)
    {
        if (playerId < 0 || playerId >= _globals.BeAZombie.Length)
            return;

        ClearPendingHumanRespawn(playerId);
        _globals.BeAZombie[playerId] = _globals.AllowHumanZombie ? GetCurrentDayBeforeZombieCount() : 0;
    }

    public bool IsPlayerMarkedForZombieRespawn(int playerId)
    {
        return playerId >= 0
            && playerId < _globals.BeAZombie.Length
            && _globals.BeAZombie[playerId] < 0;
    }

    public bool ConsumeHumanDeathAndCheckZombieRespawn(int playerId)
    {
        if (!_globals.AllowHumanZombie || playerId < 0 || playerId >= _globals.BeAZombie.Length)
            return false;

        int remainingHumanDeaths = _globals.BeAZombie[playerId];
        if (remainingHumanDeaths <= 1)
        {
            _globals.BeAZombie[playerId] = -1;
            return true;
        }

        _globals.BeAZombie[playerId] = remainingHumanDeaths - 1;
        return false;
    }

    public int QueuePendingHumanRespawn(int playerId, float respawnDelaySeconds)
    {
        if (playerId < 0 || playerId >= _globals.HumanRespawnRemaining.Length)
            return 0;

        int roundedSeconds = Math.Max(0, (int)Math.Ceiling(respawnDelaySeconds));
        _globals.HumanRespawnRemaining[playerId] = roundedSeconds;
        return roundedSeconds;
    }

    public void ClearPendingHumanRespawn(int playerId)
    {
        if (playerId < 0 || playerId >= _globals.HumanRespawnRemaining.Length)
            return;

        _globals.HumanRespawnRemaining[playerId] = 0;
    }

    public int TickPendingHumanRespawn(int playerId)
    {
        if (playerId < 0 || playerId >= _globals.HumanRespawnRemaining.Length)
            return 0;

        int remaining = _globals.HumanRespawnRemaining[playerId];
        if (remaining <= 0)
            return 0;

        remaining--;
        _globals.HumanRespawnRemaining[playerId] = remaining;
        return remaining;
    }

    public bool HasPendingHumanRespawns()
    {
        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player is not { IsValid: true } || player.IsFakeClient)
                continue;

            int playerId = player.PlayerID;
            if (playerId < 0 || playerId >= _globals.HumanRespawnRemaining.Length)
                continue;

            if (_globals.HumanRespawnRemaining[playerId] > 0)
                return true;
        }

        return false;
    }

    public int GetPlayerRemainingDeathsBeforeZombie(int playerId)
    {
        if (!_globals.AllowHumanZombie || playerId < 0 || playerId >= _globals.BeAZombie.Length)
            return 0;

        int remaining = _globals.BeAZombie[playerId];
        return remaining < 0 ? 0 : remaining;
    }

    public string? GetCurrentZombieName(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return null;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return null;

        if (controller.TeamNum != (byte)Team.T || !controller.PawnIsAlive)
            return null;

        return GetCurrentZombieName(player.PlayerID);
    }

    public string? GetCurrentZombieName(int playerId)
    {
        if (playerId < 0)
            return null;

        return _globals.CurrentZombieNames.TryGetValue(playerId, out var zombieName)
            && !string.IsNullOrWhiteSpace(zombieName)
            ? zombieName
            : null;
    }

    public void SetCurrentZombieName(int playerId, string? zombieName)
    {
        if (playerId < 0)
            return;

        if (string.IsNullOrWhiteSpace(zombieName))
        {
            _globals.CurrentZombieNames.Remove(playerId);
            return;
        }

        _globals.CurrentZombieNames[playerId] = zombieName;
    }

    public void ResetSpecialGrenadeLifeUsage(int playerId)
    {
        if (playerId < 0 || playerId >= _globals.RebornSec.Length)
            return;

        _globals.FireGrenadeLifeUses[playerId] = 0;
        _globals.LightGrenadeLifeUses[playerId] = 0;
        _globals.FreezeGrenadeLifeUses[playerId] = 0;
    }

    public bool TryActivateSpecialGrenade(HanZriotSpecialGrenadeType type, IPlayer player, bool enabled, bool allowBots, int roundLimit, int lifeLimit)
    {
        if (player is not { IsValid: true })
            return false;

        int playerId = player.PlayerID;
        var controller = player.Controller;
        if (!(enabled
            && controller is { IsValid: true, TeamNum: (byte)Team.CT, PawnIsAlive: true }
            && (!player.IsFakeClient || allowBots)
            && IsWithinSpecialGrenadeLimit(GetRoundGrenadeUses(type), playerId, roundLimit)
            && IsWithinSpecialGrenadeLimit(GetLifeGrenadeUses(type), playerId, lifeLimit)))
        {
            return false;
        }

        GetRoundGrenadeUses(type)[playerId]++;
        GetLifeGrenadeUses(type)[playerId]++;
        return true;
    }

    public void ResetPlayerRuntimeState(int playerId, bool resetHudState)
    {
        if (playerId < 0 || playerId >= _globals.RebornSec.Length)
            return;

        _globals.SpawnProtect[playerId] = CancelTimer(_globals.SpawnProtect[playerId]);

        _globals.RebornSec[playerId] = 0.0f;
        _globals.BeAZombie[playerId] = 0;
        ClearPendingHumanRespawn(playerId);
        _globals.InProtect[playerId] = false;
        _globals.g_ZombieRegenStates.Remove(playerId);
        SetCurrentZombieName(playerId, null);
        ResetPlayerSpecialGrenadeState(playerId);
        _helpers.ClearPlayerGrenadeEffects(playerId);

        if (resetHudState)
        {
            _globals.PlayerHud[playerId] = false;
            _globals.PlayerDmgHud[playerId] = false;
        }
    }

    public void ResetRoundRuntimeState(bool clearPlayerRoundState, bool clearKillCounters)
    {
        _globals.GameStart = false;
        _globals.Countdown = 0;

        _globals.SpawnAllZombie = CancelTimer(_globals.SpawnAllZombie);
        _globals.g_DeathCheck = CancelTimer(_globals.g_DeathCheck);
        _globals.g_hCountdown = CancelTimer(_globals.g_hCountdown);
        _globals.g_hAmbMusic = CancelTimer(_globals.g_hAmbMusic);
        _globals.g_HUDTimer = CancelTimer(_globals.g_HUDTimer);
        _globals.g_DeathCountDown = CancelTimer(_globals.g_DeathCountDown);
        _globals.g_ZombieRegenTimer = CancelTimer(_globals.g_ZombieRegenTimer);

        if (clearKillCounters)
        {
            _globals.NeedKillZombie = 0;
            _globals.ZombieKill = 0;
        }

        _globals.g_ZombieRegenStates.Clear();
        _globals.CurrentZombieNames.Clear();
        Array.Clear(_globals.FireGrenadeRoundUses);
        Array.Clear(_globals.FireGrenadeLifeUses);
        Array.Clear(_globals.LightGrenadeRoundUses);
        Array.Clear(_globals.LightGrenadeLifeUses);
        Array.Clear(_globals.FreezeGrenadeRoundUses);
        Array.Clear(_globals.FreezeGrenadeLifeUses);
        _helpers.ClearAllGrenadeEffects();

        if (clearPlayerRoundState)
        {
            for (int playerId = 0; playerId < _globals.RebornSec.Length; playerId++)
            {
                ResetPlayerRuntimeState(playerId, resetHudState: false);
            }
        }

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player == null || !player.IsValid)
                continue;

            var controller = player.Controller;
            var entity = controller?.Entity;
            if (entity != null && entity.EntityInstance is { IsValid: true, IsValidEntity: true })
            {
                entity.Name = string.Empty;
            }
        }
    }

    public void ResetMapRuntimeState()
    {
        InvalidateRoundGeneration();
        ResetRoundRuntimeState(clearPlayerRoundState: true, clearKillCounters: true);
        _globals.RiotDay = 1;
        _globals.AllowHumanZombie = false;
        //_core.Engine.ExecuteCommand("bot_quota 0");
    }

    public void ResetPluginRuntimeState()
    {
        ResetMapRuntimeState();
    }

    public void ForcePlayerHuman(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return;

        var playerId = player.PlayerID;
        ulong sessionId = player.SessionId;
        ResetPlayerRuntimeState(playerId, resetHudState: false);
        ResetPlayerCorpseModeToCurrentDay(playerId);

        var entity = controller.Entity;
        if (entity != null && entity.EntityInstance is { IsValid: true, IsValidEntity: true })
        {
            entity.Name = string.Empty;
        }

        if (controller.TeamNum != (byte)Team.CT)
        {
            player.SwitchTeam(Team.CT);
        }

        int generation = _globals.RoundGeneration;
        _core.Scheduler.DelayBySeconds(0.2f, () =>
        {
            if (!IsRoundGenerationCurrent(generation))
                return;

            var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
            if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                return;

            var currentController = currentPlayer.Controller;
            if (currentController == null || !currentController.IsValid)
                return;

            if (!currentController.PawnIsAlive)
            {
                currentController.Respawn();
            }
        });
    }

    public void ForcePlayerZombie(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return;

        int playerId = player.PlayerID;
        ulong sessionId = player.SessionId;
        ResetPlayerRuntimeState(playerId, resetHudState: false);
        _globals.BeAZombie[playerId] = -1;

        if (controller.TeamNum != (byte)Team.T)
        {
            player.SwitchTeam(Team.T);
        }

        int generation = _globals.RoundGeneration;
        _core.Scheduler.DelayBySeconds(0.2f, () =>
        {
            if (!IsRoundGenerationCurrent(generation))
                return;

            var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
            if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                return;

            var currentController = currentPlayer.Controller;
            if (currentController == null || !currentController.IsValid)
                return;

            if (!currentController.PawnIsAlive)
            {
                currentController.Respawn();
                return;
            }

            PossZombie(currentPlayer);
        });
    }

    public void PossZombie(IPlayer client)
    {
        if (client == null || !client.IsValid)
            return;

        var clientPawn = client.PlayerPawn;
        if (clientPawn == null || !clientPawn.IsValid)
            return;

        var controller = client.Controller;
        if (controller == null || !controller.IsValid)
            return;

        ChangeKnife(client);

        if (_mainConfig.CurrentValue.ZombieNoBlock)
        {
            _helpers.NoBlock(clientPawn);
        }

        var currentDay = TryGetCurrentDayConfig();
        if (currentDay == null)
        {
            _core.Logger.LogError(_core.Localizer["NoDayData"]);
            return;
        }

        int currentDayIndex = GetCurrentDay() - 1;
        var zombiesForLevel = _helpers.GetZombiesForCurrentLevel(currentDayIndex);

        if (zombiesForLevel.Count == 0)
        {
            _core.Logger.LogError(_core.Localizer["NoZombieData"]);
            return;
        }

        var randomZombie = _helpers.SelectZombieForSpawn(zombiesForLevel);
        if (randomZombie == null)
        {
            _core.Logger.LogError(_core.Localizer["NoZombieData"]);
            return;
        }

        int maxHealth = randomZombie.Health;
        if (currentDay.HealthBoost > 0)
        {
            maxHealth += currentDay.HealthBoost;
        }

        _globals.g_ZombieRegenStates.Remove(client.PlayerID);

        clientPawn.SetModel(randomZombie.Model);
        clientPawn.MaxHealth = maxHealth;
        clientPawn.MaxHealthUpdated();
        clientPawn.Health = maxHealth;
        clientPawn.HealthUpdated();
        clientPawn.VelocityModifier = randomZombie.Speed > 0 ? randomZombie.Speed : 1.0f;
        clientPawn.VelocityModifierUpdated();
        clientPawn.ActualGravityScale = randomZombie.Gravity > 0 ? randomZombie.Gravity : 0.8f;

        if (randomZombie.HealthRevive > 0)
        {
            float now = Environment.TickCount64 / 1000f;
            _globals.g_ZombieRegenStates[client.PlayerID] = new ZombieRegenState
            {
                PlayerID = client.PlayerID,
                RegenAmount = randomZombie.HealthReviveHp,
                RegenInterval = randomZombie.HealthReviveSec,
                NextRegenTime = now + randomZombie.HealthReviveSec
            };
        }

        var controllerEntity = controller.Entity;
        if (controllerEntity != null && controllerEntity.EntityInstance is { IsValid: true, IsValidEntity: true })
        {
            controllerEntity.Name = randomZombie.Name;
        }

        SetCurrentZombieName(client.PlayerID, randomZombie.Name);
    }

    private void ResetPlayerSpecialGrenadeState(int playerId)
    {
        _globals.FireGrenadeRoundUses[playerId] = 0;
        _globals.FireGrenadeLifeUses[playerId] = 0;
        _globals.LightGrenadeRoundUses[playerId] = 0;
        _globals.LightGrenadeLifeUses[playerId] = 0;
        _globals.FreezeGrenadeRoundUses[playerId] = 0;
        _globals.FreezeGrenadeLifeUses[playerId] = 0;
    }

    private static bool IsWithinSpecialGrenadeLimit(int[] counters, int playerId, int limit)
    {
        return playerId >= 0
            && playerId < counters.Length
            && (limit <= 0 || counters[playerId] < limit);
    }

    private int[] GetRoundGrenadeUses(HanZriotSpecialGrenadeType type)
    {
        return type switch
        {
            HanZriotSpecialGrenadeType.Fire => _globals.FireGrenadeRoundUses,
            HanZriotSpecialGrenadeType.Light => _globals.LightGrenadeRoundUses,
            HanZriotSpecialGrenadeType.Freeze => _globals.FreezeGrenadeRoundUses,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private int[] GetLifeGrenadeUses(HanZriotSpecialGrenadeType type)
    {
        return type switch
        {
            HanZriotSpecialGrenadeType.Fire => _globals.FireGrenadeLifeUses,
            HanZriotSpecialGrenadeType.Light => _globals.LightGrenadeLifeUses,
            HanZriotSpecialGrenadeType.Freeze => _globals.FreezeGrenadeLifeUses,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public void ChangeKnife(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return;

        if (controller.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        var ws = pawn.WeaponServices;
        if (ws == null || !ws.IsValid)
            return;

        ws.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);

        var Is = pawn.ItemServices;
        if (Is == null || !Is.IsValid)
            return;

        var weapon = Is.GiveItem<CCSWeaponBase>("weapon_knife");
        if (weapon == null || !weapon.IsValid)
            return;

        weapon.AcceptInput("ChangeSubclass", "42");
        weapon.AttributeManager.Item.Initialized = true;
        weapon.AttributeManager.Item.ItemDefinitionIndex = 42;
        weapon.SetModel("");
        weapon.AttributeManager.Item.CustomName = "ZombieClaw";
        weapon.AttributeManager.Item.CustomNameOverride = "ZombieClaw";
        weapon.AttributeManager.Item.CustomNameUpdated();

    }

    public void Round_Countdown(int expectedRoundGeneration)
    {
        if (!IsRoundGenerationCurrent(expectedRoundGeneration))
            return;

        var config = _mainConfig.CurrentValue;
        int currentDisplay = _globals.Countdown;

        if (_globals.Countdown > 0)
        {
            _globals.Countdown--;
        }

        if (currentDisplay <= 10 && currentDisplay >= 1 && config.SoundCountdown)
        {
            var soundList = config.SoundEventCountdown.Split(',');
            int soundIndex = currentDisplay - 1;

            if (soundIndex >= 0 && soundIndex < soundList.Length)
            {
                _helpers.EmitSoundToAll(soundList[soundIndex].Trim());
            }
        }
        else if (currentDisplay == 20 && config.Soundremaining && !string.IsNullOrWhiteSpace(config.SoundEventremaining))
        {
            var remaining = _helpers.RandomSelectSound(config.SoundEventremaining);
            if (remaining != null)
            {
                _helpers.EmitSoundToAll(remaining);
            }
        }

        if (currentDisplay <= 0)
        {
            _globals.g_hCountdown = CancelTimer(_globals.g_hCountdown);
            _globals.GameStart = true;
            _helpers.SetAllZombieUnFreeze();

            if (config.SoundZombieStart && !string.IsNullOrWhiteSpace(config.SoundEventZombieStart))
            {
                var zombieStart = _helpers.RandomSelectSound(config.SoundEventZombieStart);
                if (zombieStart != null)
                {
                    _helpers.EmitSoundToAll(zombieStart);
                }
            }

            return;
        }

        foreach (var player in _core.PlayerManager.GetCTAlive())
        {
            if (player is { IsValid: true } && !player.IsFakeClient)
            {
                player.SendMessage(MessageType.Center, $"{currentDisplay} {_core.Translation.GetPlayerLocalizer(player)["MoveZombie"]}");
            }
        }
    }

    public void FakeCtswin()
    {
        var config = _mainConfig.CurrentValue;
        var currentDay = TryGetCurrentDayConfig();
        int maxDay = GetMaxDay();
        if (currentDay == null || maxDay <= 0)
            return;

        InvalidateRoundGeneration();
        ResetRoundRuntimeState(clearPlayerRoundState: true, clearKillCounters: false);

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player is not { IsValid: true })
                continue;

            if (!player.IsFakeClient)
            {
                if (GetCurrentDay() == maxDay)
                {
                    string message =
                        $"<span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["ClearZombieOnMap"]}</font></span><br>" +
                        $"<span><span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["HumanTakeOver"]}</font></span><br>" +
                        $"<span><span><font color='#00FF00'>{_core.Translation.GetPlayerLocalizer(player)["NextMap"]}</font></span>";
                    player.SendMessage(MessageType.CenterHTML, message);
                }
                else
                {
                    string message =
                        $"<span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["HumanWins"]}</font></span><br>" +
                        $"<span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["ZombieClear"]}</font></span><br>" +
                        $"<span><font color='#00FF00'>{_core.Translation.GetPlayerLocalizer(player)["NextDay"]}</font></span>";
                    player.SendMessage(MessageType.CenterHTML, message);
                }

                _globals.InProtect[player.PlayerID] = true;
                ResetPlayerCorpseModeToCurrentDay(player.PlayerID);
            }
            else
            {
                _helpers.SetFreezeState(player, true);
                _helpers.TeleportZombie(player);
            }
        }

        if (config.SoundHumanWins && !string.IsNullOrWhiteSpace(config.SoundEventHumanWins))
        {
            var humanWins = _helpers.RandomSelectSound(config.SoundEventHumanWins);
            if (humanWins != null)
            {
                _helpers.EmitSoundToAll(humanWins);
            }
        }

        if (GetCurrentDay() == maxDay)
        {
            int generation = _globals.RoundGeneration;
            _core.Scheduler.DelayBySeconds(5.0f, () =>
            {
                if (IsRoundGenerationCurrent(generation))
                {
                    _helpers.ChangeMap();
                }
            });
        }
        else
        {
            _helpers.SetTeamScore(Team.CT);
            _helpers.TerminateRound(RoundEndReason.CTsWin, 5.0f);
        }
    }

    public void Faketswin()
    {
        var config = _mainConfig.CurrentValue;
        var currentDay = TryGetCurrentDayConfig();
        if (currentDay == null)
            return;

        InvalidateRoundGeneration();
        ResetRoundRuntimeState(clearPlayerRoundState: true, clearKillCounters: false);

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player is not { IsValid: true } || player.IsFakeClient)
                continue;

            player.SendMessage(MessageType.CenterHTML, _core.Translation.GetPlayerLocalizer(player)["ZombieWins"]);
            ResetPlayerCorpseModeToCurrentDay(player.PlayerID);
        }

        if (config.SoundZombieWins && !string.IsNullOrWhiteSpace(config.SoundEventZombieWins))
        {
            var zombieWins = _helpers.RandomSelectSound(config.SoundEventZombieWins);
            if (zombieWins != null)
            {
                _helpers.EmitSoundToAll(zombieWins);
            }
        }

        _helpers.SetTeamScore(Team.T);
        _helpers.TerminateRound(RoundEndReason.TerroristsWin, 5.0f);
    }

    public void ForceDayEnd()
    {
        InvalidateRoundGeneration();
        ResetRoundRuntimeState(clearPlayerRoundState: true, clearKillCounters: true);
        _helpers.TerminateRound(RoundEndReason.RoundDraw, 5.0f);
    }

    public void JoinTeamCheck(IPlayer player)
    {
        if (player is not { IsValid: true } || player.Controller is not { IsValid: true } controller)
            return;

        if (!_globals.GameStart)
        {
            if (!controller.PawnIsAlive)
            {
                controller.Respawn();
            }

            return;
        }

        int humanCount = _core.PlayerManager.GetCTAlive().Count();
        if (humanCount > 0)
        {
            if (!controller.PawnIsAlive)
            {
                controller.Respawn();
            }
        }
        else if (_globals.g_DeathCheck != null)
        {
            Faketswin();
        }
    }

    public void CheckHumanAlive()
    {
        int humanCount = _core.PlayerManager.GetCTAlive().Count();
        if (humanCount <= 0 && _globals.GameStart)
        {
            if (_globals.AllowHumanZombie && HasPendingHumanRespawns())
                return;

            Faketswin();
        }
    }
}
