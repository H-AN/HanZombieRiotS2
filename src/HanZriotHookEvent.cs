using System.Numerics;
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
    private readonly HanZriotHelpers _helpers;
    private readonly HanZriotGlobals _globals;
    private readonly HanZriotHud _hud;
    private readonly HanZriotService _services;
    public HanZriotEvents(ISwiftlyCore core, ILogger<HanZriotEvents> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig,
        HanZriotHelpers helpers, HanZriotGlobals globals,
        HanZriotHud hud, HanZriotService Service)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
        _helpers = helpers;
        _globals = globals;
        _hud = hud;
        _services = Service;
    }

    public void HookEvents()
    {
        // GameEvent Hook
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerHurt);
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        _core.GameEvent.HookPre<EventPlayerJump>(OnPlayerJump);

        _core.GameEvent.HookPre<EventWeaponFire>(OnWeaponFire);


        // Event 系列 Hook
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.Event.OnMapUnload += Event_MapEnd;
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
        _core.Event.OnEntityTakeDamage += Event_OnEntityTakeDamage;
        _core.Event.OnTick += Event_OnTick;
        _core.Event.OnWeaponServicesCanUseHook += Event_OnWeaponServicesCanUseHook;

        _core.Event.OnTick += Event_OnTickJump;

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
    }

    private void Event_MapEnd(IOnMapUnloadEvent @event)
    {
        _globals.RiotDay = 1;
        _core.Engine.ExecuteCommand("bot_quota 0");
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {

        _core.Engine.ExecuteCommand("bot_quota_mode fill");
        _core.Engine.ExecuteCommand("bot_quota 20");

        _core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            _helpers.GlobalHudTimer();
            _helpers.ZombieRegenTimer();
            _helpers.HumanDeathCountDown();
        });


        var allPlayers = _core.PlayerManager.GetAllPlayers();

        var currentDay = _helpers.GetCurrentDay(_globals.RiotDay);

        _globals.NeedKillZombie = currentDay.Count;
        _globals.ZombieKill = 0;

        
        _globals.SpawnAllZombie?.Cancel();
        _globals.SpawnAllZombie = null;
        _globals.SpawnAllZombie = _core.Scheduler.DelayAndRepeatBySeconds(0.5f, 5.0f, () =>
        {
            _helpers.RespawnAllZombie();
        });
        _core.Scheduler.StopOnMapChange(_globals.SpawnAllZombie);
        
        

        _globals.g_DeathCheck?.Cancel();
        _globals.g_DeathCheck = null;
        _globals.SpawnAllZombie = _core.Scheduler.DelayAndRepeatBySeconds(0.2f, 3.0f, () =>
        {
            _helpers.ChangeBotTeam();
            _services.CheckHumanAlive();
        });
        _core.Scheduler.StopOnMapChange(_globals.SpawnAllZombie);

        var CFG = _mainConfig.CurrentValue;
        if (CFG.SoundRoundstartMusic)
        {
            _helpers.EmitSoundToAll(CFG.SoundEventRoundstartMusic);
        }
        if (CFG.SoundAmbSound)
        {
            _globals.g_hAmbMusic?.Cancel();
            _globals.g_hAmbMusic = null;
            _globals.g_hAmbMusic = _core.Scheduler.DelayAndRepeatBySeconds(0.0f, CFG.AmbSoundLoopTime, () => _helpers.PlayAmbSound());
            _core.Scheduler.StopOnMapChange(_globals.g_hAmbMusic);
        }


        if (CFG.FreezeZombie > 0)
        {
            _globals.Countdown = (int)Math.Ceiling(CFG.FreezeZombie);

            _globals.g_hCountdown?.Cancel();
            _globals.g_hCountdown = null;
            _globals.g_hCountdown = _core.Scheduler.DelayAndRepeatBySeconds(0.2f, 1.0f, () => _services.Round_Countdown());
            _core.Scheduler.StopOnMapChange(_globals.g_hCountdown);
        }
        else
        {
            _globals.GameStart = true;

            _helpers.SetAllZombieUnFreeze();

            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsValid)
                    continue;

                if (!player.IsFakeClient)
                    continue; // 只处理玩家

                var pawn = player.PlayerPawn;
                if (pawn == null || !pawn.IsValid)
                    continue;

                var team = pawn.TeamNum;
                if (team == 3)
                {
                    player.SendMessage(MessageType.CenterHTML, $"{_core.Translation.GetPlayerLocalizer(player)["ZombieStartMove"]}");
                }
            }
            if (CFG.SoundZombieStart)
            {
                _helpers.EmitSoundToAll(CFG.SoundEventZombieStart);
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
                            _helpers.SetFreezeState(player, false);
                        });
                    }
                }
            }
            else
            {
                if (_globals.AllowHumanZombie)
                {
                    _core.Scheduler.DelayBySeconds(1f, () => { _globals.BeAZombie[slot] = currentDay.BeforeZombie; });
                }
            }
            _globals.g_DeadCountDown[slot]?.Cancel();
            _globals.g_DeadCountDown[slot] = null;
        }

        _helpers.RemoveRoundObjective();

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event)
    {
        var clientId = @event.UserId;

        var playerController = @event.UserIdController;
        if (playerController == null || !playerController.IsValid)
            return HookResult.Continue;

        IPlayer player = _core.PlayerManager.GetPlayer(clientId);

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var playerPawn = player.PlayerPawn;
        if (playerPawn == null || !playerPawn.IsValid)
            return HookResult.Continue;

        var attackerId = @event.Attacker;

        IPlayer attacker = _core.PlayerManager.GetPlayer(attackerId);

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
                if (CFG.SoundZombiePain)
                {

                    int randomPain = Random.Shared.Next(0, 5);
                    if (randomPain == 1)
                    {
                        _helpers.EmitSoundToEntity(player, CFG.SoundEventZombiePain);
                    }
                }
            }
            else
            {
                if (CFG.SoundZombieHurt)
                {
                    _helpers.EmitSoundToEntity(player, CFG.SoundEventZombieHurt);
                }

            }

            if (_globals.g_hCountdown == null)
            {
                if(!_globals.PlayerDmgHud[attacker.PlayerID])
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
        var DeatherId = @event.UserId;
        var attackerId = @event.Attacker;

        var DeatherController = @event.UserIdController;
        if (DeatherController == null || !DeatherController.IsValid)
            return HookResult.Continue;

        IPlayer Deather = _core.PlayerManager.GetPlayer(DeatherId);
        if (Deather == null || !Deather.IsValid)
            return HookResult.Continue;

        var DeatherPawn = Deather.PlayerPawn;
        if (DeatherPawn == null || !DeatherPawn.IsValid)
            return HookResult.Continue;


        var Dayconfig = _dayConfig.GetConfig();

        var CFG = _mainConfig.CurrentValue;
        int maxDay = Dayconfig.Days.Count;

        _core.Scheduler.NextTick(() =>
        {
            /*
            var HumanCount = _core.PlayerManager.GetAllPlayers()
            .Where(humans =>
                humans.PlayerPawn is { IsValid: true, TeamNum: 3 } &&
                humans.Controller is { IsValid: true } &&
                humans.Controller.PawnIsAlive)
            .Count();
            */
            var HumanCount = _core.PlayerManager.GetAllPlayers()
            .Where(p =>
                p is { IsValid: true } &&
                p.Controller is { IsValid: true, TeamNum: 3 } controller &&
                controller.PlayerPawn is { IsValid: true } pawn &&
                controller.PawnIsAlive
            )
            .Count();

            if (DeatherPawn.TeamNum == 2)
            {
                var DeatherControllerEntity = DeatherController.Entity;
                if (DeatherControllerEntity != null && DeatherControllerEntity.IsValid)
                {
                    DeatherControllerEntity.Name = "";
                }
                _globals.g_ZombieRegenStates.Remove(Deather.PlayerID);

                if (_globals.GameStart == true)
                {

                    _core.Scheduler.DelayBySeconds(1.0f, () => { _helpers.RespawnClient(DeatherController); });
                    if (CFG.DeathMoney > 0)
                    {
                        
                        var attacker = _core.PlayerManager.GetPlayer(attackerId);
                        if (attacker == null || !attacker.IsValid)
                            return;

                        _helpers.GiveCash(attacker, CFG.DeathMoney, "death");
                    }

                    if (CFG.SoundZombieDead)
                    {
                        _helpers.EmitSoundToEntity(Deather, CFG.SoundEventZombieDead);
                    }
                    _globals.ZombieKill++;
                    _helpers.UpdateKillCount();
                    if (_globals.ZombieKill >= _globals.NeedKillZombie)
                    {
                        _globals.ZombieKill = 0;
                        if (_globals.RiotDay < maxDay)
                        {
                            _services.FakeCtswin();
                            _globals.RiotDay++;
                        }
                        else
                        {
                            _services.FakeCtswin();
                            _globals.RiotDay = maxDay;
                        }

                    }
                }
            }
            if (DeatherPawn.TeamNum == 3)
            {
                if (_globals.GameStart)
                {
                    if (_globals.AllowHumanZombie)
                    {
                        if (_globals.BeAZombie[Deather.PlayerID] > -1)
                        {
                            _globals.BeAZombie[Deather.PlayerID]--;
                        }
                    }

                    if (HumanCount > 0)
                    {
                        if (_globals.AllowHumanZombie)
                        {
                            if (_globals.BeAZombie[Deather.PlayerID] < 0)
                            {
                                _core.Scheduler.DelayBySeconds(1.0f, () => { _helpers.RespawnClient(DeatherController); });
                            }
                            else
                            {
                                Deather.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(Deather)["DeathInfo", (int)_globals.RebornSec[Deather.PlayerID]]}");
                                var now = Environment.TickCount;
                                _globals.DeathTime[Deather.PlayerID] = now + ((int)_globals.RebornSec[Deather.PlayerID] * 1000);
                            }
                        }
                        else
                        {
                            Deather.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(Deather)["DeathInfo", (int)_globals.RebornSec[Deather.PlayerID]]}");

                            var now = Environment.TickCount;
                            _globals.DeathTime[Deather.PlayerID] = now + ((int)_globals.RebornSec[Deather.PlayerID] * 1000);

                        }

                    }
                }
                else
                {
                    _core.Scheduler.DelayBySeconds(1.0f, () => { DeatherController.Respawn(); });
                    
                }
                

            }


        });

        return HookResult.Continue;
    }

    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        var clientId = @event.UserId;

        IPlayer player = _core.PlayerManager.GetPlayer(clientId);
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var clienpawn = player.PlayerPawn;
        if (clienpawn == null || !clienpawn.IsValid)
            return HookResult.Continue;

        var playerController = player.Controller;
        if (playerController == null || !playerController.IsValid)
            return HookResult.Continue;

        var CFG = _mainConfig.CurrentValue;

        if (!player.IsFakeClient)
        {
            if (_globals.AllowHumanZombie)
            {
                if (_globals.BeAZombie[player.PlayerID] < 0)
                {
                    if (_globals.RiotDay != 1)
                    {
                        player.SwitchTeam(Team.T);
                        _core.Scheduler.DelayBySeconds(0.2f, () => _services.PossZombie(player));
                        _globals.BeAZombie[player.PlayerID] = 0;
                    }
                    else
                    {
                        _globals.RebornSec[player.PlayerID] = (int)Math.Ceiling(CFG.HumanRebornSec);
                        if (clienpawn.TeamNum != 3)
                        {
                            player.SwitchTeam(Team.CT);
                        }

                        if (CFG.SpawnProtect)
                        {
                            if (clienpawn != null)
                            {
                                _core.Scheduler.DelayBySeconds(0.2f, () => { _globals.InProtect[player.PlayerID] = true;});
                            }

                            _globals.SpawnProtect[player.PlayerID]?.Cancel();
                            _globals.SpawnProtect[player.PlayerID] = null;
                            _globals.SpawnProtect[player.PlayerID] = _core.Scheduler.DelayBySeconds(CFG.SpawnProtectCount, () => { _helpers.DeleSpawnProtect(player); });

                            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["SpawnProtect", CFG.SpawnProtectCount]}");
                        }
                    }
                }
                else
                {
                    _globals.RebornSec[player.PlayerID] = (int)Math.Ceiling(CFG.HumanRebornSec);
                    if (clienpawn.TeamNum != 3)
                    {
                        player.SwitchTeam(Team.CT);
                    }
                }
            }
            else
            {
                _globals.RebornSec[player.PlayerID] = (int)Math.Ceiling(CFG.HumanRebornSec);
                if (clienpawn.TeamNum != 3)
                {
                    player.SwitchTeam(Team.CT);
                }

                if (CFG.SpawnProtect)
                {
                    if (clienpawn != null)
                        _core.Scheduler.DelayBySeconds(0.2f, () => { _globals.InProtect[player.PlayerID] = true; });

                    _globals.SpawnProtect[player.PlayerID]?.Cancel();
                    _globals.SpawnProtect[player.PlayerID] = null;
                    _globals.SpawnProtect[player.PlayerID] = _core.Scheduler.DelayBySeconds(CFG.SpawnProtectCount, () => { _helpers.DeleSpawnProtect(player); });
                    player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["SpawnProtect", CFG.SpawnProtectCount]}");
                }


            }
            
            if (CFG.HumanNoBlock)
            {
                var pawn = player.PlayerPawn;
                if (pawn != null && pawn.IsValid)
                {
                    _core.Scheduler.NextTick( () => 
                    { 
                        _helpers.NoBlock(pawn); 
                    });
                }
            }
            
        }
        else
        {
            if (clienpawn.TeamNum != 2)
            {
                player.SwitchTeam(Team.T);
            }
            _core.Scheduler.DelayBySeconds(0.2f, () => _services.PossZombie(player));
            if (!_globals.GameStart)
            {
                _core.Scheduler.DelayBySeconds(0.5f, () => _helpers.SetFreezeState(player, true)); 
            }
        }

        return HookResult.Continue;
    }


    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var client = @event.PlayerId;

        _globals.g_DeadCountDown[client]?.Cancel();
        _globals.g_DeadCountDown[client] = null;

        _globals.g_ZombieRegenStates.Remove(client);


        _globals.SpawnProtect[client]?.Cancel();
        _globals.SpawnProtect[client] = null;

        _globals.RebornSec[client] = 0.0f;
        _globals.BeAZombie[client] = 0;
        _globals.DeathTime[client] = 0;
        _globals.InProtect[client] = false;
        _globals.PlayerHud[client] = false;
        _globals.PlayerDmgHud[client] = false;

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

        var VictimPlayer = HanExtensions.GetPlayerByController(VictimController, _core);
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

        var AttackerPlayer = HanExtensions.GetPlayerByController(AttackerController, _core);
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

    }

    public HookResult OnPlayerJump(EventPlayerJump @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = @event.UserIdPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        int now = _core.Engine.GlobalVars.TickCount;
        int duration = 8; // 8 Tick ≈ 0.12 秒

        // 持续 8 Tick（大约 0.12 秒）
        _globals.jumpBoostState[player] = now + duration;

        return HookResult.Continue;
    }

    private void Event_OnTickJump()
    {
        var ZombieCFG = _zombieConfig.GetConfig();
        var ZombieList = ZombieCFG.ZombieList;

        int nowTick = _core.Engine.GlobalVars.TickCount;

        foreach (var kv in new Dictionary<IPlayer, int>(_globals.jumpBoostState))
        {
            var player = kv.Key;
            if (player == null || !player.IsValid)
                return;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                return;

            var Controller = player.Controller;
            if (Controller == null || !Controller.IsValid)
                return;

            var ControllerEntity = Controller.Entity;
            if (ControllerEntity == null || !ControllerEntity.IsValid)
                return;

            int endTick = kv.Value;

            if (player == null || !player.IsValid)
            {
                _globals.jumpBoostState.Remove(player);
                continue;
            }

            if(pawn.TeamNum == 2)
            {
                foreach (var zombie in ZombieList)
                {
                    if (ControllerEntity.Name == zombie.Name)
                    {
                        if (nowTick <= endTick)
                        {
                            pawn.AbsVelocity.Z = 300.0f * zombie.Gravity;
                        }
                        else
                        {
                            _globals.jumpBoostState.Remove(player);
                        }

                    }
                }
            }
        }
    }

    private HookResult OnWeaponFire(EventWeaponFire @event)
    {
        var player = @event.UserIdPlayer;
        if(player == null || !player.IsValid)
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

        var VictimPlayer = HanExtensions.GetPlayerByController(VictimController, _core);
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

        var AttackerPlayer = HanExtensions.GetPlayerByController(AttackerController, _core);
        if (AttackerPlayer == null || !AttackerPlayer.IsValid)
            return;

        var ZombieCFG = _zombieConfig.GetConfig();
        var ZombieList = ZombieCFG.ZombieList;

        var AttackerControllerEntity = AttackerController.Entity;
        if (AttackerControllerEntity == null || !AttackerControllerEntity.IsValid)
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