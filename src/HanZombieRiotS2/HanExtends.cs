using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

public static class HanExtensions
{
    public static IPlayer? GetPlayerBySteamID(ulong? SteamID, ISwiftlyCore core)
    {
        return core.PlayerManager.GetAllPlayers().FirstOrDefault(x => !x.IsFakeClient && x.SteamID == SteamID);
    }

    public static IPlayer? GetPlayerByController(CCSPlayerController controller, ISwiftlyCore core)
    {
        return core.PlayerManager.GetPlayer((int)(controller.Index - 1));
    }

}