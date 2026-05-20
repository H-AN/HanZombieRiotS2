using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Schemas;

namespace HanZombieRiotS2;

public class HanZriotEvents
{
    private readonly ILogger<HanZriotEvents> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanZriotCFG> _mainConfig;
    private readonly IStageConfigProvider _dayConfig;
    private readonly IZombieConfigProvider _zombieConfig;
    private readonly IGrenadeConfigProvider _grenadeConfig;
    private readonly HanZriotHelpers _helpers;
    private readonly HanZriotGlobals _globals;
    private readonly HanZriotHud _hud;
    private readonly HanZriotService _services;
    private ZombieDataConfig? _cachedZombieDamageConfig;
    private readonly Dictionary<string, float> _zombieDamageByName = new(StringComparer.Ordinal);
    public HanZriotEvents(ISwiftlyCore core, ILogger<HanZriotEvents> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig,
        IGrenadeConfigProvider grenadeConfig,
        HanZriotHelpers helpers, HanZriotGlobals globals,
        HanZriotHud hud, HanZriotService Service)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
        _grenadeConfig = grenadeConfig;
        _helpers = helpers;
        _globals = globals;
        _hud = hud;
        _services = Service;
    }

    public void HookEvents()
    {
        // GameEvent Hook
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerHurt);
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        _core.GameEvent.HookPre<EventPlayerBlind>(OnPlayerBlind);
        _core.GameEvent.HookPre<EventWeaponFire>(OnWeaponFire);
        _core.GameEvent.HookPre<EventHegrenadeDetonate>(OnHegrenadeDetonate);
        _core.GameEvent.HookPre<EventFlashbangDetonate>(OnFlashbangDetonate);
        _core.GameEvent.HookPre<EventSmokegrenadeDetonate>(OnSmokegrenadeDetonate);


        // Event 系列 Hook
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.Event.OnMapUnload += Event_MapEnd;
        _core.Event.OnEntityCreated += Event_OnEntityCreated;
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
        _core.Event.OnEntityTakeDamage += Event_OnEntityTakeDamage;
        _core.Event.OnTick += Event_OnTick;
        _core.Event.OnWeaponServicesCanUseHook += Event_OnWeaponServicesCanUseHook;

    }


    public void Event_OnWeaponServicesCanUseHook(IOnWeaponServicesCanUseHookEvent @event)
    {
        var weapon = @event.Weapon;
        var weaponName = weapon?.Entity?.DesignerName;

        var pawn = @event.WeaponServices.Pawn;
        if (pawn == null || !pawn.IsValid)
            return;

        //禁止除匕首外的武器
        if (pawn.TeamNum == 2 && weaponName != null && weaponName != "weapon_knife")
        {
            @event.SetResult(false); // 阻止使用
        }
    }

    private void Event_OnTick()
    {

        var gameRules = _core.EntitySystem.GetGameRules();
        if (gameRules is not { IsValid: true, WarmupPeriod: false })
            return;

        gameRules.GameRestart = gameRules.RestartRoundTime.Value < _core.Engine.GlobalVars.CurrentTime;
        ApplyCurrentDayWeaponModifiers();
    }

    private void Event_MapEnd(IOnMapUnloadEvent @event)
    {
        _services.ResetMapRuntimeState();
    }

    private void Event_OnEntityCreated(IOnEntityCreatedEvent @event)
    {
        var entity = @event.Entity;
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return;

        if (string.IsNullOrWhiteSpace(entity.DesignerName) || !entity.DesignerName.Contains("_projectile", StringComparison.OrdinalIgnoreCase))
            return;

        var entityIndex = entity.Index;
        _core.Scheduler.NextWorldUpdate(() =>
        {
            var currentEntity = _core.EntitySystem.GetEntityByIndex<CEntityInstance>(entityIndex);
            if (currentEntity == null || !currentEntity.IsValid || !currentEntity.IsValidEntity)
                return;

            if (string.IsNullOrWhiteSpace(currentEntity.DesignerName) || !currentEntity.DesignerName.Contains("_projectile", StringComparison.OrdinalIgnoreCase))
                return;

            _helpers.CheckGrenadeSpawned(currentEntity);
        });
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        _globals.SafeRoundStart = true;

        return HookResult.Continue;
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        if (!_globals.SafeRoundStart)
            return HookResult.Continue;

        _globals.SafeRoundStart = false;

        _services.ResetRoundRuntimeState(clearPlayerRoundState: true, clearKillCounters: false);
        int roundGeneration = _services.BeginRoundGeneration();
        _core.Engine.ExecuteCommand("bot_quota_mode fill");
        _core.Engine.ExecuteCommand("bot_quota 20");

        _core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                return;

            _helpers.GlobalHudTimer(roundGeneration);
            _helpers.ZombieRegenTimer(roundGeneration);
            _helpers.HumanDeathCountDown(roundGeneration);
        });

        var allPlayers = _core.PlayerManager.GetAllPlayers();

        var currentDay = _services.TryGetCurrentDayConfig();
        if (currentDay == null)
        {
            return HookResult.Continue;
        }
        _services.RefreshCurrentDayCorpseMode();

        _globals.NeedKillZombie = currentDay.Count;
        _globals.ZombieKill = 0;

        _globals.SpawnAllZombie = _core.Scheduler.DelayAndRepeatBySeconds(0.5f, 5.0f, () =>
        {
            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                return;

            _helpers.ChangeBotTeam();
            _helpers.RespawnAllZombie();
        });
        _core.Scheduler.StopOnMapChange(_globals.SpawnAllZombie);

        _globals.g_DeathCheck = _core.Scheduler.RepeatBySeconds(0.1f, () =>
        {
            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                return;

            _services.CheckHumanAlive();
        });
        _core.Scheduler.StopOnMapChange(_globals.g_DeathCheck);

        var CFG = _mainConfig.CurrentValue;
        if (CFG.SoundRoundstartMusic && !string.IsNullOrWhiteSpace(CFG.SoundEventRoundstartMusic))
        {
            var RoundstartMusic = _helpers.RandomSelectSound(CFG.SoundEventRoundstartMusic);
            if (RoundstartMusic != null)
            {
                _helpers.EmitSoundToAll(RoundstartMusic);
            }
        }
        if (CFG.SoundAmbSound && !string.IsNullOrWhiteSpace(CFG.SoundEventAmbSound))
        {
            _globals.g_hAmbMusic = _core.Scheduler.DelayAndRepeatBySeconds(0.1f, CFG.AmbSoundLoopTime, () =>
            {
                if (_services.IsRoundGenerationCurrent(roundGeneration))
                {
                    _helpers.PlayAmbSound(CFG.SoundEventAmbSound);
                }
            });
            _core.Scheduler.StopOnMapChange(_globals.g_hAmbMusic);
        }


        if (CFG.FreezeZombie > 0)
        {
            _globals.Countdown = (int)Math.Ceiling(CFG.FreezeZombie);

            _globals.g_hCountdown = _core.Scheduler.DelayAndRepeatBySeconds(0.2f, 1.0f, () => _services.Round_Countdown(roundGeneration));
            _core.Scheduler.StopOnMapChange(_globals.g_hCountdown);
        }
        else
        {
            _globals.GameStart = true;
            _helpers.SetAllZombieUnFreeze();
            if (CFG.SoundZombieStart && !string.IsNullOrWhiteSpace(CFG.SoundEventZombieStart))
            {
                var ZombieStart = _helpers.RandomSelectSound(CFG.SoundEventZombieStart);
                if (ZombieStart != null)
                {
                    _helpers.EmitSoundToAll(ZombieStart);
                }
                
            }
        }

        foreach (var player in allPlayers)
        {
            if (player == null || !player.IsValid)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var team = pawn.TeamNum;

            var slot = player.PlayerID;
            ulong sessionId = player.SessionId;

            if (player.IsFakeClient)
            {
                if (team == 2)
                {
                    if (_globals.Countdown > 0)
                    {
                        _helpers.SetFreezeState(player, true);
                        _core.Scheduler.DelayBySeconds((float)_globals.Countdown, () =>
                        {
                            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                                return;

                            var currentPlayer = _core.PlayerManager.GetPlayer(slot);
                            if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                                return;

                            _helpers.SetFreezeState(currentPlayer, false);
                        });
                    }
                }
            }
            else
            {
                if (_globals.AllowHumanZombie)
                {
                    _core.Scheduler.DelayBySeconds(1f, () =>
                    {
                        if (_services.IsRoundGenerationCurrent(roundGeneration))
                        {
                            _services.ResetPlayerCorpseModeToCurrentDay(slot);
                        }
                    });
                }
            }
        }

        //_helpers.RemoveRoundObjective();

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var playerController = @event.UserIdController;
        if (!playerController.IsValid)
            return HookResult.Continue;

        var playerPawn = @event.UserIdPawn;
        if (!playerPawn.IsValid)
            return HookResult.Continue;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var attackerPawn = @event.AttackerPawn;
        if (!attackerPawn.IsValid)
            return HookResult.Continue;

        var attackerController = @event.AttackerController;
        if (!attackerController.IsValid)
            return HookResult.Continue;

        var dmgHealth = @event.ActualDmgHealth;
        var hitgroup = @event.ActualHitGroup;

        if (attackerPawn.TeamNum == 3 && playerPawn.TeamNum == 2)
        {
            //int LeftZombie = _globals.NeedKillZombie - _globals.ZombieKill;
            var remainingHP = playerPawn.Health;

            var CFG = _mainConfig.CurrentValue;

            if (CFG.HurtMoney)
            {
                _helpers.GiveCash(attacker, dmgHealth, "hurt");
            }

            if (hitgroup == HitGroup_t.HITGROUP_HEAD)
            {
                if (CFG.SoundZombiePain && !string.IsNullOrWhiteSpace(CFG.SoundEventZombiePain))
                {
                    int randomPain = Random.Shared.Next(0, 5);
                    if (randomPain == 1)
                    {
                        var PainSounds = _helpers.RandomSelectSound(CFG.SoundEventZombiePain);
                        if (PainSounds != null)
                        {
                            _helpers.EmitSoundToEntity(player, PainSounds);
                        }
                    }
                }
            }
            else
            {
                if (CFG.SoundZombieHurt && !string.IsNullOrWhiteSpace(CFG.SoundEventZombieHurt))
                {
                    var HurtSounds = _helpers.RandomSelectSound(CFG.SoundEventZombieHurt);
                    if (HurtSounds != null)
                    {
                        _helpers.EmitSoundToEntity(player, HurtSounds);
                    }
                }

            }

            if (_globals.g_hCountdown == null)
            {
                if (!_globals.PlayerDmgHud[attacker.PlayerID])
                {
                    attacker.SendMessage(MessageType.Center, $"\n\n{_core.Translation.GetPlayerLocalizer(attacker)["Target", playerController.PlayerName, remainingHP]}");
                }

            }
        }
        if (attackerPawn.TeamNum == 2 && playerPawn.TeamNum == 3)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["Attacked", attackerController.PlayerName, dmgHealth]}");
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {

        _services.CheckHumanAlive();
        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var deather = @event.UserIdPlayer;
        if (deather == null || !deather.IsValid)
            return HookResult.Continue;

        var deatherController = @event.UserIdController;
        if (!deatherController.IsValid)
            return HookResult.Continue;

        var deatherPawn = @event.UserIdPawn;
        if (!deatherPawn.IsValid)
            return HookResult.Continue;

        int deatherPlayerId = deather.PlayerID;
        ulong deatherSessionId = deather.SessionId;
        bool deatherWasZombie = deatherPawn.TeamNum == 2;
        bool deatherWasHuman = deatherPawn.TeamNum == 3;
        int attackerPlayerId = attacker.PlayerID;
        ulong attackerSessionId = attacker.SessionId;
        int roundGeneration = _helpers.GetCurrentRoundGeneration();
        var dayConfig = _dayConfig.GetConfig();
        var CFG = _mainConfig.CurrentValue;
        int maxDay = dayConfig.Days.Count;

        _services.ClearPendingHumanRespawn(deatherPlayerId);

        _core.Scheduler.NextWorldUpdate(() =>
        {
            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                return;

            var currentDeather = _core.PlayerManager.GetPlayer(deatherPlayerId);
            if (currentDeather == null || !currentDeather.IsValid || currentDeather.SessionId != deatherSessionId)
                return;

            var currentDeatherController = currentDeather.Controller;
            if (currentDeatherController == null || !currentDeatherController.IsValid)
                return;

            if (deatherWasZombie)
            {
                var deatherControllerEntity = currentDeatherController.Entity;
                if (deatherControllerEntity != null && deatherControllerEntity.EntityInstance is { IsValid: true, IsValidEntity: true })
                {
                    deatherControllerEntity.Name = "";
                }
                _services.SetCurrentZombieName(deatherPlayerId, null);
                _helpers.ClearPlayerGrenadeEffects(deatherPlayerId);
                _globals.g_ZombieRegenStates.Remove(deatherPlayerId);

                if (_globals.GameStart)
                {
                    _core.Scheduler.DelayBySeconds(1.0f, () =>
                    {
                        if (!_services.IsRoundGenerationCurrent(roundGeneration))
                            return;

                        var respawnPlayer = _core.PlayerManager.GetPlayer(deatherPlayerId);
                        if (respawnPlayer == null || !respawnPlayer.IsValid || respawnPlayer.SessionId != deatherSessionId)
                            return;

                        var respawnController = respawnPlayer.Controller;
                        if (respawnController == null || !respawnController.IsValid)
                            return;

                        _helpers.RespawnClient(respawnController);
                    });

                    if (CFG.DeathMoney > 0)
                    {
                        var currentAttacker = _core.PlayerManager.GetPlayer(attackerPlayerId);
                        if (currentAttacker != null && currentAttacker.IsValid && currentAttacker.SessionId == attackerSessionId)
                        {
                            _helpers.GiveCash(currentAttacker, CFG.DeathMoney, "death");
                        }
                    }

                    if (CFG.SoundZombieDead && !string.IsNullOrWhiteSpace(CFG.SoundEventZombieDead))
                    {
                        var deadSounds = _helpers.RandomSelectSound(CFG.SoundEventZombieDead);
                        if (deadSounds != null)
                        {
                            _helpers.EmitSoundToEntity(currentDeather, deadSounds);
                        }
                    }

                    _globals.ZombieKill++;
                    _helpers.UpdateKillCount();
                    if (_globals.ZombieKill >= _globals.NeedKillZombie)
                    {
                        _globals.ZombieKill = 0;
                        if (!_services.IsFinalDay())
                        {
                            _services.FakeCtswin();
                            _services.AdvanceToNextDay();
                        }
                        else
                        {
                            _services.FakeCtswin();
                            _services.SetCurrentDayClamped(maxDay);
                        }

                    }
                }
            }

            if (deatherWasHuman)
            {
                if (_globals.GameStart)
                {
                    bool respawnAsZombie = _services.ConsumeHumanDeathAndCheckZombieRespawn(deatherPlayerId);

                    if (_globals.AllowHumanZombie && respawnAsZombie)
                    {
                        _core.Scheduler.DelayBySeconds(1.0f, () =>
                        {
                            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                                return;

                            var respawnPlayer = _core.PlayerManager.GetPlayer(deatherPlayerId);
                            if (respawnPlayer == null || !respawnPlayer.IsValid || respawnPlayer.SessionId != deatherSessionId)
                                return;

                            var respawnController = respawnPlayer.Controller;
                            if (respawnController == null || !respawnController.IsValid)
                                return;

                            _helpers.RespawnClient(respawnController);
                        });
                    }
                    else
                    {
                        float respawnSeconds = _globals.RebornSec[deatherPlayerId] > 0
                            ? _globals.RebornSec[deatherPlayerId]
                            : CFG.HumanRebornSec;
                        int respawnDelay = _services.QueuePendingHumanRespawn(deatherPlayerId, respawnSeconds);
                        currentDeather.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(currentDeather)["DeathInfo", respawnDelay]}");

                        if (respawnDelay > 0)
                        {
                            currentDeather.SendMessage(MessageType.CenterHTML, $"{_core.Translation.GetPlayerLocalizer(currentDeather)["ReSpawn", respawnDelay]}");
                        }
                        else
                        {
                            _core.Scheduler.NextTick(() =>
                            {
                                if (!_services.IsRoundGenerationCurrent(roundGeneration))
                                    return;

                                var respawnPlayer = _core.PlayerManager.GetPlayer(deatherPlayerId);
                                if (respawnPlayer == null || !respawnPlayer.IsValid || respawnPlayer.SessionId != deatherSessionId)
                                    return;

                                var respawnController = respawnPlayer.Controller;
                                if (respawnController == null || !respawnController.IsValid)
                                    return;

                                _helpers.RespawnClient(respawnController);
                            });
                        }
                    }
                }
                else
                {
                    _core.Scheduler.DelayBySeconds(1.0f, () =>
                    {
                        if (!_services.IsRoundGenerationCurrent(roundGeneration))
                            return;

                        var respawnPlayer = _core.PlayerManager.GetPlayer(deatherPlayerId);
                        if (respawnPlayer == null || !respawnPlayer.IsValid || respawnPlayer.SessionId != deatherSessionId)
                            return;

                        var respawnController = respawnPlayer.Controller;
                        if (respawnController == null || !respawnController.IsValid)
                            return;

                        _helpers.RespawnClient(respawnController);
                    });
                }
            }
        });

        return HookResult.Continue;
    }

    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var playerpawn = @event.UserIdPawn;
        if (!playerpawn.IsValid)
            return HookResult.Continue;

        var playerController = @event.UserIdController;
        if (!playerController.IsValid)
            return HookResult.Continue;

        int playerId = player.PlayerID;
        ulong sessionId = player.SessionId;
        var CFG = _mainConfig.CurrentValue;

        if (!player.IsFakeClient)
        {
            _services.ClearPendingHumanRespawn(playerId);

            if (_services.IsPlayerMarkedForZombieRespawn(playerId))
            {
                if (playerpawn.TeamNum != 2)
                {
                    player.SwitchTeam(Team.T);
                }

                _core.Scheduler.DelayBySeconds(0.1f, () =>
                {
                    var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                        return;

                    _services.PossZombie(currentPlayer);
                });

                if (!_globals.GameStart)
                {
                    _core.Scheduler.DelayBySeconds(0.5f, () =>
                    {
                        if (_globals.GameStart)
                            return;

                        var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                        if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                            return;

                        _helpers.SetFreezeState(currentPlayer, true);
                    });
                }

                return HookResult.Continue;
            }

            _globals.RebornSec[playerId] = (int)Math.Ceiling(CFG.HumanRebornSec);
            _services.SetCurrentZombieName(playerId, null);
            _services.ResetSpecialGrenadeLifeUsage(playerId);
            if (playerpawn.TeamNum != 3)
            {
                player.SwitchTeam(Team.CT);
            }

            _core.Scheduler.DelayBySeconds(0.1f, () =>
            {
                var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                    return;

                _helpers.ApplyHumanDefaultModel(currentPlayer);
                GiveConfiguredGrenadesOnSpawn(currentPlayer);
            });

            if (_globals.AllowHumanZombie && _globals.BeAZombie[playerId] == 0)
            {
                _services.ResetPlayerCorpseModeToCurrentDay(playerId);
            }

            if (CFG.SpawnProtect)
            {
                _core.Scheduler.DelayBySeconds(0.2f, () =>
                {
                    var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                        return;

                    var currentController = currentPlayer.Controller;
                    if (currentController == null || !currentController.IsValid || !currentController.PawnIsAlive)
                        return;

                    _globals.InProtect[playerId] = true;
                });

                _globals.SpawnProtect[playerId]?.Cancel();
                _globals.SpawnProtect[playerId] = null;
                _globals.SpawnProtect[playerId] = _core.Scheduler.DelayBySeconds(CFG.SpawnProtectCount, () =>
                {
                    var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                        return;

                    _helpers.DeleSpawnProtect(currentPlayer);
                });
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["SpawnProtect", CFG.SpawnProtectCount]}");
            }

            if (CFG.HumanNoBlock)
            {
                _core.Scheduler.NextWorldUpdate(() =>
                {
                    var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                        return;

                    var currentPawn = currentPlayer.PlayerPawn;
                    if (currentPawn == null || !currentPawn.IsValid || currentPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                        return;

                    _helpers.NoBlock(currentPawn);
                });
            }

        }
        else
        {
            if (playerpawn.TeamNum != 2)
            {
                player.SwitchTeam(Team.T);
            }
            _core.Scheduler.DelayBySeconds(0.1f, () =>
            {
                var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                    return;

                _services.PossZombie(currentPlayer);
            });
            if (!_globals.GameStart)
            {
                _core.Scheduler.DelayBySeconds(0.5f, () =>
                {
                    if (_globals.GameStart)
                        return;

                    var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (currentPlayer == null || !currentPlayer.IsValid || currentPlayer.SessionId != sessionId)
                        return;

                    _helpers.SetFreezeState(currentPlayer, true);
                });
            }
        }

        return HookResult.Continue;
    }


    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var client = @event.PlayerId;
        _services.ResetPlayerRuntimeState(client, resetHudState: true);
        _services.CheckHumanAlive();
    }

    private void Event_OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        var victim = @event.Entity;
        if (victim == null || !victim.IsValid || !victim.IsValidEntity)
            return;

        var victimPawn = victim.As<CCSPlayerPawn>();
        if (!victimPawn.IsValid)
            return;

        var victimPlayer = victimPawn.ToPlayer();
        if (victimPlayer == null || !victimPlayer.IsValid)
            return;

        var victimController = victimPlayer.Controller;
        if (!victimController.IsValid)
            return;

        bool inProtect = victimPlayer.PlayerID >= 0
            && victimPlayer.PlayerID < _globals.InProtect.Length
            && _globals.InProtect[victimPlayer.PlayerID];

        if ((victimController.TeamNum == 3 && inProtect) || (victimController.TeamNum == 2 && !_globals.GameStart))
        {
            @event.Info.Damage = 0;
            return;
        }

        var attackerHandle = @event.Info.Attacker;
        if (!attackerHandle.IsValid)
            return;

        var attackerInstance = attackerHandle.Value!;
        if (attackerInstance is not CCSPlayerPawn attackerPawn || !attackerPawn.IsValid)
            return;

        var attackerPlayer = attackerPawn.ToPlayer();
        if (attackerPlayer == null || !attackerPlayer.IsValid)
            return;

        var attackerController = attackerPlayer.Controller;
        if (!attackerController.IsValid)
            return;

        if (attackerController.TeamNum != 2 || victimController.TeamNum != 3)
            return;

        var zombieName = _services.GetCurrentZombieName(attackerPlayer.PlayerID);
        if (string.IsNullOrWhiteSpace(zombieName))
            return;

        if (TryGetZombieAdditionalDamage(zombieName, out var additionalDamage) && additionalDamage != 0f)
        {
            @event.Info.Damage += additionalDamage;
        }
    }

    private bool TryGetZombieAdditionalDamage(string zombieName, out float damage)
    {
        var zombieConfig = _zombieConfig.GetConfig();
        if (!ReferenceEquals(_cachedZombieDamageConfig, zombieConfig))
        {
            _zombieDamageByName.Clear();
            foreach (var zombie in zombieConfig.ZombieList)
            {
                if (!string.IsNullOrWhiteSpace(zombie.Name))
                {
                    _zombieDamageByName[zombie.Name] = zombie.Damage;
                }
            }

            _cachedZombieDamageConfig = zombieConfig;
        }

        return _zombieDamageByName.TryGetValue(zombieName, out damage);
    }

    private void Event_OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        var CFG = _mainConfig.CurrentValue;
        var ZombieCFG = _zombieConfig.GetConfig();
        var grenadeConfig = _grenadeConfig.GetConfig();

        var ZombieList = ZombieCFG.ZombieList;
        foreach (var zombie in ZombieList)
        {
            @event.AddItem(zombie.Model);
            Console.WriteLine($"PrecacheMod: {zombie.Model}");
        }
        if (!string.IsNullOrEmpty(CFG.PrecacheSoundEvent))
        {
            var soundList = CFG.PrecacheSoundEvent
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (var sound in soundList)
            {
                @event.AddItem(sound);
                Console.WriteLine($"PrecacheSound: {sound}");
            }
        }

        AddGrenadeResourceIfPresent(@event, grenadeConfig.FireGrenade.PrecacheSoundEvent);
        AddGrenadeResourceIfPresent(@event, grenadeConfig.LightGrenade.PrecacheSoundEvent);
        AddGrenadeResourceIfPresent(@event, grenadeConfig.FreezeGrenade.PrecacheSoundEvent);
        AddGrenadeResourceIfPresent(@event, grenadeConfig.FireGrenade.BurnParticle);
        AddGrenadeResourceIfPresent(@event, CFG.HumandefaultModel);
        @event.AddItem("particles/ui/hud/ui_map_def_utility_trail.vpcf");
        @event.AddItem("particles/burning_fx/barrel_burning_trail.vpcf");
        @event.AddItem("particles/environment/de_train/train_coal_dump_trails.vpcf");

    }

    private static void AddGrenadeResourceIfPresent(IOnPrecacheResourceEvent @event, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var item in value.Split(',').Select(entry => entry.Trim()).Where(entry => !string.IsNullOrWhiteSpace(entry)))
        {
            @event.AddItem(item);
        }
    }

    private HookResult OnHegrenadeDetonate(EventHegrenadeDetonate @event)
    {
        if (!_globals.GameStart)
            return HookResult.Continue;

        var thrower = @event.UserIdPlayer;
        if (thrower == null || !thrower.IsValid)
            return HookResult.Continue;

        var config = _grenadeConfig.GetConfig().FireGrenade;
        if (!_services.TryActivateSpecialGrenade(HanZriotSpecialGrenadeType.Fire, thrower, config.Enabled, config.AllowBots, config.RoundLimit, config.LifeLimit))
            return HookResult.Continue;

        var sound = _helpers.RandomSelectSound(config.Sound);
        if (!string.IsNullOrWhiteSpace(sound))
        {
            _helpers.EmitSoundToAll(sound);
        }

        SwiftlyS2.Shared.Natives.Vector position = new(@event.X, @event.Y, @event.Z);
        _helpers.DrawExpandingRing(position, config.ExplosionRadius, 255, 0, 0, 125);
        foreach (var zombie in _helpers.GetPlayersInRadius(position, config.ExplosionRadius, 2))
        {
            _helpers.ApplyDamage(thrower, zombie, config.ExplosionDamage, DamageTypes_t.DMG_BLAST);
            _helpers.ApplySpecialGrenadeBurn(thrower, zombie, config.BurnDamage, config.BurnDuration, config.BurnParticle, config.BurnSound);
        }

        return HookResult.Continue;
    }

    private HookResult OnFlashbangDetonate(EventFlashbangDetonate @event)
    {
        var config = _grenadeConfig.GetConfig().LightGrenade;
        if (!config.Enabled)
            return HookResult.Continue;

        if (!_globals.GameStart)
        {
            SuppressFlashbangDetonate(@event.EntityID);
            return HookResult.Continue;
        }

        var thrower = @event.UserIdPlayer;
        if (thrower == null || !thrower.IsValid
            || !_services.TryActivateSpecialGrenade(HanZriotSpecialGrenadeType.Light, thrower, true, config.AllowBots, config.RoundLimit, config.LifeLimit))
        {
            SuppressFlashbangDetonate(@event.EntityID);
            return HookResult.Continue;
        }

        TrackSpecialFlashbangEntity(@event.EntityID);

        SwiftlyS2.Shared.Natives.Vector position = new(@event.X, @event.Y, @event.Z);
        var light = _helpers.CreateGrenadeLight(position, config.LightRadius, config.Brightness, config.Sound);
        if (light == null || !light.IsValid || !light.IsValidEntity)
            return HookResult.Continue;

        uint lightIndex = light.Index;
        _globals.ActiveGrenadeLights[lightIndex] = light;
        var timer = _core.Scheduler.DelayBySeconds(config.Duration, () => _helpers.RemoveGrenadeLight(lightIndex));
        _core.Scheduler.StopOnMapChange(timer);
        _globals.ActiveGrenadeLightTimers[lightIndex] = timer;

        return HookResult.Continue;
    }

    private HookResult OnSmokegrenadeDetonate(EventSmokegrenadeDetonate @event)
    {
        var config = _grenadeConfig.GetConfig().FreezeGrenade;
        if (!config.Enabled)
            return HookResult.Continue;

        if (!_globals.GameStart)
        {
            RemoveSmokeGrenadeEntity(@event.EntityID);
            return HookResult.Continue;
        }

        var thrower = @event.UserIdPlayer;
        if (thrower == null || !thrower.IsValid
            || !_services.TryActivateSpecialGrenade(HanZriotSpecialGrenadeType.Freeze, thrower, true, config.AllowBots, config.RoundLimit, config.LifeLimit))
        {
            RemoveSmokeGrenadeEntity(@event.EntityID);
            return HookResult.Continue;
        }

        var sound = _helpers.RandomSelectSound(config.Sound);
        if (!string.IsNullOrWhiteSpace(sound))
        {
            _helpers.EmitSoundToAll(sound);
        }

        SwiftlyS2.Shared.Natives.Vector position = new(@event.X, @event.Y, @event.Z);
        _helpers.DrawExpandingRing(position, config.FreezeRadius, 0, 0, 255, 125);
        foreach (var zombie in _helpers.GetPlayersInRadius(position, config.FreezeRadius, 2))
        {
            _helpers.ApplyFreezeGrenade(zombie, config.FreezeDuration, config.FreezeSound, config.UnfreezeSound);
        }

        RemoveSmokeGrenadeEntity(@event.EntityID);

        return HookResult.Continue;
    }

    private HookResult OnPlayerBlind(EventPlayerBlind @event)
    {
        if (!_globals.SpecialFlashbangEntityIds.Contains(@event.EntityID))
            return HookResult.Continue;

        @event.BlindDuration = 0f;

        var pawn = @event.UserIdPawn;
        if (!pawn.IsValid)
            return HookResult.Continue;

        pawn.BlindUntilTime.Value = _core.Engine.GlobalVars.CurrentTime;

        return HookResult.Continue;
    }

    private HookResult OnWeaponFire(EventWeaponFire @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        if (pawn.TeamNum == 3 && _globals.InProtect[player.PlayerID])
        {
            _globals.InProtect[player.PlayerID] = false;
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["RemoveProtect"]}");
        }

        return HookResult.Continue;
    }

    private void ApplyCurrentDayWeaponModifiers()
    {
        var currentDay = _services.TryGetCurrentDayConfig();
        if (currentDay == null)
            return;

        if (!currentDay.NoRecoil && !currentDay.InfiniteReserveAmmo && !currentDay.InfiniteClipAmmo)
            return;

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player == null || !player.IsValid)
                continue;

            if(!player.IsAlive)
                continue;

            if (player.IsFakeClient)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            if(pawn.TeamNum != 3)
                continue;

            var weaponServices = pawn.WeaponServices;
            if (weaponServices == null || !weaponServices.IsValid)
                continue;

            if (currentDay.NoRecoil)
            {
                var aimPunchServices = pawn.AimPunchServices;
                if (aimPunchServices != null && aimPunchServices.IsValid)
                {
                    aimPunchServices.PredictableBaseAngle.Pitch = 0;
                    aimPunchServices.PredictableBaseAngle.Yaw = 0;
                    aimPunchServices.PredictableBaseAngle.Roll = 0;
                    aimPunchServices.PredictableBaseAngleVel.Pitch = 0;
                    aimPunchServices.PredictableBaseAngleVel.Yaw = 0;
                    aimPunchServices.PredictableBaseAngleVel.Roll = 0;
                }
            }

            var activeWeaponHandle = weaponServices.ActiveWeapon;
            if (!activeWeaponHandle.IsValid)
                continue;

            var activeWeapon = activeWeaponHandle.Value!;
            if (!activeWeapon.IsValid || !activeWeapon.IsValidEntity)
                continue;

            if (IsWeaponExcludedFromAmmoRules(activeWeapon.DesignerName))
                continue;

            if (currentDay.InfiniteReserveAmmo && activeWeapon.ReserveAmmo[0] < 1000)
            {
                activeWeapon.ReserveAmmo[0] = 1000;
            }

            if (currentDay.InfiniteClipAmmo && activeWeapon.Clip1 >= 0 && activeWeapon.Clip1 < 100)
            {
                activeWeapon.Clip1 = 100;
                activeWeapon.Clip1Updated();
            }
        }
    }

    private static bool IsWeaponExcludedFromAmmoRules(string? designerName)
    {
        return string.IsNullOrWhiteSpace(designerName)
            || designerName is "weapon_knife"
            or "weapon_hegrenade"
            or "weapon_flashbang"
            or "weapon_smokegrenade"
            or "weapon_molotov"
            or "weapon_incgrenade"
            or "weapon_decoy"
            or "weapon_c4"
            or "weapon_taser";
    }

    private void GiveConfiguredGrenadesOnSpawn(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return;

        if(player.IsFakeClient || !player.IsAlive)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.TeamNum != 3)
            return;

        var grenadeConfig = _grenadeConfig.GetConfig();

        if (grenadeConfig.FireGrenade.Enabled && grenadeConfig.FireGrenade.GiveOnSpawn && (!player.IsFakeClient || grenadeConfig.FireGrenade.AllowBots))
        {
            _helpers.GiveGrenade(player, "weapon_hegrenade");
        }

        if (grenadeConfig.LightGrenade.Enabled && grenadeConfig.LightGrenade.GiveOnSpawn && (!player.IsFakeClient || grenadeConfig.LightGrenade.AllowBots))
        {
            _helpers.GiveGrenade(player, "weapon_flashbang");
        }

        if (grenadeConfig.FreezeGrenade.Enabled && grenadeConfig.FreezeGrenade.GiveOnSpawn && (!player.IsFakeClient || grenadeConfig.FreezeGrenade.AllowBots))
        {
            _helpers.GiveGrenade(player, "weapon_smokegrenade");
        }
    }

    private void TrackSpecialFlashbangEntity(short entityId)
    {
        _globals.SpecialFlashbangEntityIds.Add(entityId);
        var timer = _core.Scheduler.DelayBySeconds(1.0f, () => _globals.SpecialFlashbangEntityIds.Remove(entityId));
        _core.Scheduler.StopOnMapChange(timer);
    }

    private void SuppressFlashbangDetonate(short entityId)
    {
        TrackSpecialFlashbangEntity(entityId);

        var entity = _core.EntitySystem.GetEntityByIndex<CFlashbangProjectile>((uint)entityId);
        if (entity != null && entity.IsValid && entity.IsValidEntity)
        {
            entity.AcceptInput("kill", 0);
        }
    }

    private void RemoveSmokeGrenadeEntity(short entityId)
    {
        var entity = _core.EntitySystem.GetEntityByIndex<CSmokeGrenadeProjectile>((uint)entityId);
        if (entity != null && entity.IsValid && entity.IsValidEntity)
        {
            entity.AcceptInput("kill", 0);
        }
    }

}
