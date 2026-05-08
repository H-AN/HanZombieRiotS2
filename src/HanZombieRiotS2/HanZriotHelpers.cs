using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using static HanZombieRiotS2.ZombieDataConfig;

namespace HanZombieRiotS2;

public class HanZriotHelpers
{
    private readonly ILogger<HanZriotHelpers> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanZriotCFG> _mainConfig;
    private readonly HanZriotGlobals _globals;
    private readonly HanZriotHud _hud;
    private readonly IStageConfigProvider _dayConfig;
    private readonly IZombieConfigProvider _zombieConfig;

    public HanZriotHelpers(ISwiftlyCore core, ILogger<HanZriotHelpers> logger,
        IOptionsMonitor<HanZriotCFG> mainConfig,
        HanZriotGlobals globals, HanZriotHud hud,
        IStageConfigProvider dayConfig,
        IZombieConfigProvider zombieConfig)
    {
        _core = core;
        _logger = logger;
        _mainConfig = mainConfig;
        _globals = globals;
        _hud = hud;
        _dayConfig = dayConfig;
        _zombieConfig = zombieConfig;
    }

    public int AdvanceRoundGeneration()
    {
        _globals.RoundGeneration++;
        return _globals.RoundGeneration;
    }

    public int GetCurrentRoundGeneration()
    {
        return _globals.RoundGeneration;
    }

    public bool IsRoundGenerationCurrent(int expectedRoundGeneration)
    {
        return expectedRoundGeneration == _globals.RoundGeneration;
    }

    public bool TryResolveCurrentPlayer(int playerId, ulong expectedSessionId, int expectedRoundGeneration, out IPlayer player, bool requireAlive = false)
    {
        player = null!;

        if (!IsRoundGenerationCurrent(expectedRoundGeneration))
            return false;

        var currentPlayer = _core.PlayerManager.GetPlayer(playerId);
        if (currentPlayer == null || !currentPlayer.IsValid || !_core.PlayerManager.IsPlayerOnline(playerId))
            return false;

        if (expectedSessionId != 0 && currentPlayer.SessionId != expectedSessionId)
            return false;

        if (requireAlive)
        {
            var controller = currentPlayer.Controller;
            if (controller == null || !controller.IsValid || controller.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                return false;
        }

        player = currentPlayer;
        return true;
    }

    public bool TryResolveCurrentPlayerPawn(int playerId, ulong expectedSessionId, int expectedRoundGeneration, out IPlayer player, out CCSPlayerPawn pawn, bool requireAlive = false)
    {
        player = null!;
        pawn = null!;

        if (!TryResolveCurrentPlayer(playerId, expectedSessionId, expectedRoundGeneration, out player, requireAlive))
            return false;

        var currentPawn = player.PlayerPawn;
        if (currentPawn == null || !currentPawn.IsValid)
            return false;

        if (requireAlive && currentPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return false;

        pawn = currentPawn;
        return true;
    }

    public void ApplyHumanDefaultModel(IPlayer player)
    {
        if (player is not { IsValid: true })
            return;

        string modelPath = _mainConfig.CurrentValue.HumandefaultModel;
        if (string.IsNullOrWhiteSpace(modelPath))
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        SetPlayerModelFixed(pawn, modelPath);
    }

    private bool TryGetPlayerIdentity(CCSPlayerPawn pawn, out int playerId, out ulong sessionId)
    {
        playerId = 0;
        sessionId = 0;

        if (pawn == null || !pawn.IsValid)
            return false;

        var controller = pawn.Controller.Value?.As<CCSPlayerController>();
        if (controller == null || !controller.IsValid)
            return false;

        var player = _core.PlayerManager.GetPlayer((int)(controller.Index - 1));
        if (player == null || !player.IsValid)
            return false;

        playerId = player.PlayerID;
        sessionId = player.SessionId;
        return true;
    }

    public void SetPlayerModelFixed(CCSPlayerPawn pawn, string modelPath)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        if (string.IsNullOrWhiteSpace(modelPath))
            return;

        if (!TryGetPlayerIdentity(pawn, out var playerId, out var sessionId))
            return;

        pawn.SetModel(modelPath);
        FixPlayerModelAnimations(playerId, sessionId, GetCurrentRoundGeneration(), pawn.AbsVelocity);
    }

    private void FixPlayerModelAnimations(int playerId, ulong sessionId, int expectedRoundGeneration, Vector originalVelocity)
    {
        if (!TryResolveCurrentPlayerPawn(playerId, sessionId, expectedRoundGeneration, out _, out var currentPawn, requireAlive: true))
            return;

        currentPawn.Teleport(null, null, new Vector(0, 0, 0));
        currentPawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
        currentPawn.ActualMoveType = MoveType_t.MOVETYPE_OBSOLETE;
        currentPawn.MoveTypeUpdated();

        _core.Scheduler.DelayBySeconds(0.02f, () =>
        {
            if (!TryResolveCurrentPlayerPawn(playerId, sessionId, expectedRoundGeneration, out _, out var resolvedPawn, requireAlive: true))
                return;

            resolvedPawn.MoveType = MoveType_t.MOVETYPE_WALK;
            resolvedPawn.ActualMoveType = MoveType_t.MOVETYPE_WALK;
            resolvedPawn.MoveTypeUpdated();

            resolvedPawn.Teleport(null, null, originalVelocity);
        });
    }


    public HanZriotDayConfig.Day GetCurrentDay(int RiotDay)
    {
        var config = _dayConfig.GetConfig();

        if (config.Days == null || config.Days.Count == 0)
            throw new InvalidOperationException($"{_core.Localizer["NoDayData"]}");

        if (RiotDay <= 0 || RiotDay > config.Days.Count)
        {
            RiotDay = 1;
        }

        return config.Days[RiotDay - 1];
    }

    public void ChangeBotTeam()
    {
        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (player != null && player.IsValid && player.IsFakeClient)
            {
                var Controller = player.Controller;
                if (Controller != null && Controller.IsValid)
                {
                    if (Controller.TeamNum == 3)
                    {
                        player.SwitchTeam(Team.T);
                        Controller.TeamNum = 2;
                        Controller.TeamNumUpdated();
                    }
                }
            }

        }

    }


    public void RespawnAllZombie()
    {
        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (player != null && player.IsValid)
            {
                var Controller = player.Controller;
                if (Controller != null && Controller.IsValid)
                {

                    if (Controller.TeamNum == 2)
                    {

                        if (!Controller.PawnIsAlive)
                        {

                            Controller.Respawn();
                        }
                    }
                }

            }
        }
    }


    public void PlayAmbSound(string sounds) //播放环境音乐
    {
        if (!string.IsNullOrWhiteSpace(sounds))
        {
            var toPlay = RandomSelectSound(sounds);
            if (toPlay != null)
            {
                EmitSoundToAll(toPlay);
            }
        }
    }

    public string? RandomSelectSound(string sound)
    {
        if (string.IsNullOrWhiteSpace(sound)) return null;

        var items = sound
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        if (items.Length == 0) return null;

        return items.Length == 1 ? items[0] : items[Random.Shared.Next(items.Length)];
    }

    public Zombie? SelectZombieForSpawn(List<Zombie> zombies)
    {
        if (zombies == null || zombies.Count == 0)
            return null;

        var weighted = zombies
            .Where(z => z != null)
            .Select(z => new { Zombie = z, Weight = Math.Max(0, z.Percent) })
            .ToList();

        int totalWeight = weighted.Sum(x => x.Weight);
        if (totalWeight <= 0)
        {
            return zombies[Random.Shared.Next(zombies.Count)];
        }

        int roll = Random.Shared.Next(totalWeight);
        int cursor = 0;

        foreach (var entry in weighted)
        {
            cursor += entry.Weight;
            if (roll < cursor)
            {
                return entry.Zombie;
            }
        }

        return weighted[^1].Zombie;
    }


    private bool TryGetSoundSourceEntityIndex(IPlayer player, out int sourceEntityIndex)
    {
        sourceEntityIndex = -1;

        if (player is not { IsValid: true })
            return false;

        var pawn = player.PlayerPawn;
        if (pawn is not { IsValid: true })
            return false;

        sourceEntityIndex = (int)pawn.Index;
        return true;
    }

    public void EmitSoundToEntity(IPlayer player, string SoundPath)
    {
        if (string.IsNullOrEmpty(SoundPath) || !TryGetSoundSourceEntityIndex(player, out int sourceEntityIndex))
            return;

        var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(SoundPath, 1.0f, 1.0f);
        sound.SourceEntityIndex = sourceEntityIndex;
        sound.Recipients.AddAllPlayers();
        _core.Scheduler.NextTick(() =>
        {
            sound.Emit();
        });
    }

    public void EmitSoundToAll(string SoundPath)
    {
        if (!string.IsNullOrEmpty(SoundPath))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(SoundPath, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddAllPlayers();
            _core.Scheduler.NextTick(() =>
            {
                sound.Emit();
            });
        }
    }

    public void GiveGrenade(IPlayer player, string weaponName)
    {
        if (player is not { IsValid: true })
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        var itemServices = pawn.ItemServices;
        if (itemServices == null || !itemServices.IsValid)
            return;

        itemServices.GiveItem<CCSWeaponBase>(weaponName);
    }

    public IEnumerable<IPlayer> GetPlayersInRadius(Vector center, float radius, byte teamNum)
    {
        float radiusSquared = radius * radius;

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player is not { IsValid: true })
                continue;

            var controller = player.Controller;
            if (controller == null || !controller.IsValid || controller.TeamNum != teamNum || !controller.PawnIsAlive)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var origin = pawn.AbsOrigin;
            if (origin == null)
                continue;

            if (GetDistanceSquared(center, origin.Value) <= radiusSquared)
            {
                yield return player;
            }
        }
    }

    public void ApplyDamage(IPlayer attacker, IPlayer target, float damageAmount, DamageTypes_t damageType = DamageTypes_t.DMG_BULLET)
    {
        if (damageAmount <= 0f)
            return;

        if (attacker is not { IsValid: true } || target is not { IsValid: true })
            return;

        var attackerPawn = attacker.PlayerPawn;
        var targetPawn = target.PlayerPawn;
        if (attackerPawn == null || !attackerPawn.IsValid || targetPawn == null || !targetPawn.IsValid)
            return;

        CBaseEntity inflictorEntity = attackerPawn;
        CBaseEntity attackerEntity = attackerPawn;
        CBaseEntity abilityEntity = attackerPawn;

        var damageInfo = new CTakeDamageInfo(inflictorEntity, attackerEntity, abilityEntity, damageAmount, damageType)
        {
            DamageForce = new Vector(0, 0, 10f)
        };

        var targetPos = targetPawn.AbsOrigin;
        if (targetPos != null)
        {
            damageInfo.DamagePosition = targetPos.Value;
        }

        target.TakeDamage(damageInfo);
    }

    public CParticleSystem? CreateParticleAtPos(CCSPlayerPawn pawn, Vector pos, string effectName)
    {
        if (pawn == null || !pawn.IsValid || string.IsNullOrWhiteSpace(effectName))
            return null;

        var particle = _core.EntitySystem.CreateEntityByDesignerName<CParticleSystem>("info_particle_system");
        if (particle == null || !particle.IsValid || !particle.IsValidEntity)
            return null;

        particle.StartActive = true;
        particle.EffectName = effectName;
        particle.AcceptInput("Start", "");
        particle.DispatchSpawn();
        particle.Teleport(pos, QAngle.Zero, Vector.Zero);
        particle.AcceptInput("SetParent", "!activator", pawn, particle);
        return particle;
    }

    public void ApplySpecialGrenadeBurn(IPlayer attacker, IPlayer zombie, float burnDamage, float duration, string particlePath, string soundPath)
    {
        if (attacker is not { IsValid: true } || zombie is not { IsValid: true })
            return;

        int playerId = zombie.PlayerID;
        ClearPlayerGrenadeBurn(playerId);

        if (duration <= 0f)
            return;

        var pawn = zombie.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        CParticleSystem? particle = null;
        var origin = pawn.AbsOrigin;
        if (origin != null && !string.IsNullOrWhiteSpace(particlePath))
        {
            Vector offsetPos = new(origin.Value.X, origin.Value.Y, origin.Value.Z + 15f);
            particle = CreateParticleAtPos(pawn, offsetPos, particlePath);
        }

        float startTime = _core.Engine.GlobalVars.CurrentTime;
        float lastSoundTime = startTime - 1.0f;
        CancellationTokenSource? timer = null;
        timer = _core.Scheduler.RepeatBySeconds(0.2f, () =>
        {
            if (zombie is not { IsValid: true })
            {
                ClearPlayerGrenadeBurn(playerId);
                return;
            }

            var currentPawn = zombie.PlayerPawn;
            if (currentPawn == null || !currentPawn.IsValid)
            {
                ClearPlayerGrenadeBurn(playerId);
                return;
            }

            if (_core.Engine.GlobalVars.CurrentTime - startTime >= duration)
            {
                ClearPlayerGrenadeBurn(playerId);
                return;
            }

            if (burnDamage > 0f)
            {
                ApplyDamage(attacker, zombie, burnDamage, DamageTypes_t.DMG_BURN);
            }

            if (!string.IsNullOrWhiteSpace(soundPath) && _core.Engine.GlobalVars.CurrentTime - lastSoundTime >= 1.0f)
            {
                var sound = RandomSelectSound(soundPath);
                if (!string.IsNullOrWhiteSpace(sound))
                {
                    EmitSoundToEntity(zombie, sound);
                }

                lastSoundTime = _core.Engine.GlobalVars.CurrentTime;
            }
        });

        _core.Scheduler.StopOnMapChange(timer);
        _globals.ActiveGrenadeBurns[playerId] = (particle, timer);
    }

    public void ClearPlayerGrenadeBurn(int playerId)
    {
        if (!_globals.ActiveGrenadeBurns.TryGetValue(playerId, out var burn))
            return;

        if (burn.Particle != null && burn.Particle.IsValid && burn.Particle.IsValidEntity)
        {
            burn.Particle.AcceptInput("kill", 0);
        }

        burn.Timer?.Cancel();
        _globals.ActiveGrenadeBurns.Remove(playerId);
    }

    public void ClearAllGrenadeBurns()
    {
        foreach (var playerId in _globals.ActiveGrenadeBurns.Keys.ToList())
        {
            ClearPlayerGrenadeBurn(playerId);
        }
    }

    public COmniLight? CreateGrenadeLight(Vector position, float range, float brightness, string soundPath)
    {
        var light = _core.EntitySystem.CreateEntity<COmniLight>();
        if (light == null || !light.IsValid)
            return null;

        light.Enabled = true;
        light.DirectLight = 3;
        light.OuterAngle = 360f;
        light.ColorMode = 0;
        light.Shape = 0;
        light.LightStyleString = "None";
        light.Color = new SwiftlyS2.Shared.Natives.Color(255, 255, 255, 255);
        light.Brightness = brightness > 0f ? brightness : 5f;
        light.Range = range;
        light.Teleport(position, null, null);
        light.DispatchSpawn();

        var sound = RandomSelectSound(soundPath);
        if (!string.IsNullOrWhiteSpace(sound))
        {
            var lightSound = new SwiftlyS2.Shared.Sounds.SoundEvent(sound, 1.0f, 1.0f);
            lightSound.SourceEntityIndex = (int)light.Index;
            lightSound.Recipients.AddAllPlayers();
            _core.Scheduler.NextTick(() => lightSound.Emit());
        }

        return light;
    }

    public void RemoveGrenadeLight(uint lightIndex)
    {
        if (_globals.ActiveGrenadeLightTimers.TryGetValue(lightIndex, out var timer))
        {
            timer.Cancel();
            _globals.ActiveGrenadeLightTimers.Remove(lightIndex);
        }

        if (_globals.ActiveGrenadeLights.TryGetValue(lightIndex, out var light))
        {
            if (light.IsValid && light.IsValidEntity)
            {
                light.AcceptInput("kill", 0);
            }

            _globals.ActiveGrenadeLights.Remove(lightIndex);
        }
    }

    public void ClearAllGrenadeLights()
    {
        foreach (var lightIndex in _globals.ActiveGrenadeLightTimers.Keys.ToList())
        {
            RemoveGrenadeLight(lightIndex);
        }

        foreach (var lightIndex in _globals.ActiveGrenadeLights.Keys.ToList())
        {
            RemoveGrenadeLight(lightIndex);
        }
    }

    public void ApplyFreezeGrenade(IPlayer player, float duration)
    {
        if (player is not { IsValid: true } || duration <= 0f)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
            return;

        int playerId = player.PlayerID;
        ulong sessionId = player.SessionId;
        int roundGeneration = GetCurrentRoundGeneration();

        ClearFreezeGrenade(playerId, unfreeze: false);
        SetFreezeState(player, true);

        var timer = _core.Scheduler.DelayBySeconds(duration, () =>
        {
            _globals.ActiveFreezeGrenades.Remove(playerId);

            if (!TryResolveCurrentPlayer(playerId, sessionId, roundGeneration, out var currentPlayer, requireAlive: true))
                return;

            var currentController = currentPlayer.Controller;
            if (currentController == null || !currentController.IsValid)
                return;

            if (!_globals.GameStart && currentController.TeamNum == (byte)Team.T)
                return;

            SetFreezeState(currentPlayer, false);
        });

        _core.Scheduler.StopOnMapChange(timer);
        _globals.ActiveFreezeGrenades[playerId] = timer;
    }

    public void ClearFreezeGrenade(int playerId, bool unfreeze = true)
    {
        if (_globals.ActiveFreezeGrenades.TryGetValue(playerId, out var timer))
        {
            timer?.Cancel();
            _globals.ActiveFreezeGrenades.Remove(playerId);
        }

        if (!unfreeze)
            return;

        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player is { IsValid: true })
        {
            SetFreezeState(player, false);
        }
    }

    public void ClearPlayerGrenadeEffects(int playerId)
    {
        ClearPlayerGrenadeBurn(playerId);
        ClearFreezeGrenade(playerId);
    }

    public void ClearAllGrenadeEffects()
    {
        ClearAllGrenadeBurns();

        foreach (var playerId in _globals.ActiveFreezeGrenades.Keys.ToList())
        {
            ClearFreezeGrenade(playerId);
        }

        ClearAllGrenadeLights();
        _globals.SpecialHegrenadeEntityIds.Clear();
        _globals.SpecialFlashbangEntityIds.Clear();
    }

    private static float GetDistanceSquared(Vector left, Vector right)
    {
        float x = left.X - right.X;
        float y = left.Y - right.Y;
        float z = left.Z - right.Z;
        return x * x + y * y + z * z;
    }

    public void RemoveRoundObjective()
    {
        var objectivelist = new List<string>() { "func_bomb_target", "func_hostage_rescue", "hostage_entity", "c4" };

        foreach (string objectivename in objectivelist)
        {
            var entityIndex = _core.EntitySystem.GetAllEntitiesByDesignerName<CEntityInstance>(objectivename);

            foreach (var entity in entityIndex)
            {
                if (entity != null && entity.IsValid)
                {
                    entity.AcceptInput("Kill", 0, null, null);
                }

            }
        }
    }

    public void TeleportZombie(IPlayer player) //传送僵尸 回合结束隐藏僵尸
    {
        if (player is not { IsValid: true })
            return;

        var clientpawn = player.PlayerPawn;
        if (clientpawn == null)
            return;

        if (player.IsFakeClient)
        {
            var entities = _core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").FirstOrDefault();
            var position = entities?.AbsOrigin;
            var EntAngle = entities?.AbsRotation;
            var EntVelocity = entities?.AbsVelocity;
            if (position != null)
            {
                player?.Teleport((SwiftlyS2.Shared.Natives.Vector)position, (SwiftlyS2.Shared.Natives.QAngle)EntAngle!, (SwiftlyS2.Shared.Natives.Vector)EntVelocity!);
            }

        }

    }

    public void ChangeMap()
    {
        var CFG = _mainConfig.CurrentValue;

        string baseConfig = _core.Configuration.GetConfigPath("");
        string mapsConfig = Path.Combine(baseConfig, "mapsconfig");

        // 如果 mapsconfig 文件夹不存在 → 使用默认地图
        if (!Directory.Exists(mapsConfig))
        {
            FallbackToDefault();
            return;
        }

        // 根据主配置读取对应文件
        string fileName = CFG.useworkshopmap switch
        {
            1 => "RandomMapConfig.jsonc",   // 混合集
            2 => "WorkShopMapConfig.jsonc", // 只读取工坊
            3 => "MapConfig.jsonc",         // 只读取官图
            _ => "MapConfig.jsonc"
        };

        string fullPath = Path.Combine(mapsConfig, fileName);

        // 文件不存在 → 使用默认地图
        if (!File.Exists(fullPath))
        {
            _core.Logger.LogWarning($"{_core.Localizer["MapCfgError"]}: {fullPath}");
            FallbackToDefault();
            return;
        }

        // 读取有效地图行
        var mapList = File.ReadAllLines(fullPath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .ToList();

        // 如果配置为空 → 默认地图
        if (mapList.Count == 0)
        {
            _core.Logger.LogWarning($"{_core.Localizer["MapCfgEmpty"]}: {fullPath}");
            FallbackToDefault();
            return;
        }

        // 随机选择一个地图项
        string selected = mapList[Random.Shared.Next(mapList.Count)];

        // 判断是 官图 OR 工坊地图
        bool isWorkshop = selected.All(char.IsDigit);

        _logger.LogInformation($"{_core.Localizer["MapRandomSelect"]}: {selected} (Workshop: {isWorkshop})");

        // 执行更换地图
        _core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            if (isWorkshop)
            {
                // → 工坊地图：使用 host_workshop_map
                _core.Engine.ExecuteCommand($"host_workshop_map {selected}");
            }
            else
            {
                // → 普通地图：使用 changelevel
                _core.Engine.ExecuteCommand($"changelevel {selected}");
            }
        });
    }


    private void FallbackToDefault()
    {
        _core.Logger.LogWarning($"{_core.Localizer["UseDefaultMap"]}：de_dust2");
        _core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            _core.Engine.ExecuteCommand("changelevel de_dust2");
        });
    }



    public List<Zombie> GetZombiesForCurrentLevel(int currentDayIndex)
    {
        var Dayconfig = _dayConfig.GetConfig();
        var Zombieconfig = _zombieConfig.GetConfig();
        //_core.Logger.LogInformation($"[僵尸选择] 当前Day索引: {currentDayIndex}");

        if (currentDayIndex < 0 || currentDayIndex >= Dayconfig.Days.Count)
        {
            //_core.Logger.LogInformation("[僵尸选择] 索引无效，返回空列表");
            return new List<Zombie>();
        }

        var day = Dayconfig.Days[currentDayIndex];
        List<Zombie> zombiesForLevel = new List<Zombie>();

        //_core.Logger.LogInformation($"[僵尸选择] 当前关卡名: {day.DayName}, ZombieOverride = {day.ZombieOverride}");

        if (!string.IsNullOrEmpty(day.ZombieOverride))
        {
            var zombieNames = day.ZombieOverride.Split(',');
            //_core.Logger.LogInformation($"[僵尸选择] 指定僵尸数量: {zombieNames.Length}");

            foreach (var zombieName in zombieNames)
            {
                var normalizedName = zombieName.Trim();
                //_core.Logger.LogInformation($"[僵尸选择] 查找僵尸: {zombieName}");
                var zombie = Zombieconfig.ZombieList.FirstOrDefault(
                    z => z.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)
                );

                if (zombie != null)
                {
                    //_core.Logger.LogInformation($"[僵尸选择] 找到僵尸: {zombie.Name}");
                    zombiesForLevel.Add(zombie);
                }
                else
                {
                    _core.Logger.LogWarning($"{_core.Localizer["NoZombieByName", normalizedName]}");
                }
            }
        }
        else
        {
            if (Zombieconfig != null)
            {
                //_core.Logger.LogInformation($"[僵尸选择] 没指定僵尸，使用默认列表，共 {Zombieconfig.ZombieList.Count} 个。");
                zombiesForLevel = Zombieconfig.ZombieList;
            }
            else
            {
                _core.Logger.LogWarning($"{_core.Localizer["ZombieCfgError"]}");
            }
        }

        //_core.Logger.LogInformation($"[僵尸选择] 最终僵尸数量: {zombiesForLevel.Count}");
        return zombiesForLevel;
    }


    public void UpdateKillCount()
    {
        if (_globals.CurrentMapIsHighDiff)
        {
            // 如果当前地图是高难度，击杀数不会增加
            return;
        }

        // 允许普通难度增加击杀数
        _globals.KillCount++;

        // 计算击杀百分比
        _globals.KillPercent = MathF.Round(_globals.KillCount * 0.1f, 1);

        // 当击杀百分比达到 100% 时，标记下一张地图为高难度
        if (_globals.KillPercent >= 100f)
        {
            _globals.HightDiff = true;
        }
        else
        {
            _globals.HightDiff = false; // **如果击杀百分比低于 100%，重置为普通难度**
        }
    }


    public void RespawnClient(CCSPlayerController Controller)
    {
        if (!Controller.IsValid || Controller.PawnIsAlive)
            return;

        Controller.Respawn();
    }

    public void DeleSpawnProtect(IPlayer player) //删除重生保护
    {
        if (player == null || !player.IsValid)
            return;
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.TeamNum == 3 && _globals.InProtect[player.PlayerID])
        {
            _globals.InProtect[player.PlayerID] = false;
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["RemoveProtect"]}");
        }
    }

    public void NoBlock(CCSPlayerPawn pawn) //碰撞体积关闭
    {
        if (pawn == null || !pawn.IsValid)
            return;

        pawn.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
        pawn.CollisionRulesChanged();
    }

    public void GiveCash(IPlayer player, int account, string info) //给予金钱
    {
        if (player == null || !player.IsValid)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return;

        var CFG = _mainConfig.CurrentValue;

        var Ims = controller.InGameMoneyServices;
        if (Ims == null || !Ims.IsValid)
            return;

        int current = Ims.Account;
        int max = _core.ConVar.Find<int>("mp_maxmoney")?.Value ?? 16000;
        int newMoney = Math.Min(current + account, max);
        Ims.Account = newMoney;
        controller.InGameMoneyServicesUpdated();

        var loc = _core.Translation.GetPlayerLocalizer(player);

        if (info == "hurt" && CFG.HurtMoneyMessage)
        {
            player.SendMessage(MessageType.Chat, $"{loc["HurtMoney", account]}");
        }
        else if (info == "death" && CFG.DeathMoneyMessage)
        {
            player.SendMessage(MessageType.Chat, $"{loc["KillMoney", account]}");
        }
    }

    /*
    
    public void SetPlayerScale(IPlayer player, float scale)
    {
        if (!player.IsValid || player == null)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var skeletonInstance = pawn!.CBodyComponent?.SceneNode?.GetSkeletonInstance();
        if (skeletonInstance != null)
        {
            skeletonInstance.Scale = scale;
        }
        pawn.SetScale(scale);
    }
    
    */

    public void SetTeamScore(Team team) //设置队伍分数
    {
        var teamManagers = _core.EntitySystem.GetAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");

        foreach (var teamManager in teamManagers)
        {
            if ((int)team == teamManager.TeamNum)
            {
                teamManager.Score += 1;
                teamManager.ScoreUpdated();
            }
        }
    }









    public void TerminateRound(RoundEndReason reason, float delay)
    {
        var gameRules = _core.EntitySystem.GetGameRules();
        if (gameRules is not { IsValid: true, WarmupPeriod: false })
            return;

        gameRules.TerminateRound(reason, delay);
    }



    public void SetFreezeState(IPlayer player, bool freeze)
    {
        if (!player.IsValid)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        var moveType = freeze ? MoveType_t.MOVETYPE_NONE : MoveType_t.MOVETYPE_WALK;
        pawn.MoveType = moveType;
        pawn.ActualMoveType = moveType;
        pawn.MoveTypeUpdated();
    }

    public void SetAllZombieUnFreeze()
    {
        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (player is not { IsValid: true })
                continue;

            var controller = player.Controller;
            if (controller == null || !controller.IsValid)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var moveType = MoveType_t.MOVETYPE_WALK;
            pawn.MoveType = moveType;
            pawn.ActualMoveType = moveType;
            pawn.MoveTypeUpdated();
        }
    }

    public void HumanDeathCountDown(int expectedRoundGeneration)
    {
        _globals.g_DeathCountDown?.Cancel();
        _globals.g_DeathCountDown = null;

        CancellationTokenSource? deathTimer = null;
        deathTimer = _core.Scheduler.DelayAndRepeatBySeconds(1.0f, 1.0f, () =>
        {
            if (!IsRoundGenerationCurrent(expectedRoundGeneration))
            {
                deathTimer?.Cancel();
                return;
            }

            var allPlayers = _core.PlayerManager.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                try
                {
                    if (player is not { IsValid: true } || player.IsFakeClient)
                        continue;

                    int playerId = player.PlayerID;
                    if (playerId < 0 || playerId >= _globals.HumanRespawnRemaining.Length)
                        continue;

                    int remaining = _globals.HumanRespawnRemaining[playerId];
                    if (remaining <= 0)
                        continue;

                    if (player.Controller is not { IsValid: true } controller)
                    {
                        _globals.HumanRespawnRemaining[playerId] = 0;
                        continue;
                    }

                    if (controller.TeamNum != (byte)Team.CT || controller.PawnIsAlive)
                    {
                        _globals.HumanRespawnRemaining[playerId] = 0;
                        continue;
                    }

                    remaining--;
                    _globals.HumanRespawnRemaining[playerId] = remaining;

                    if (remaining <= 0)
                    {
                        int generation = expectedRoundGeneration;

                        _core.Scheduler.NextTick(() =>
                        {
                            if (!IsRoundGenerationCurrent(generation))
                                return;

                            if (player is not { IsValid: true } currentPlayer)
                                return;

                            if (currentPlayer.Controller is not { IsValid: true } currentController)
                                return;

                            if (currentController.TeamNum != (byte)Team.CT || currentController.PawnIsAlive)
                                return;

                            RespawnClient(currentController);
                            currentPlayer.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(currentPlayer)["Spawned"]}");
                        });

                        continue;
                    }

                    player.SendMessage(MessageType.CenterHTML, $"{_core.Translation.GetPlayerLocalizer(player)["ReSpawn", remaining]}");
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError($"Human respawn timer error: {ex.Message}");
                }
            }
        });

        _globals.g_DeathCountDown = deathTimer;
        _core.Scheduler.StopOnMapChange(_globals.g_DeathCountDown);
    }

    public void ZombieRegenTimer(int expectedRoundGeneration)
    {
        _globals.g_ZombieRegenTimer?.Cancel();
        _globals.g_ZombieRegenTimer = null;

        CancellationTokenSource? regenTimer = null;
        regenTimer = _core.Scheduler.RepeatBySeconds(0.2f, () =>
        {
            if (!IsRoundGenerationCurrent(expectedRoundGeneration))
            {
                regenTimer?.Cancel();
                return;
            }

            float now = Environment.TickCount64 / 1000f;
            var aliveZombies = _core.PlayerManager.GetTAlive();

            foreach (var player in aliveZombies)
            {
                try
                {
                    if (!_globals.g_ZombieRegenStates.TryGetValue(player.PlayerID, out var state))
                        continue;

                    var pawn = player.PlayerPawn;
                    if (pawn == null || !pawn.IsValid)
                        continue;

                    int maxHealth = Math.Max(pawn.MaxHealth, pawn.Health);
                    if (pawn.Health >= maxHealth)
                        continue;

                    if (now < state.NextRegenTime)
                        continue;

                    pawn.Health = Math.Min(pawn.Health + state.RegenAmount, maxHealth);
                    pawn.HealthUpdated();

                    state.NextRegenTime = now + state.RegenInterval;
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError($"Regen Error: {ex.Message}");
                }
            }
        });

        _globals.g_ZombieRegenTimer = regenTimer;
        _core.Scheduler.StopOnMapChange(_globals.g_ZombieRegenTimer);
    }

    public void GlobalHudTimer(int expectedRoundGeneration)
    {
        _globals.g_HUDTimer?.Cancel();
        _globals.g_HUDTimer = null;

        CancellationTokenSource? hudTimer = null;
        hudTimer = _core.Scheduler.RepeatBySeconds(0.1f, () =>
        {
            if (!IsRoundGenerationCurrent(expectedRoundGeneration))
            {
                hudTimer?.Cancel();
                return;
            }

            var aliveHumans = _core.PlayerManager.GetCTAlive();
            foreach (var player in aliveHumans)
            {
                try
                {
                    if (player is not { IsValid: true } || player.IsFakeClient)
                        continue;

                    _hud.Show(player);
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError($"HUD Timer Error: {ex.Message}");
                }
            }
        });

        _globals.g_HUDTimer = hudTimer;
        _core.Scheduler.StopOnMapChange(_globals.g_HUDTimer);
    }
}
