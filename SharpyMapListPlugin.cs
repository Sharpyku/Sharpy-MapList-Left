using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace SharpyMapList;

public class SharpyMapListPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Sharpy-MapList";
    public override string ModuleAuthor => "Sharpyku";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "Maps Done / Maps Left / Profile commands for SharpTimer";

    public PluginConfig Config { get; set; } = new();
    private Database? _db;

    // Style names matching SharpTimer
    private static readonly Dictionary<int, string> StyleNames = new()
    {
        { 0, "Normal" }, { 1, "Sideways" }, { 2, "Half-Sideways" },
        { 3, "Backwards" }, { 4, "Low Gravity" }, { 5, "Slow Motion" },
        { 6, "Fast Forward" }, { 7, "No Strafe Limit" }, { 8, "Turbo" },
        { 10, "Auto-Strafe" }, { 11, "250 Vel Max" }, { 12, "400 Vel Max" }
    };

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _db = new Database(Config);

        _ = Task.Run(async () =>
        {
            bool ok = await _db.TestConnectionAsync();
            Server.NextFrame(() =>
            {
                if (ok)
                    Logger.LogInformation("[Sharpy-MapList] Database connected successfully.");
                else
                    Logger.LogError("[Sharpy-MapList] Database connection FAILED! Check config.");
            });
        });
    }

    private string GetPrefix()
    {
        return Config.ChatPrefix
            .Replace("{GREEN}", $"{ChatColors.Green}")
            .Replace("{DEFAULT}", $"{ChatColors.Default}")
            .Replace("{GOLD}", $"{ChatColors.Gold}")
            .Replace("{LIME}", $"{ChatColors.Lime}")
            .Replace("{RED}", $"{ChatColors.Red}")
            .Replace("{GREY}", $"{ChatColors.Grey}")
            .Replace("{YELLOW}", $"{ChatColors.Yellow}")
            .Replace("{WHITE}", $"{ChatColors.White}")
            .Replace("{ORANGE}", $"{ChatColors.Orange}")
            .Replace("{BLUE}", $"{ChatColors.Blue}")
            .Replace("{PURPLE}", $"{ChatColors.Purple}")
            .Replace("{LIGHTRED}", $"{ChatColors.LightRed}")
            .Replace("{LIGHTBLUE}", $"{ChatColors.LightBlue}");
    }

    private static string GetStyleName(int style) =>
        StyleNames.TryGetValue(style, out var name) ? name : $"Style {style}";

    private static string FormatTime(int ticks)
    {
        double totalSeconds = ticks / 64.0;
        int minutes = (int)(totalSeconds / 60);
        double seconds = totalSeconds - minutes * 60;
        return minutes > 0
            ? $"{minutes}:{seconds:00.000}"
            : $"{seconds:0.000}";
    }

    private int GetPlayerStyle(CCSPlayerController player)
    {
        // Try to read SharpTimer's style from the player's ConVar
        // Fallback: 0 (Normal)
        // SharpTimer stores style in playerTimers dict, we can't access that
        // So we accept an optional style argument on commands: !md 1 2 = page 1, style 2
        return 0;
    }

    // ── !mapsdone / !md ──────────────────────────────────────────────

    [ConsoleCommand("css_mapsdone", "Shows maps you have completed")]
    [ConsoleCommand("css_md", "alias for !mapsdone")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void MapsDoneCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        if (_db == null) { player.PrintToChat($"{GetPrefix()} Database not configured."); return; }

        var steamId = player.SteamID.ToString();
        int page = 0;
        int style = 0;

        // Parse args: !md [page] [style]
        if (command.ArgCount > 1 && int.TryParse(command.ArgByIndex(1), out int p)) page = Math.Max(0, p - 1);
        if (command.ArgCount > 2 && int.TryParse(command.ArgByIndex(2), out int s)) style = s;

        var db = _db;
        var prefix = GetPrefix();
        int perPage = Config.ItemsPerPage;

        _ = Task.Run(async () =>
        {
            try
            {
                var mapsDone = await db.GetPlayerMapsDoneAsync(steamId, style);
                int totalMaps = await db.GetTotalValidMapsAsync(style);

                if (mapsDone.Count == 0)
                {
                    Server.NextFrame(() => player?.PrintToChat($"{prefix} {ChatColors.Grey}You haven't completed any maps yet!"));
                    return;
                }

                int totalPages = (int)Math.Ceiling(mapsDone.Count / (double)perPage);
                page = Math.Min(page, totalPages - 1);
                var pageItems = mapsDone.Skip(page * perPage).Take(perPage).ToList();
                string styleName = GetStyleName(style);
                string pct = totalMaps > 0 ? (mapsDone.Count * 100.0 / totalMaps).ToString("F1") : "0";

                var lines = new List<string>
                {
                    $"{prefix} {ChatColors.Green}Maps Done {ChatColors.Grey}({styleName}){ChatColors.Default}: {ChatColors.Lime}{mapsDone.Count}{ChatColors.Default}/{ChatColors.Lime}{totalMaps} {ChatColors.Grey}({pct}%) {ChatColors.Default}[Page {page + 1}/{totalPages}]"
                };

                foreach (var (mapName, ticks) in pageItems)
                    lines.Add($" {ChatColors.Grey}• {ChatColors.Lime}{mapName} {ChatColors.Default}- {ChatColors.Green}{FormatTime(ticks)}");

                if (totalPages > 1)
                    lines.Add($" {ChatColors.Grey}Use {ChatColors.Lime}!mapsdone {page + 2} {ChatColors.Grey}for next page");

                Server.NextFrame(() =>
                {
                    if (player == null || !player.IsValid) return;
                    foreach (var line in lines) player.PrintToChat(line);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Sharpy-MapList] MapsDone error: {ex.Message}");
            }
        });
    }

    // ── !mapsleft / !ml ──────────────────────────────────────────────

    [ConsoleCommand("css_mapsleft", "Shows maps you have not completed")]
    [ConsoleCommand("css_ml", "alias for !mapsleft")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void MapsLeftCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        if (_db == null) { player.PrintToChat($"{GetPrefix()} Database not configured."); return; }

        var steamId = player.SteamID.ToString();
        int page = 0;
        int style = 0;

        if (command.ArgCount > 1 && int.TryParse(command.ArgByIndex(1), out int p)) page = Math.Max(0, p - 1);
        if (command.ArgCount > 2 && int.TryParse(command.ArgByIndex(2), out int s)) style = s;

        var db = _db;
        var prefix = GetPrefix();
        int perPage = Config.ItemsPerPage;

        _ = Task.Run(async () =>
        {
            try
            {
                var mapsLeft = await db.GetPlayerMapsLeftAsync(steamId, style);
                int totalMaps = await db.GetTotalValidMapsAsync(style);

                if (mapsLeft.Count == 0)
                {
                    Server.NextFrame(() => player?.PrintToChat($"{prefix} {ChatColors.Green}You've completed ALL maps! GG!"));
                    return;
                }

                int totalPages = (int)Math.Ceiling(mapsLeft.Count / (double)perPage);
                page = Math.Min(page, totalPages - 1);
                var pageItems = mapsLeft.Skip(page * perPage).Take(perPage).ToList();
                string styleName = GetStyleName(style);
                string pct = totalMaps > 0 ? (mapsLeft.Count * 100.0 / totalMaps).ToString("F1") : "0";

                var lines = new List<string>
                {
                    $"{prefix} {ChatColors.LightRed}Maps Left {ChatColors.Grey}({styleName}){ChatColors.Default}: {ChatColors.Lime}{mapsLeft.Count}{ChatColors.Default}/{ChatColors.Lime}{totalMaps} {ChatColors.Grey}({pct}% remaining) {ChatColors.Default}[Page {page + 1}/{totalPages}]"
                };

                foreach (var mapName in pageItems)
                    lines.Add($" {ChatColors.Grey}• {ChatColors.LightRed}{mapName}");

                if (totalPages > 1)
                    lines.Add($" {ChatColors.Grey}Use {ChatColors.Lime}!mapsleft {page + 2} {ChatColors.Grey}for next page");

                Server.NextFrame(() =>
                {
                    if (player == null || !player.IsValid) return;
                    foreach (var line in lines) player.PrintToChat(line);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Sharpy-MapList] MapsLeft error: {ex.Message}");
            }
        });
    }

    // ── !profile / !stats / !p ───────────────────────────────────────

    [ConsoleCommand("css_profile", "Shows your player profile")]
    [ConsoleCommand("css_stats", "alias for !profile")]
    [ConsoleCommand("css_p", "alias for !profile")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void ProfileCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        if (_db == null) { player.PrintToChat($"{GetPrefix()} Database not configured."); return; }

        var steamId = player.SteamID.ToString();
        var playerName = player.PlayerName;
        int style = 0;

        if (command.ArgCount > 1 && int.TryParse(command.ArgByIndex(1), out int s)) style = s;

        var db = _db;
        var prefix = GetPrefix();

        _ = Task.Run(async () =>
        {
            try
            {
                var (timesConnected, globalPoints, totalPlaytime, mapsDone, totalMaps, serverRank, totalRanked) =
                    await db.GetPlayerProfileAsync(steamId, style);

                int hours = totalPlaytime / 3600;
                int minutes = (totalPlaytime % 3600) / 60;
                string completionPct = totalMaps > 0 ? (mapsDone * 100.0 / totalMaps).ToString("F1") : "0";
                string styleName = GetStyleName(style);

                Server.NextFrame(() =>
                {
                    if (player == null || !player.IsValid) return;
                    player.PrintToChat($"{prefix} {ChatColors.Green}━━━ Player Profile ━━━");
                    player.PrintToChat($" {ChatColors.Grey}Player: {ChatColors.Lime}{playerName}");
                    player.PrintToChat($" {ChatColors.Grey}Style: {ChatColors.Lime}{styleName}");
                    player.PrintToChat($" {ChatColors.Grey}Maps Completed: {ChatColors.Green}{mapsDone}{ChatColors.Default}/{ChatColors.Lime}{totalMaps} {ChatColors.Grey}({completionPct}%)");
                    if (globalPoints > 0)
                        player.PrintToChat($" {ChatColors.Grey}Points: {ChatColors.Lime}{globalPoints} {ChatColors.Grey}| Rank: {ChatColors.Green}#{serverRank}{ChatColors.Default}/{ChatColors.Lime}{totalRanked}");
                    player.PrintToChat($" {ChatColors.Grey}Playtime: {ChatColors.Green}{hours}h {minutes}m");
                    player.PrintToChat($" {ChatColors.Grey}Connections: {ChatColors.Lime}{timesConnected}");
                    player.PrintToChat($"{prefix} {ChatColors.Green}━━━━━━━━━━━━━━━━━━━━━");
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Sharpy-MapList] Profile error: {ex.Message}");
            }
        });
    }
}
