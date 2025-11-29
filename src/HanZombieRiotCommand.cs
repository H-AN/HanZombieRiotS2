using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace HanZombieRiotS2;
public class HanZriotCommands
{
    private readonly ILogger<HanZriotCommands> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanZriotCFG> _mainConfig;
    private readonly IStageConfigProvider _dayConfig;
    private readonly IZombieConfigProvider _zombieConfig;
    private readonly HanZriotHelpers _helpers;
    private readonly HanZriotGlobals _globals;
    private readonly HanZriotService _services;


    public HanZriotCommands(ISwiftlyCore core, ILogger<HanZriotCommands> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig,
        HanZriotHelpers helpers, HanZriotGlobals globals,
        HanZriotService Service)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
        _helpers = helpers;
        _globals = globals;
        _services = Service;
    }

    public void UpDateServerCommand()
    {
        _core.Engine.ExecuteCommand("bot_quota_mode fill");
        _core.Engine.ExecuteCommand("bot_quota 20");
        _core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 1");
        _core.Engine.ExecuteCommand("mp_warmup_end 1");
        _core.Engine.ExecuteCommand("bot_join_after_player 1");
        //_core.Engine.ExecuteCommand("bot_join_team T");
        _core.Engine.ExecuteCommand("sv_human_autojoin_team 1");
        _core.Engine.ExecuteCommand("mp_humanteam CT");
        _core.Engine.ExecuteCommand("bot_chatter off");
        _core.Engine.ExecuteCommand("bot_knives_only");
        _core.Engine.ExecuteCommand("mp_autokick 0");
        _core.Engine.ExecuteCommand("mp_round_restart_delay 0");
        _core.Engine.ExecuteCommand("mp_autoteambalance 0");
        //_core.Engine.ExecuteCommand("bot_stop 0");
    }

    public void Command()
    {
        _core.Command.RegisterCommand("jointeam", RegisterJoin, true);
        _core.Command.HookClientCommand(OnJoinTeam);

        var Cfg = _mainConfig.CurrentValue;
        string Permission = Cfg.AdminCommandPermission;
        _core.Command.RegisterCommand("sw_zriot_next", NEXTDAY, true, Permission);
        _core.Command.RegisterCommand("sw_zriot_setday", SETDAY, true, Permission);
        _core.Command.RegisterCommand("sw_zriot_diff", changediff, true, Permission);

        _core.Command.RegisterCommand("sw_zriot_human", ChangeToHuman, true, Permission);
        _core.Command.RegisterCommand("sw_zriot_zombie", ChangeToZombie, true, Permission);

        _core.Command.RegisterCommand("sw_zriot_hud", ShowOrCloseHud, true);
        _core.Command.RegisterCommand("sw_zriot_dmg", ShowOrCloseDmghud, true);
    }

    public void RegisterJoin(ICommandContext context){
    }


    public HookResult OnJoinTeam(int playerId, string commandLine)
    {
        IPlayer? player = _core.PlayerManager.GetPlayer(playerId);

        if (player.IsValid && !player.IsFakeClient)
        {
            if (commandLine.StartsWith("jointeam 2"))
            {
                player.SendMessage(MessageType.Chat, $"[华仔] 无法加入僵尸队伍,只能加入CT队伍");
                return HookResult.Stop;
            }
            else if (commandLine.StartsWith("jointeam 3"))
            {
                player.SwitchTeam(Team.CT);
                _core.Scheduler.DelayBySeconds(1.0f, () =>
                {
                    _services.JoinTeamCheck(player);
                });
                
                
            }
            else if (commandLine.StartsWith("jointeam 1"))
            {
                player.SendMessage(MessageType.Chat, $"[华仔] 无法加入观察者,只能加入CT队伍");
                return HookResult.Stop;
            }

        }

        
        return HookResult.Continue;
    }

    

    public void NEXTDAY(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) 
            return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) 
            return;
        int maxDay = _dayConfig.GetConfig().Days.Count;

        if (_globals.RiotDay < maxDay)
        {
            _globals.RiotDay++;
        }
        else
        {
            _globals.RiotDay = maxDay;
        }


        _helpers.TerminateRound(RoundEndReason.RoundDraw, 8.0f);

        _core.PlayerManager.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminNextDay", Controller.PlayerName]}");
    }

    public void SETDAY(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) 
            return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) 
            return;

        int maxDay = _dayConfig.GetConfig().Days.Count;
        if (context.Args.Length < 1)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSetDayError1"]}"); 
            return;
        }

        if (!int.TryParse(context.Args[0], out int count) || count <= 0 || count > maxDay)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSetDayError2"]}"); 
            return;
        }
            

        _globals.RiotDay = count;
        _helpers.TerminateRound(RoundEndReason.RoundDraw, 8.0f);

        _core.PlayerManager.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSetDay", Controller.PlayerName, count]}");
    }

    public void changediff(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) return;

        _globals.KillCount = 1000;
        _globals.KillPercent = 100f;
        _core.PlayerManager.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSetDiff", Controller.PlayerName]}");
    }

    public void ShowOrCloseHud(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) return;

        if(_globals.PlayerHud[player.PlayerID])
        {
            _globals.PlayerHud[player.PlayerID] = false;
        }
        else
        {
            _globals.PlayerHud[player.PlayerID] = true;
            player.SendMessage(MessageType.CenterHTML, "");
        }
        

    }
    public void ShowOrCloseDmghud(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) return;

        if (_globals.PlayerDmgHud[player.PlayerID])
        {
            _globals.PlayerDmgHud[player.PlayerID] = false;
        }
        else
        {
            _globals.PlayerDmgHud[player.PlayerID] = true;
        }
        

    }

    public void ChangeToHuman(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) return;

        if (context.Args.Length < 0)
            return;
        

        string targetName = context.Args[0];

        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var target in allPlayers)
        {
            if (target.IsValid)
            {
                var targetpawn = target.PlayerPawn;
                if (targetpawn != null && targetpawn.IsValid)
                {
                    if (targetpawn.TeamNum == 2)
                    {
                        if (!target.IsFakeClient)
                        {
                            var targetController = target.Controller;
                            if (targetController != null && targetController.IsValid && targetController.PlayerName == targetName)
                            {
                                _globals.BeAZombie[target.PlayerID] = 0;
                                target.ChangeTeam(Team.CT);
                                target.SendMessage(MessageType.Chat, $"{_globals.Countdown} {_core.Translation.GetPlayerLocalizer(player)["ForceJoinHuman"]}");
                            }
                        }
                    }
                }
            }

        }

    }

    public void ChangeToZombie(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid) return;

        if (context.Args.Length < 0)
            return;
        

        string targetName = context.Args[0];

        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var target in allPlayers)
        {
            if (target.IsValid)
            {
                var targetpawn = target.PlayerPawn;
                if (targetpawn != null && targetpawn.IsValid)
                {
                    if (targetpawn.TeamNum == 3)
                    {
                        if (!target.IsFakeClient)
                        {
                            var targetController = target.Controller;
                            if (targetController != null && targetController.IsValid && targetController.PlayerName == targetName)
                            {
                                target.ChangeTeam(Team.T);
                                target.SendMessage(MessageType.Chat, $"{_globals.Countdown} {_core.Translation.GetPlayerLocalizer(player)["ForceJoinZombie"]}");
                            }
                        }
                    }
                }
            }

        }

    }



}