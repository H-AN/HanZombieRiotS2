using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
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


    public HanZriotService(ISwiftlyCore core, ILogger<HanZriotService> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig,
        HanZriotHelpers helpers, HanZriotGlobals globals)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
        _helpers = helpers;
        _globals = globals;
    }

    public void PossZombie(IPlayer client) //应用僵尸各项属性
    {
        if (client == null || !client.IsValid)
            return;

        var clientpawn = client.PlayerPawn;
        if (clientpawn == null || !clientpawn.IsValid)
            return;

        var Controller = client.Controller;
        if (Controller == null || !Controller.IsValid)
            return;

        var CFG = _mainConfig.CurrentValue;
        var Dayconfig = _dayConfig.GetConfig(); //.CurrentValue;

        clientpawn.ItemServices!.GiveItem<CCSWeaponBase>("weapon_knife");
        

        if (CFG.ZombieNoBlock)
        {
            _helpers.NoBlock(clientpawn);
        }

        // 获取当前关卡的僵尸数据
        var currentDay = _helpers.GetCurrentDay(_globals.RiotDay);// RiotDay 从 1 开始
        int currentDayIndex = Dayconfig.Days.IndexOf(currentDay); // 获取当前关卡的索引
        var zombiesForLevel = _helpers.GetZombiesForCurrentLevel(currentDayIndex); // 传递索引



        // 随机选择一个僵尸
        if (zombiesForLevel.Count > 0)
        {
            var randomZombie = zombiesForLevel[Random.Shared.Next(zombiesForLevel.Count)]; // 随机选择一个僵尸

            int maxhealth;
            if (currentDay.HealthBoost > 0)
            {
                maxhealth = currentDay.HealthBoost + randomZombie.Health;
            }
            else
            {
                maxhealth = randomZombie.Health;
            }

            clientpawn.SetModel(randomZombie.Model); // 设置模型
            // 设置属性
            if (currentDay.HealthBoost > 0)
            {
                
                clientpawn.Health = randomZombie.Health + currentDay.HealthBoost;
                clientpawn.HealthUpdated();
                
            }
            else
            {
                
                clientpawn.Health = randomZombie.Health;
                clientpawn.HealthUpdated();
                

            }
            if (randomZombie.Speed > 0)
            {
                clientpawn.VelocityModifier = randomZombie.Speed;
            }
            else
            {
                clientpawn.VelocityModifier = 1.0f;
            }

            if (randomZombie.HealthRevive > 0)
            {
                var now = Environment.TickCount / 1000f; 
                _globals.g_ZombieRegenStates[client.PlayerID] = new ZombieRegenState
                {
                    PlayerID = client.PlayerID,
                    RegenAmount = randomZombie.HealthReviveHp,
                    RegenInterval = randomZombie.HealthReviveSec,
                    NextRegenTime = now + randomZombie.HealthReviveSec // 下一次回血时间
                };
            }

            var ControllerEntity = Controller.Entity;
            if (ControllerEntity != null && ControllerEntity.IsValid)
            {
                ControllerEntity.Name = randomZombie.Name;
            }
        }
        else
        {
            _core.Logger.LogError($"{_core.Localizer["NoZombieData"]}");
        }

    }

    public void Round_Countdown()
    {
        var CFG = _mainConfig.CurrentValue;
        var soundList = CFG.SoundEventCountdown.Split(',');

        if (_globals.Countdown > 0)
        {
            _globals.Countdown--;
        }
        if (_globals.Countdown == 20 && _globals.Countdown != 0)
        {
            if (CFG.Soundremaining)
            {
                _helpers.EmitSoundToAll(CFG.SoundEventremaining);
            }
        }
        else if (_globals.Countdown == 10 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound10 = soundList[9];
                _helpers.EmitSoundToAll(sound10);
            }
        }
        else if (_globals.Countdown == 9 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound9 = soundList[8];
                _helpers.EmitSoundToAll(sound9);
            }
        }
        else if (_globals.Countdown == 8 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound8 = soundList[7];
                _helpers.EmitSoundToAll(sound8);
            }
        }
        else if (_globals.Countdown == 7 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound7 = soundList[6];
                _helpers.EmitSoundToAll(sound7);
            }
        }
        else if (_globals.Countdown == 6 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound6 = soundList[5];
                _helpers.EmitSoundToAll(sound6);
            }
        }
        else if (_globals.Countdown == 5 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound5 = soundList[4];
                _helpers.EmitSoundToAll(sound5);
            }
        }
        else if (_globals.Countdown == 4 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound4 = soundList[3];
                _helpers.EmitSoundToAll(sound4);
            }
        }
        else if (_globals.Countdown == 3 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound3 = soundList[2];
                _helpers.EmitSoundToAll(sound3);
            }
        }
        else if (_globals.Countdown == 2 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound2 = soundList[1];
                _helpers.EmitSoundToAll(sound2);
            }
        }
        else if (_globals.Countdown == 1 && _globals.Countdown != 0)
        {
            if (CFG.SoundCountdown)
            {
                string sound1 = soundList[0];
                _helpers.EmitSoundToAll(sound1);
            }
        }
        else if (_globals.Countdown == 0)
        {
            if (CFG.SoundZombieStart)
            {
                _helpers.EmitSoundToAll(CFG.SoundEventZombieStart);
            }
            _helpers.SetAllZombieUnFreeze();
        }
        if (_globals.Countdown <= 0)
        {
            _globals.g_hCountdown?.Cancel();
            _globals.g_hCountdown = null;
            _globals.GameStart = true;
        }
        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (player.IsValid)
            {
                var pawn = player.PlayerPawn;
                if (pawn!=null && pawn.IsValid)
                {
                    if (pawn.TeamNum == 3)
                    {
                        if (!player.IsFakeClient)
                        {
                            player.SendMessage(MessageType.Center, $"{_globals.Countdown} {_core.Translation.GetPlayerLocalizer(player)["MoveZombie"]}");
                        }
                    }
                }
            }

        }
    }


    public void FakeCtswin() 
    {
        var CFG = _mainConfig.CurrentValue;
        var Dayconfig = _dayConfig.GetConfig();
        var currentDay = _helpers.GetCurrentDay(_globals.RiotDay);

        int maxDay = Dayconfig.Days.Count; // 获取关卡总数
        _globals.GameStart = false;

        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (!player.IsFakeClient)
            {
                if (_globals.RiotDay == maxDay)
                {
                    string Message = $"<span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["ClearZombieOnMap"]}</font></span><br>" +
                    $"<span><span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["HumanTakeOver"]}</font></span><br>" +
                    $"<span><span><font color='#00FF00'>{_core.Translation.GetPlayerLocalizer(player)["NextMap"]}</font></span>";
                    player.SendMessage(MessageType.CenterHTML, $"{Message}");
                }
                else
                {
                    string Message = $"<span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["HumanWins"]}</font></span><br>" +
                    $"<span><font color='#E22D2D'>{_core.Translation.GetPlayerLocalizer(player)["ZombieClear"]}</font></span><br>" +
                    $"<span><font color='#00FF00'>{_core.Translation.GetPlayerLocalizer(player)["NextDay"]}</font></span>";
                    player.SendMessage(MessageType.CenterHTML, $"{Message}");
                }

                _globals.InProtect[player.PlayerID] = true;

                _globals.BeAZombie[player.PlayerID] = currentDay.BeforeZombie;
                _globals.DeathTime[player.PlayerID] = 0;
            }
            else
            {
                _helpers.SetFreezeState(player, true);
                _helpers.TeleportZombie(player);
            }

        }
        _globals.g_DeathCheck?.Cancel();
        _globals.g_DeathCheck = null;

        if (CFG.SoundHumanWins)
        {
            _helpers.EmitSoundToAll(CFG.SoundEventHumanWins);
        }


        if (_globals.RiotDay == maxDay)
        {
            _core.Scheduler.DelayBySeconds(5.0f, () => { _helpers.ChangeMap(); });
        }
        else
        {
            _helpers.SetTeamScore(Team.CT);
            _helpers.TerminateRound(RoundEndReason.CTsWin, 8.0f);
        }
    }
    public void Faketswin()
    {
        var CFG = _mainConfig.CurrentValue;
        var Dayconfig = _dayConfig.GetConfig();
        _globals.GameStart = false;
        var currentDay = _helpers.GetCurrentDay(_globals.RiotDay);
        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (!player.IsFakeClient) // 人类玩家
            {
                player.SendMessage(MessageType.CenterHTML, $"{_core.Translation.GetPlayerLocalizer(player)["ZombieWins"]}");
                _globals.BeAZombie[player.PlayerID] = currentDay.BeforeZombie;
                _globals.DeathTime[player.PlayerID] = 0;
            }
        }

        if (CFG.SoundZombieWins)
        {
            _helpers.EmitSoundToAll(CFG.SoundEventZombieWins);
        }

        _globals.g_DeathCheck?.Cancel();
        _globals.g_DeathCheck = null;

        _helpers.SetTeamScore(Team.T);
        _helpers.TerminateRound(RoundEndReason.TerroristsWin, 8.0f);

    }

    public void JoinTeamCheck(IPlayer player)
    {
        
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid)
            return;

        var HumanCount = _core.PlayerManager.GetAllPlayers()
            .Where(humans =>
                humans.PlayerPawn is { IsValid: true, TeamNum: 3 } &&
                humans.Controller is { IsValid: true } &&
                humans.Controller.PawnIsAlive)
            .Count();


        if (!_globals.GameStart)
        {
            if (!Controller.PawnIsAlive)
            {
                Controller.Respawn();
            }
        }
        else
        {
            if (HumanCount > 0)
            {
                if (!Controller.PawnIsAlive)
                {
                    Controller.Respawn();
                }
            }
            else
            {
                Faketswin();
            }
        }

    }


    public void CheckHumanAlive()
    {
        var HumanCount = _core.PlayerManager.GetAllPlayers()
            .Where(humans =>
                humans.PlayerPawn is { IsValid: true, TeamNum: 3 } &&
                humans.Controller is { IsValid: true } &&
                humans.Controller.PawnIsAlive)
            .Count();

        if (HumanCount <= 0 && _globals.GameStart)
        {
            Faketswin();
            _globals.GameStart = false;
        }
    }

}