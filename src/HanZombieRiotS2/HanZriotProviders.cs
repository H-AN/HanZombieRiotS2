using Microsoft.Extensions.Logging;
using System.Text.Json;
using SwiftlyS2.Shared;

namespace HanZombieRiotS2;

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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly ISwiftlyCore _core;
    private HanZriotDayConfig _current = new();

    public StageConfigProvider(ISwiftlyCore core)
    {
        _core = core;
    }

    public HanZriotDayConfig GetConfig() => _current;

    public void Reload(string difficulty, string mapName)
    {
        string fileName = string.IsNullOrEmpty(difficulty)
            ? "HanZriotDayConfig.jsonc"
            : $"HanZriotDayConfig_{difficulty}.jsonc";

        string? fullPath = ResolveConfigPath(fileName, mapName);
        if (fullPath == null)
        {
            _core.Logger.LogWarning($"Stage config file not found: {fileName}");
            _current = new HanZriotDayConfig();
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonSerializer.Deserialize<HanZriotDayConfigWrapper>(json, JsonOptions);
            _current = wrapper?.ZriotDayCFG ?? new HanZriotDayConfig();
            _core.Logger.LogInformation($"[Provider] Stage config loaded: {fullPath}, Days count={_current.Days?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            _core.Logger.LogError($"[Provider] Failed to deserialize stage config {fullPath}: {ex}");
            _current = new HanZriotDayConfig();
        }
    }

    private string? ResolveConfigPath(string fileName, string mapName)
    {
        string baseConfig = _core.Configuration.GetConfigPath("");
        string mapPath = Path.Combine(baseConfig, mapName, fileName);
        if (File.Exists(mapPath))
            return mapPath;

        string defaultPath = Path.Combine(baseConfig, fileName);
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private sealed class HanZriotDayConfigWrapper
    {
        public HanZriotDayConfig ZriotDayCFG { get; set; } = new();
    }
}

public class ZombieConfigProvider : IZombieConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly ISwiftlyCore _core;
    private ZombieDataConfig _current = new();

    public ZombieConfigProvider(ISwiftlyCore core)
    {
        _core = core;
    }

    public ZombieDataConfig GetConfig() => _current;

    public void Reload(string difficulty, string mapName)
    {
        string fileName = string.IsNullOrEmpty(difficulty)
            ? "ZombieDataConfig.jsonc"
            : $"ZombieDataConfig_{difficulty}.jsonc";

        string? fullPath = ResolveConfigPath(fileName, mapName);
        if (fullPath == null)
        {
            _core.Logger.LogWarning($"[Provider] Zombie config file not found: {fileName}");
            _current = new ZombieDataConfig();
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonSerializer.Deserialize<HanZriotZombieConfigWrapper>(json, JsonOptions);
            _current = wrapper?.ZriotZombieCFG ?? new ZombieDataConfig();
            _core.Logger.LogInformation($"[Provider] Zombie config loaded: {fullPath}, ZombieList count={_current.ZombieList?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            _core.Logger.LogError($"[Provider] Failed to deserialize zombie config {fullPath}: {ex}");
            _current = new ZombieDataConfig();
        }
    }

    private string? ResolveConfigPath(string fileName, string mapName)
    {
        string baseConfig = _core.Configuration.GetConfigPath("");
        string mapPath = Path.Combine(baseConfig, mapName, fileName);
        if (File.Exists(mapPath))
            return mapPath;

        string defaultPath = Path.Combine(baseConfig, fileName);
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private sealed class HanZriotZombieConfigWrapper
    {
        public ZombieDataConfig ZriotZombieCFG { get; set; } = new();
    }
}
