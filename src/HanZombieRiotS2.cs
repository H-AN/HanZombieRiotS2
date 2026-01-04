
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Plugins;


namespace HanZombieRiotS2;

[PluginMetadata(
    Id = "HanZombieRiotS2",
    Version = "3.0.0",
    Name = "僵尸暴动 for Sw2/HanZombieRiotS2",
    Author = "H-AN",
    Description = "CS2僵尸暴动 SW2版本 CS2 zombieriot for SW2.")]
public partial class HanZombieRiotS2(ISwiftlyCore core) : BasePlugin(core)
{

    private ServiceProvider? ServiceProvider { get; set; }
    private HanZriotCFG _ZriotCFG = null!;
    private IStageConfigProvider _ZriotDayCFG = null!;
    private IZombieConfigProvider _ZriotZombieCFG = null!;

    private HanZriotEvents _Events = null!;
    private HanZriotHelpers _Helpers = null!;
    private HanZriotGlobals _Globals = null!;
    private HanZriotCommands _Commands = null!;


    public override void Load(bool hotReload)
    {

        Core.Configuration.InitializeJsonWithModel<HanZriotCFG>("HanZriotCFG.jsonc", "ZriotCFG").Configure(builder =>
        {
            builder.AddJsonFile("HanZriotCFG.jsonc", false, true);
        });


        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HanZriotCFG>()
            .BindConfiguration("ZriotCFG");

        collection.AddSingleton<IStageConfigProvider, StageConfigProvider>();
        collection.AddSingleton<IZombieConfigProvider, ZombieConfigProvider>();

        collection.AddSingleton<HanZriotGlobals>();
        collection.AddSingleton<HanZriotEvents>();
        collection.AddSingleton<HanZriotHelpers>();
        collection.AddSingleton<HanZriotHud>();
        collection.AddSingleton<HanZriotCommands>();
        collection.AddSingleton<HanZriotService>();

        ServiceProvider = collection.BuildServiceProvider();

        _ZriotDayCFG = ServiceProvider.GetRequiredService<IStageConfigProvider>();
        _ZriotZombieCFG = ServiceProvider.GetRequiredService<IZombieConfigProvider>();

        _Globals = ServiceProvider.GetRequiredService<HanZriotGlobals>();
        _Events = ServiceProvider.GetRequiredService<HanZriotEvents>();
        _Helpers = ServiceProvider.GetRequiredService<HanZriotHelpers>();
        _Commands = ServiceProvider.GetRequiredService<HanZriotCommands>();

        var ZriotCFGMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanZriotCFG>>();

        _ZriotCFG = ZriotCFGMonitor.CurrentValue;


        ZriotCFGMonitor.OnChange(newConfig =>
        {
            _ZriotCFG = newConfig;
            Core.Logger.LogInformation($"{Core.Localizer["ServerCfgChange"]}");
        });


        Core.Event.OnMapLoad += Event_OnMapLoad;
        _Commands.Command();
        _Events.HookEvents();
    }

    public override void Unload()
    {
        ServiceProvider!.Dispose();
    }

    private void Event_OnMapLoad(IOnMapLoadEvent @event)
    {
        _Globals.CurrentMapIsHighDiff = _Globals.HightDiff;

        string difficulty = _Globals.CurrentMapIsHighDiff ? "hight" : "";
        string mapname = @event.MapName;

        _ZriotDayCFG.Reload(difficulty, mapname);
        _ZriotZombieCFG.Reload(difficulty, mapname);

        if (_Globals.CurrentMapIsHighDiff)
        {
            _Globals.KillCount = 0;
            _Globals.KillPercent = 0;
            _Globals.HightDiff = false;
        }

        var Daycfg = _ZriotDayCFG.GetConfig();
        var zombiecfg = _ZriotZombieCFG.GetConfig();

        var ZombieList = zombiecfg.ZombieList;

        if (ZombieList == null || ZombieList.Count == 0)
        {
            Core.Logger.LogError($"{Core.Localizer["NoZombieData"]}");
            return;
        }

        _Commands.UpDateServerCommand();

        if (Daycfg.Days == null || Daycfg.Days.Count == 0)
        {
            Core.Logger.LogError($"{Core.Localizer["NoDayData"]}");
            return;
        }

        _Globals.RiotDay = Math.Clamp(_Globals.RiotDay, 1, Daycfg.Days.Count);
        var currentDay = _Helpers.GetCurrentDay(_Globals.RiotDay);
        if (currentDay.BeforeZombie > 0)
        {
            _Globals.AllowHumanZombie = true;
        }
        else
        {
            _Globals.AllowHumanZombie = false;
        }

    }

}

