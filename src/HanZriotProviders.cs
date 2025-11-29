using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;

namespace HanZombieRiotS2
{
    public interface IStageConfigProvider
    {
        HanZriotDayConfig GetConfig();
        void Reload(string difficulty, string mapName);
    }

    public interface IZombieConfigProvider
    {
        ZombieDataConfig GetConfig();
        void Reload(string difficulty, string mapName);
    }

   
    public class StageConfigProvider : IStageConfigProvider
    {
        private readonly ISwiftlyCore _core; 
        private HanZriotDayConfig _current = new();

        public StageConfigProvider(ISwiftlyCore core)
        {
            _core = core;
        }
        public HanZriotDayConfig GetConfig() => _current;

        public void Reload(string difficulty, string mapName) 
        {
            //获取插件配置根目录
            string baseConfig = _core.Configuration.GetConfigPath(""); //普通配置目录

            //拼接地图专属目录
            string mapsDir = Path.Combine(baseConfig, mapName);

            //地图目录存在
            string configFolder = Directory.Exists(mapsDir) ? mapsDir : baseConfig;

            string dayFile = string.IsNullOrEmpty(difficulty) ? "HanZriotDayConfig.jsonc" : $"HanZriotDayConfig_{difficulty}.jsonc";
            string fullPath = Path.Combine(configFolder, dayFile);

            
            if (!File.Exists(fullPath))
            {
                _core.Logger.LogWarning($"Stage config file not found: {fullPath}");
                _current = new HanZriotDayConfig();
                return;
            }
            
            string json = File.ReadAllText(fullPath);
            
            try
            {
                var wrapper = JsonSerializer.Deserialize<HanZriotDayConfigWrapper>(json);
                _current = wrapper?.ZriotDayCFG ?? new HanZriotDayConfig();

                _core.Logger.LogInformation($"[Provider] Stage config loaded: {dayFile}, Days count={_current.Days?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                _core.Logger.LogError($"[Provider] Failed to deserialize stage config: {ex}");
                _current = new HanZriotDayConfig();
            }

            _core.Logger.LogInformation($"Stage config loaded: {dayFile}");
        }

        private class HanZriotDayConfigWrapper
        {
            public HanZriotDayConfig ZriotDayCFG { get; set; } = new HanZriotDayConfig();
        }
    }


    public class ZombieConfigProvider : IZombieConfigProvider
    {
        private readonly ISwiftlyCore _core;
        private ZombieDataConfig _current = new();

        public ZombieConfigProvider(ISwiftlyCore core)
        {
            _core = core;
        }

        public ZombieDataConfig GetConfig() => _current;

        public void Reload(string difficulty, string mapName) 
        {
            //获取插件配置根目录
            string baseConfig = _core.Configuration.GetConfigPath(""); // 普通配置目录

            //拼接地图专属目录
            string mapsDir = Path.Combine(baseConfig, mapName);

            //地图目录存在
            string configFolder = Directory.Exists(mapsDir) ? mapsDir : baseConfig;


            string zombieFile = string.IsNullOrEmpty(difficulty)? "ZombieDataConfig.jsonc": $"ZombieDataConfig_{difficulty}.jsonc";

            string fullPath = Path.Combine(configFolder, zombieFile);
            
            if (!File.Exists(fullPath))
            {
                _core.Logger.LogWarning($"[Provider] ZombieData config file not found: {fullPath}");
                _current = new ZombieDataConfig();
                return;
            }
            

            string json = File.ReadAllText(fullPath);

            try
            {
                var wrapper = JsonSerializer.Deserialize<HanZriotZombieConfigWrapper>(json);
                _current = wrapper?.ZriotZombieCFG ?? new ZombieDataConfig();
                _core.Logger.LogInformation($"[Provider] Zombie config loaded: {zombieFile}, ZombieList count={_current.ZombieList?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                _core.Logger.LogError($"[Provider] Failed to deserialize zombie config: {ex}");
                _current = new ZombieDataConfig();
            }
        }

        private class HanZriotZombieConfigWrapper
        {
            public ZombieDataConfig ZriotZombieCFG { get; set; } = new ZombieDataConfig();
        }
    }
       
    }
