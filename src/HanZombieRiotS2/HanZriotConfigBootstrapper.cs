using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using SwiftlyS2.Shared;

namespace HanZombieRiotS2;

internal static class HanZriotConfigBootstrapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static void EnsureDefaultConfigFiles(ISwiftlyCore core)
    {
        string baseConfigPath = core.Configuration.GetConfigPath("");
        Directory.CreateDirectory(baseConfigPath);

        EnsureFile(core, Path.Combine(baseConfigPath, "HanZriotDayConfig.jsonc"), new HanZriotDayConfigWrapper());
        EnsureFile(core, Path.Combine(baseConfigPath, "HanZriotDayConfig_hight.jsonc"), new HanZriotDayConfigWrapper());
        EnsureFile(core, Path.Combine(baseConfigPath, "ZombieDataConfig.jsonc"), new HanZriotZombieConfigWrapper());
        EnsureFile(core, Path.Combine(baseConfigPath, "ZombieDataConfig_hight.jsonc"), new HanZriotZombieConfigWrapper());
        EnsureFile(core, Path.Combine(baseConfigPath, "HanZriotGrenadeGroupConfig.jsonc"), new HanZriotGrenadeConfigWrapper());
    }

    private static void EnsureFile<T>(ISwiftlyCore core, string fullPath, T payload)
    {
        if (File.Exists(fullPath))
            return;

        try
        {
            string json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(fullPath, json + Environment.NewLine);
            core.Logger.LogInformation($"[Bootstrapper] Generated default config: {fullPath}");
        }
        catch (Exception ex)
        {
            core.Logger.LogError($"[Bootstrapper] Failed to generate config {fullPath}: {ex}");
        }
    }

    private sealed class HanZriotDayConfigWrapper
    {
        public HanZriotDayConfig ZriotDayCFG { get; set; } = new();
    }

    private sealed class HanZriotZombieConfigWrapper
    {
        public ZombieDataConfig ZriotZombieCFG { get; set; } = new();
    }

    private sealed class HanZriotGrenadeConfigWrapper
    {
        public HanZriotGrenadeGroupConfig ZriotGrenadeCFG { get; set; } = new();
    }
}
