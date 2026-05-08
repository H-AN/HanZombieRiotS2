using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

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
        _core.Event.OnEntityTakeDamage += Event_Protect;

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

        _core.Scheduler.NextTick(() =>
        {
            if (entity.IsValid && entity.IsValidEntity)
            {
                _helpers.CheckGrenadeSpawned(entity);
            }
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

            if (player.IsFakeClient)
            {
                if (team == 2)
                {
                    if (_globals.Countdown > 0)
                    {
                        _helpers.SetFreezeState(player, true);
                        _core.Scheduler.DelayBySeconds((float)_globals.Countdown, () =>
                        {
                            if (_services.IsRoundGenerationCurrent(roundGeneration) && player is { IsValid: true })
                            {
                                _helpers.SetFreezeState(player, false);
                            }
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
        if (playerController == null || !playerController.IsValid)
            return HookResult.Continue;

        var playerPawn = player.PlayerPawn;
        if (playerPawn == null || !playerPawn.IsValid)
            return HookResult.Continue;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var attackerPawn = attacker.PlayerPawn;
        if (attackerPawn == null || !attackerPawn.IsValid)
            return HookResult.Continue;

        var attackerController = attacker.Controller;
        if (attackerController == null || !attackerController.IsValid)
            return HookResult.Continue;

        var weapon = @event.Weapon;
        var dmgHealth = @event.DmgHealth;
        var hitgroup = @event.HitGroup;

        if (attackerPawn.TeamNum == 3 && playerPawn.TeamNum == 2)
        {
            int LeftZombie = _globals.NeedKillZombie - _globals.ZombieKill;
            var remainingHP = playerPawn.Health;

            var CFG = _mainConfig.CurrentValue;

            if (CFG.HurtMoney)
            {
                _helpers.GiveCash(attacker, dmgHealth, "hurt");
            }

            if (hitgroup == 1)
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
        bool attackerValid = attacker is { IsValid: true };

        var deather = @event.UserIdPlayer;
        if (deather == null || !deather.IsValid)
            return HookResult.Continue;

        var deatherController = @event.UserIdController;
        if (deatherController == null || !deatherController.IsValid)
            return HookResult.Continue;

        var deatherPawn = deather.PlayerPawn;
        if (deatherPawn == null || !deatherPawn.IsValid)
            return HookResult.Continue;
        byte deathTeam = deatherPawn.TeamNum;

        int roundGeneration = _helpers.GetCurrentRoundGeneration();
        var dayConfig = _dayConfig.GetConfig();
        var CFG = _mainConfig.CurrentValue;
        int maxDay = dayConfig.Days.Count;

        _services.ClearPendingHumanRespawn(deather.PlayerID);

        _core.Scheduler.NextTick(() =>
        {
            if (!_services.IsRoundGenerationCurrent(roundGeneration))
                return;

            if (deathTeam == 2)
            {
                var deatherControllerEntity = deatherController.Entity;
                if (deatherControllerEntity != null && deatherControllerEntity.IsValid)
                {
                    deatherControllerEntity.Name = "";
                }
                _services.SetCurrentZombieName(deather.PlayerID, null);
                _helpers.ClearPlayerGrenadeEffects(deather.PlayerID);
                _globals.g_ZombieRegenStates.Remove(deather.PlayerID);

                if (_globals.GameStart)
                {
                    _core.Scheduler.DelayBySeconds(1.0f, () =>
                    {
                        if (_services.IsRoundGenerationCurrent(roundGeneration))
                        {
                            _helpers.RespawnClient(deatherController);
                        }
                    });

                    if (attackerValid && CFG.DeathMoney > 0)
                    {
                        _helpers.GiveCash(attacker!, CFG.DeathMoney, "death");
                    }

                    if (CFG.SoundZombieDead && !string.IsNullOrWhiteSpace(CFG.SoundEventZombieDead))
                    {
                        var deadSounds = _helpers.RandomSelectSound(CFG.SoundEventZombieDead);
                        if (deadSounds != null)
                        {
                            _helpers.EmitSoundToEntity(deather, deadSounds);
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

            if (deathTeam == 3)
            {
                if (_globals.GameStart)
                {
                    bool respawnAsZombie = _services.ConsumeHumanDeathAndCheckZombieRespawn(deather.PlayerID);

                    if (_globals.AllowHumanZombie && respawnAsZombie)
                    {
                        _core.Scheduler.DelayBySeconds(1.0f, () =>
                        {
                            if (_services.IsRoundGenerationCurrent(roundGeneration))
                            {
                                _helpers.RespawnClient(deatherController);
                            }
                        });
                    }
                    else
                    {
                        float respawnSeconds = _globals.RebornSec[deather.PlayerID] > 0
                            ? _globals.RebornSec[deather.PlayerID]
                            : CFG.HumanRebornSec;
                        int respawnDelay = _services.QueuePendingHumanRespawn(deather.PlayerID, respawnSeconds);
                        deather.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(deather)["DeathInfo", respawnDelay]}");

                        if (respawnDelay > 0)
                        {
                            deather.SendMessage(MessageType.CenterHTML, $"{_core.Translation.GetPlayerLocalizer(deather)["ReSpawn", respawnDelay]}");
                        }
                        else
                        {
                            _core.Scheduler.NextTick(() =>
                            {
                                if (_services.IsRoundGenerationCurrent(roundGeneration))
                                {
                                    _helpers.RespawnClient(deatherController);
                                }
                            });
                        }
                    }
                }
                else
                {
                    _core.Scheduler.DelayBySeconds(1.0f, () =>
                    {
                        if (_services.IsRoundGenerationCurrent(roundGeneration))
                        {
                            deatherController.Respawn();
                        }
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

        var playerpawn = player.PlayerPawn;
        if (playerpawn == null || !playerpawn.IsValid)
            return HookResult.Continue;

        var playerController = player.Controller;
        if (playerController == null || !playerController.IsValid)
            return HookResult.Continue;

        int roundGeneration = _helpers.GetCurrentRoundGeneration();
        var CFG = _mainConfig.CurrentValue;

        if (!player.IsFakeClient)
        {
            _services.ClearPendingHumanRespawn(player.PlayerID);

            if (_services.IsPlayerMarkedForZombieRespawn(player.PlayerID))
            {
                if (playerpawn.TeamNum != 2)
                {
                    player.SwitchTeam(Team.T);
                }

                _core.Scheduler.DelayBySeconds(0.05f, () =>
                {
                    if (_services.IsRoundGenerationCurrent(roundGeneration))
                    {
                        _services.PossZombie(player);
                    }
                });

                if (!_globals.GameStart)
                {
                    _core.Scheduler.DelayBySeconds(0.5f, () =>
                    {
                        if (_services.IsRoundGenerationCurrent(roundGeneration) && player is { IsValid: true })
                        {
                            _helpers.SetFreezeState(player, true);
                        }
                    });
                }

                return HookResult.Continue;
            }

            _globals.RebornSec[player.PlayerID] = (int)Math.Ceiling(CFG.HumanRebornSec);
            _services.SetCurrentZombieName(player.PlayerID, null);
            _services.ResetSpecialGrenadeLifeUsage(player.PlayerID);
            if (playerpawn.TeamNum != 3)
            {
                player.SwitchTeam(Team.CT);
            }

            _core.Scheduler.DelayBySeconds(0.05f, () =>
            {
                if (player is { IsValid: true })
                {
                    _helpers.ApplyHumanDefaultModel(player);
                    GiveConfiguredGrenadesOnSpawn(player);
                }
            });

            if (_globals.AllowHumanZombie && _globals.BeAZombie[player.PlayerID] == 0)
            {
                _services.ResetPlayerCorpseModeToCurrentDay(player.PlayerID);
            }

            if (CFG.SpawnProtect)
            {
                _core.Scheduler.DelayBySeconds(0.2f, () =>
                {
                    if (_services.IsRoundGenerationCurrent(roundGeneration) && player is { IsValid: true })
                    {
                        _globals.InProtect[player.PlayerID] = true;
                    }
                });

                _globals.SpawnProtect[player.PlayerID]?.Cancel();
                _globals.SpawnProtect[player.PlayerID] = null;
                _globals.SpawnProtect[player.PlayerID] = _core.Scheduler.DelayBySeconds(CFG.SpawnProtectCount, () =>
                {
                    if (_services.IsRoundGenerationCurrent(roundGeneration))
                    {
                        _helpers.DeleSpawnProtect(player);
                    }
                });
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["SpawnProtect", CFG.SpawnProtectCount]}");
            }

            if (CFG.HumanNoBlock)
            {
                var pawn = player.PlayerPawn;
                if (pawn != null && pawn.IsValid)
                {
                    _core.Scheduler.NextTick(() =>
                    {
                        _helpers.NoBlock(pawn);
                    });
                }
            }

        }
        else
        {
            if (playerpawn.TeamNum != 2)
            {
                player.SwitchTeam(Team.T);
            }
            _core.Scheduler.DelayBySeconds(0.05f, () =>
            {
                    _services.PossZombie(player);
            });
            if (!_globals.GameStart)
            {
                _core.Scheduler.DelayBySeconds(0.5f, () =>
                {
                    if (_services.IsRoundGenerationCurrent(roundGeneration) && player is { IsValid: true })
                    {
                        _helpers.SetFreezeState(player, true);
                    }
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
        if (victim == null || !victim.IsValid)
            return;

        var VictimPawn = victim.As<CCSPlayerPawn>();
        if (VictimPawn == null || !VictimPawn.IsValid)
            return;

        var VictimController = VictimPawn.Controller.Value?.As<CCSPlayerController>();
        if (VictimController == null || !VictimController.IsValid)
            return;

        var VictimPlayer = _core.PlayerManager.GetPlayerFromController(VictimController);
        if (VictimPlayer == null || !VictimPlayer.IsValid)
            return;

        var attacker = @event.Info.Attacker.Value;
        if (attacker == null || !attacker.IsValid)
            return;

        var AttackerPawn = attacker.As<CCSPlayerPawn>();
        if (AttackerPawn == null || !AttackerPawn.IsValid)
            return;

        var AttackerController = AttackerPawn.Controller.Value?.As<CCSPlayerController>();
        if (AttackerController == null || !AttackerController.IsValid)
            return;

        var AttackerPlayer = _core.PlayerManager.GetPlayerFromController(AttackerController);
        if (AttackerPlayer == null || !AttackerPlayer.IsValid)
            return;

        var ZombieCFG = _zombieConfig.GetConfig();//.CurrentValue;
        var ZombieList = ZombieCFG.ZombieList;

        var AttackerControllerEntity = AttackerController.Entity;
        if (AttackerControllerEntity == null || !AttackerControllerEntity.IsValid)
            return;

        if (AttackerController.TeamNum == 2 && VictimController.TeamNum == 3)
        {
            foreach (var zombie in ZombieList)
            {
                if (AttackerControllerEntity.Name == zombie.Name)
                {
                    @event.Info.Damage += zombie.Damage;
                    VictimPlayer.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(VictimPlayer)["ZombieDamage", AttackerController.PlayerName, VictimController.PlayerName, @event.Info.Damage]}");
                }
            }
        }
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

        AddGrenadeResourceIfPresent(@event, grenadeConfig.FireGrenade.Sound);
        AddGrenadeResourceIfPresent(@event, grenadeConfig.LightGrenade.Sound);
        AddGrenadeResourceIfPresent(@event, grenadeConfig.FreezeGrenade.Sound);
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
            _helpers.ApplySpecialGrenadeBurn(thrower, zombie, config.BurnDamage, config.BurnDuration, config.BurnParticle, string.Empty);
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
        if (light == null || !light.IsValid)
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
            _helpers.ApplyFreezeGrenade(zombie, config.FreezeDuration);
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
        if (pawn != null && pawn.IsValid)
        {
            pawn.BlindUntilTime.Value = _core.Engine.GlobalVars.CurrentTime;
        }

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
            if (player is not { IsValid: true })
                continue;

            var controller = player.Controller;
            if (controller == null || !controller.IsValid || controller.TeamNum != (byte)Team.CT || !controller.PawnIsAlive)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var weaponServices = pawn.WeaponServices;
            if (weaponServices == null || !weaponServices.IsValid)
                continue;

            var activeWeapon = weaponServices.ActiveWeapon.Value;
            if (activeWeapon == null || !activeWeapon.IsValid)
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
        if (player is not { IsValid: true })
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || controller.TeamNum != (byte)Team.CT || !controller.PawnIsAlive)
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

    private void Event_Protect(IOnEntityTakeDamageEvent @event)
    {
        var victim = @event.Entity;
        if (victim == null || !victim.IsValid)
            return;

        var VictimPawn = victim.As<CCSPlayerPawn>();
        if (VictimPawn == null || !VictimPawn.IsValid)
            return;

        var VictimController = VictimPawn.Controller.Value?.As<CCSPlayerController>();
        if (VictimController == null || !VictimController.IsValid)
            return;

        var VictimPlayer = _core.PlayerManager.GetPlayerFromController(VictimController);
        if (VictimPlayer == null || !VictimPlayer.IsValid)
            return;

        if (VictimController.TeamNum == 3 && _globals.InProtect[VictimPlayer.PlayerID])
        {
            @event.Info.Damage = 0;
        }

        if (VictimController.TeamNum == 2 && !_globals.GameStart)
        {
            @event.Info.Damage = 0;
        }

    }

}
