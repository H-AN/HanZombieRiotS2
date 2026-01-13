<div align="center"><h1><img width="600" height="131" alt="68747470733a2f2f70616e2e73616d7979632e6465762f732f56596d4d5845" src="https://github.com/user-attachments/assets/d0316faa-c2d0-478f-a642-1e3c3651f1d4" /></h1></div>

<div class="section">
<div align="center"><h1>ZombieRiot for Swiftly2</h1></div>


<div align="center"><strong>基于 Swiftly2 框架开发的 CS2 僵尸暴动（Zombie Riot）游戏模式插件。</p></div>

<div align="center"><strong>支持 PVPVE 玩法：人类 VS 丧尸（Bot 或玩家）。</p></div>
<div align="center"><strong>高性能、配置灵活、易扩展。</p></div>
</div>

<div align="center">

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Z8Z31PY52N)
  

</div>
---
使用此插件 最好使用 +game_type 0 +game_mode 0 启动服务器 

并在 game\csgo\cfg\gamemode_casual.cfg 

设置 mp_warmuptime 0 mp_freezetime 0 

设置 mp_t_default_secondary "" 不给丧尸队伍任何枪械让地上不会掉落更多格洛克

在 game\csgo\cfg\server.cfg 中  

写入 bot_join_team T 和 mp_limitteams 0

---

📦 创意工坊示例（Zombie 模型/音效等）


插件可结合以下创意工坊资源使用（示例）：
3474477701  3450081072 3603675956 
```
要使用创意工坊资源,需要服务器安装metamod插件 multiaddonmanager 来管理服务器和玩家使用下载和安装创意工坊资源

安装multiaddonmanager插件后 在game\csgo\cfg\multiaddonmanager\multiaddonmanager.cfg配置文件中
 
找到第一行 mm_extra_addons  "3474477701,3450081072,3603675956"

把资源ID填写上去 等待服务器下载资源完毕 玩家进服会自动下载资源

之后用 Source2Viewer 软件 打开资源包 查看资源内的 模型路径与soundevent名字

之后根据需要填写到僵尸暴动插件内使用
```
---

🧩 插件功能特色

支持 多关卡 PVE 玩法

支持 高难度模式（污染浓度 100% 后启用）

可自定义每张地图独立配置

多种丧尸类型与属性可自由扩展

游戏结束自动切换至下一张地图（支持官图 / 工坊图）

HUD 与伤害显示可供玩家切换

完整管理员控制指令

---

🛠 管理员指令（Admin Commands）

指令说明

!zriot_next	直接跳到下一关

!zriot_setday 5	跳到指定关卡（数字必填）

!zriot_diff	将污染浓度提升到 100%，下张地图切换至高难度配置

!zriot_human 玩家名	将某玩家设置为人类队伍（需填写完整玩家名）

!zriot_zombie 玩家名	将某玩家设置为丧尸队伍（需完整玩家名）


---


🎮 玩家指令（Player Commands）

指令说明

!zriot_hud	开启/关闭中心 HUD 显示

!zriot_dmg	开启/关闭中心伤害显示

---

⚙️ 配置文件说明

---

📁 主配置（必须）

---

文件说明

HanZriotCFG.jsonc	插件的主要配置文件

HanZriotDayConfig.jsonc	关卡配置（普通难度）

ZombieDataConfig.jsonc	丧尸属性配置（普通难度）

🔥 高难度配置（污染浓度 100% 后使用）

---

文件说明

HanZriotDayConfig_hight.jsonc	高难度关卡配置

ZombieDataConfig_hight.jsonc	高难度丧尸属性配置

---

🗺 地图配置（可选）

若要为某张地图使用独立配置：

新建一个 与地图同名的文件夹

把关卡与丧尸属性配置放入

（示例：de_dust2/HanZriotDayConfig.jsonc）

插件会自动检测是否存在专属地图配置并优先读取。

---

🔄 地图轮换配置（mapsconfig）

mapsconfig 文件夹包含：

---

文件说明

MapConfig.jsonc	仅官方地图

RandomMapConfig.jsonc	官图 + 工坊地图混合

WorkShopMapConfig.jsonc	仅工坊地图 ID

主配置中的 useworkshopmap 决定启用哪个：

---

值/地图轮换方式

0	仅使用官方地图（MapConfig.jsonc）

1	混合官图 + 工坊图（RandomMapConfig.jsonc）

2	仅使用工坊图（WorkShopMapConfig.jsonc）

通关最后一关后会根据配置自动随机切换地图。

🧱 关卡配置示例（节选）
```
"ZriotDayCFG": {
  "Days": [
    {
      "DayName": "第1天",
      "Count": 5,
      "HealthBoost": 0,
      "BeforeZombie": 0,
      "Storyline": "故事线1",
      "ZombieOverride": "Zombienormal1,Zombienormal2"
    },
    {
      "DayName": "第2天",
      "Count": 10,
      "HealthBoost": 0,
      "BeforeZombie": 0,
      "Storyline": "故事线1",
      "ZombieOverride": "Zombienormal1,Zombienormal2"
    }
  ]
}
```

你可以自由添加关卡、设置丧尸种类、血量增强、死亡次数尸变、故事线等内容。

🧟 丧尸属性配置示例
```
"ZriotZombieCFG": {
  "ZombieList": [
    {
      "Name": "ZombieLight",
      "Model": "characters/models/hoshistar/zombiezeta/mutation_light/mutation_light.vmdl",
      "Health": 150,
      "Speed": 1.0,
      "Damage": 1.0,
      "Gravity": 1.0,
      "HealthRevive": 1,
      "HealthReviveSec": 1.0,
      "HealthReviveHp": 1,
      "Percent": 10,
      "ZombieScale": 1.0
    },
    {
      "Name": "ZombieHeavy",
      "Model": "characters/models/hoshistar/zombiezeta/mutation_heavy/mutation_heavy.vmdl",
      "Health": 450,
      "Speed": 1.0,
      "Damage": 1.0,
      "Gravity": 1.0,
      "HealthRevive": 0,
      "HealthReviveSec": 1.0,
      "HealthReviveHp": 1,
      "Percent": 10,
      "ZombieScale": 1.0
    }
  ]
}
```

注：ZombieScale 目前有 bug，此功能暂时无效。


---



<div align="center"><strong>A CS2 Zombie Riot game mode plugin built on the Swiftly2 framework.</p></div>

<div align="center"><strong>Supports PVPVE gameplay: Humans VS Zombies (bots or players).</p></div>

<div align="center">High performance, fully configurable, and highly extensible.</p></div>

---

---
It’s best to start the server with +game_type 0 +game_mode 0 and 

set the following in game\csgo\cfg\gamemode_casual.cfg:

mp_warmuptime 0  and mp_freezetime 0 

mp_t_default_secondary ""  Do not give the zombie team any firearms so that no extra Glocks will drop on the ground.

set the following in game\csgo\cfg\server.cfg  

bot_join_team T and mp_limitteams 0

---

📦 Workshop Examples (Zombie models / sounds)

You may use the plugin with the following workshop resources:
3474477701  3450081072 3603675956 
```
To use Workshop resources, your server must install the Metamod plugin: multiaddonmanager

which handles downloading and installing Workshop addons for both the server and players.
 
After installing the multiaddonmanager plugin, open the configuration file:

game\csgo\cfg\multiaddonmanager\multiaddonmanager.cfg

Locate the first line: mm_extra_addons  "3474477701,3450081072,3603675956"

Add the Workshop IDs you want to use, then wait for the server to finish downloading the addons.

When players join the server, the required Workshop content will be downloaded automatically.

Afterwards, use Source2Viewer to open the downloaded workshop package to find the model paths and soundevent names.

Finally, fill in the required paths and soundevent names in the Zombie Riot plugin configuration as needed.
```

---

🧩 Features

Multi-stage PVE survival gameplay

High-difficulty mode triggered at 100% contamination

Per-map custom configurations

Fully customizable zombie types and stats

Automatic map rotation (official / workshop supported)

HUD & damage indicators toggleable by players

Full admin control command set

---

🛠 Admin Commands

Command	Description

!zriot_next	Skip to the next stage

!zriot_setday 5	Jump to a specific stage (number required)

!zriot_diff	Set contamination to 100%; next map uses high-difficulty configs

!zriot_human playername	Move a player to the human team (exact name required)

!zriot_zombie playername	Move a player to the zombie team (exact name required)

---

🎮 Player Commands

Command	Description

!zriot_hud	Enable/disable center HUD

!zriot_dmg	Enable/disable damage HUD

---

⚙️ Configuration Files

Main configs

File	Description

HanZriotCFG.jsonc	Main plugin settings

HanZriotDayConfig.jsonc	Stage configuration (normal difficulty)

ZombieDataConfig.jsonc	Zombie attribute configuration (normal difficulty)

High-difficulty configs (activated at 100% contamination)

---

File	Description

HanZriotDayConfig_hight.jsonc	High-difficulty stage config

ZombieDataConfig_hight.jsonc	High-difficulty zombie config

---

🗺 Per-map Configuration

To create a custom configuration for a specific map:

Create a folder with the same name as the map

Put the stage and zombie configs inside it

The plugin will automatically detect and load them.

---

🔄 Map Rotation (mapsconfig)

The mapsconfig folder includes:

---

File	Description

MapConfig.jsonc	Official maps only

RandomMapConfig.jsonc	Official + workshop mixed

WorkShopMapConfig.jsonc	Workshop IDs only

Controlled by useworkshopmap:

Value	Rotation Mode

0	Official maps only

1	Mixed official + workshop

2	Workshop maps only

After finishing the final stage, the plugin randomly selects the next map based on these configs.

🧱 Stage Configuration Example
```
"ZriotDayCFG": {
  "Days": [
    {
      "DayName": "第1天",
      "Count": 5,
      "HealthBoost": 0,
      "BeforeZombie": 0,
      "Storyline": "故事线1",
      "ZombieOverride": "Zombienormal1,Zombienormal2"
    },
    {
      "DayName": "第2天",
      "Count": 10,
      "HealthBoost": 0,
      "BeforeZombie": 0,
      "Storyline": "故事线1",
      "ZombieOverride": "Zombienormal1,Zombienormal2"
    }
  ]
}
```
🧟 Zombie Configuration Example
```
"ZriotZombieCFG": {
  "ZombieList": [
    {
      "Name": "ZombieLight",
      "Model": "characters/models/hoshistar/zombiezeta/mutation_light/mutation_light.vmdl",
      "Health": 150,
      "Speed": 1.0,
      "Damage": 1.0,
      "Gravity": 1.0,
      "HealthRevive": 1,
      "HealthReviveSec": 1.0,
      "HealthReviveHp": 1,
      "Percent": 10,
      "ZombieScale": 1.0
    },
    {
      "Name": "ZombieHeavy",
      "Model": "characters/models/hoshistar/zombiezeta/mutation_heavy/mutation_heavy.vmdl",
      "Health": 450,
      "Speed": 1.0,
      "Damage": 1.0,
      "Gravity": 1.0,
      "HealthRevive": 0,
      "HealthReviveSec": 1.0,
      "HealthReviveHp": 1,
      "Percent": 10,
      "ZombieScale": 1.0
    }
  ]
}
```

Note: ZombieScale currently has issues This function is temporarily unavailable.

---
<div align="center"><h1>V3.1版本 API 使用/API version 3.1 usage</h1></div>

要编译 v3.1版本插件需要同时下载api源码,文件夹与主插件平级

To compile the v3.1 version plugin, you also need to download the API source code; the folder should be at the same level as the main plugin.

```
├── HanZombieRiotS2    → 插件主体文件夹/Plugin main folder
├── IHanZriotAPI          → API文件夹/API folder
```
编译 主体插件,即可获得 主体插件文件与API文件

Compiling the main plugin will yield the main plugin files and API files.

---
<div align="center"><h1>其他插件使用API方法/Other plugins use API methods</h1></div>

```
1. 在头文件内添加 using HanZombieRiotS2; / Add `using HanZombieRiotS2;` to the header file.

2. 在需要使用API的插件csproj文件内添加 / Add the following to the csproj file of the plugin that requires the API:

<ItemGroup>
		<Reference Include="HanZombieRiotS2">
		<HintPath>HanZriotAPI.dll</HintPath>
		<Private>false</Private>
	</Reference>
</ItemGroup>

3. 将 HanZriotAPI.dll 放置在 需要API的插件文件夹内

yourplugins/
├── HanZriotAPI.dll              # API dll文件 /API dll file
├── yourplugins.cs          # 你的插件 / yourplugins cs
└── yourplugins.csproj   # 你的插件项目 /yourplugins csproj

4.其他插件内获取API / Get API within other plugins

public partial class yourplugins(ISwiftlyCore core) : BasePlugin(core)
{
    private IHanZriotAPI? _gameApi; //先声明api / First declare the API
    
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
      if (interfaceManager.HasSharedInterface("zriot"))  //获取api / Get API
      {
          _gameApi = interfaceManager.GetSharedInterface<IHanZriotAPI>("zriot");
          Core.Logger.LogInformation("成功加载僵尸暴动API/Successfully loaded the Zombie Riot API");
      }
      else
      {
          Core.Logger.LogInformation("加载僵尸暴动API加载失败/Zombie Riot API loading failed");
      }
    }
}

5. 使用API功能(示例) / Using API features (example)

Core.Command.RegisterCommand("sw_zombie", zombie, true);

public void zombie(ICommandContext context)
{
    var player = context.Sender;
    if (player == null || !player.IsValid)
        return;
        
    _gameApi.ZRiot_Zombie(player); //设置某人为丧尸 /Set someone as a zombie
}

6. 运行插件需要放入api dll文件

yourplugins/
├── yourplugins.dll              # 插件dll / plugins dll
├── yourplugins.deps.json          # 插件 deps.json  / plugins  deps.json 
├── yourplugins.pdb          # 插件 pdb  /  plugins pdb 
└── resources/
    ├── exports/                # API放置文件夹 / API folder
    └────── HanZriotAPI.dll          # API dll 文件 / API dll file
		
```
API功能 / API features
```
/// <summary>
/// Get GameStart.
/// 获取游戏开始状态.
/// </summary>
bool GameStart { get; }

/// <summary>
/// Get CurrentDay.
/// 获取当前天数.
/// </summary>
int CurrentDay { get; }

/// <summary>
/// Get NeedKillZombie.
/// 获取当前需要击杀多少只丧尸.
/// </summary>
int NeedKillZombie { get; }

/// <summary>
/// Get ZombieKill.
/// 获取当前所有玩家已经击杀了多少只丧尸.
/// </summary>
int ZombieKill { get; }

/// <summary>
/// Get ZombiesLeft.
/// 获取还剩余多少只丧尸通关.
/// </summary>
int ZombiesLeft { get; }

/// <summary>
/// Get HumansAlive.
/// 获取当前存活人类数量.
/// </summary>
int HumansAlive { get; }

/// <summary>
/// Get MaxDay.
/// 获取当前地图最大关卡天数.
/// </summary>
int MaxDay { get; }

/// <summary>
/// change player to human.
/// 指定某个玩家成为人类.
/// </summary>
void ZRiot_Human(IPlayer player);

/// <summary>
/// change player to human.
/// 指定某个玩家成为丧尸.
/// </summary>
void ZRiot_Zombie(IPlayer player);
```



