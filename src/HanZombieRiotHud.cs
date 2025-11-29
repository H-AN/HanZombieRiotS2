using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace HanZombieRiotS2;
public class HanZriotHud
{
    private readonly ILogger<HanZriotHud> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanZriotCFG> _mainConfig;
    private readonly IStageConfigProvider _dayConfig;
    private readonly IZombieConfigProvider _zombieConfig;
    private readonly HanZriotGlobals _globals;


    public HanZriotHud(ISwiftlyCore core, ILogger<HanZriotHud> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig,
        HanZriotGlobals globals)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
        _globals = globals;
    }

    public void Show(IPlayer player) //僵尸暴动信息显示
    {
        if (player == null || !player.IsValid)
            return;

        if (_globals.PlayerHud[player.PlayerID])
            return;

        if (player.IsFakeClient)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid)
            return;

        /*
        var HumanCount = _core.PlayerManager.GetAllPlayers()
            .Where(humans =>
                humans is { IsValid: true} &&
                humans.PlayerPawn is { IsValid: true, TeamNum: 3 } &&
                humans.Controller is { IsValid: true } &&
                humans.Controller.PawnIsAlive)
            .Count();
        */
        var HumanCount = _core.PlayerManager.GetAllPlayers()
        .Where(p =>
            p is { IsValid: true } &&
            p.Controller is { IsValid: true, TeamNum: 3 } controller &&
            controller.PlayerPawn is { IsValid: true} pawn &&
            controller.PawnIsAlive
        )
        .Count();

        var AliveZombie = _core.PlayerManager.GetAllPlayers()
            .Where(Zombie =>
                Zombie.PlayerPawn is { IsValid: true, TeamNum: 2 } &&
                Zombie.Controller is { IsValid: true } &&
                Zombie.Controller.PawnIsAlive)
            .Count();


        int LeftZombie = _globals.NeedKillZombie - _globals.ZombieKill;

        var Dayconfig = _dayConfig.GetConfig(); //CurrentValue;

        int maxDay = Dayconfig.Days.Count;

        var currentDay = HudGetCurrentDay(_globals.RiotDay);

        string DiffMessage;
        string WDiffMessage;

        if (!_globals.CurrentMapIsHighDiff)
        {
            if (_globals.KillPercent <= 100f)
            {
                DiffMessage = $"<span><font color='#E22D2D'>[{_core.Translation.GetPlayerLocalizer(player)["PollutionCount"]}:{_globals.KillPercent}％]</font></span><br>";
                WDiffMessage = $"[{_core.Translation.GetPlayerLocalizer(player)["PollutionCount"]}:{_globals.KillPercent}％]";
            }
            else
            {
                DiffMessage = $"<span><font color='#E22D2D'>[{_core.Translation.GetPlayerLocalizer(player)["HardMode"]}]</font></span><br>";
                WDiffMessage = $"[{_core.Translation.GetPlayerLocalizer(player)["HardMode"]}]";
            }
        }
        else
        {
            DiffMessage = $"<span><font color='#E22D2D'>[{_core.Translation.GetPlayerLocalizer(player)["ZombiePowerful"]}]</font></span><br>";
            WDiffMessage = $"[{_core.Translation.GetPlayerLocalizer(player)["ZombiePowerful"]}]";
        }
       
        string Message = $"<span><font color='#E22D2D'>{currentDay.DayName}</font></span><br>" +
            $"<span><font color='#FFFFE0'>{_core.Translation.GetPlayerLocalizer(player)["Stage"]}:[{_core.Translation.GetPlayerLocalizer(player)["Progress"]}</font><font color='#87CEEB'>{_globals.RiotDay}</font><font color='#FFFFE0'>/</font><font color='#87CEEB'>{maxDay}</font><font color='#FFFFE0'>{_core.Translation.GetPlayerLocalizer(player)["Days"]}]</font></span><br>" +
            $"<span><font color='#FFFFE0'>{_core.Translation.GetPlayerLocalizer(player)["ZombiesLeft"]}:</font> <font color='#E22D2D'>{LeftZombie}</font> <font color='#FFFFE0'>{_core.Translation.GetPlayerLocalizer(player)["ZCount"]}</font></span><br>" +
            $"<span><font color='#FFFFE0'>{_core.Translation.GetPlayerLocalizer(player)["HumanLeft"]}:</font> <font color='#00FF00'>{HumanCount}</font> <font color='#FFFFE0'>{_core.Translation.GetPlayerLocalizer(player)["HCount"]}</font></span><br>" +
            $"{DiffMessage}" +
            $"<span><font color='#FFFFE0'>{currentDay.Storyline}</font></span>";

        if (Controller.PawnIsAlive && pawn.TeamNum == 3) 
        {
            
            if (_globals.GameStart == true)
            {
                player.SendMessage(MessageType.CenterHTML, $"{Message}");
            }
            
        }
        
    }

    private HanZriotDayConfig.Day HudGetCurrentDay(int riotDay)
    {
        var config = _dayConfig.GetConfig(); //CurrentValue;

        if (config.Days == null || config.Days.Count == 0)
            throw new InvalidOperationException("[HUD] 配置未加载或 Day 列表为空");

        if (riotDay < 1 || riotDay > config.Days.Count)
            riotDay = 1;

        return config.Days[riotDay - 1];
    }


}