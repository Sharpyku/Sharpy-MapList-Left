using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace SharpyMapList;

public class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("DatabaseHost")]
    public string DatabaseHost { get; set; } = "localhost";

    [JsonPropertyName("DatabasePort")]
    public int DatabasePort { get; set; } = 3306;

    [JsonPropertyName("DatabaseName")]
    public string DatabaseName { get; set; } = "sharptimer";

    [JsonPropertyName("DatabaseUser")]
    public string DatabaseUser { get; set; } = "root";

    [JsonPropertyName("DatabasePassword")]
    public string DatabasePassword { get; set; } = "";

    [JsonPropertyName("PlayerRecordsTable")]
    public string PlayerRecordsTable { get; set; } = "PlayerRecords";

    [JsonPropertyName("PlayerStatsTable")]
    public string PlayerStatsTable { get; set; } = "PlayerStats";

    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = " [{GREEN}★{DEFAULT}] {GOLD}EliteGames{DEFAULT}";

    [JsonPropertyName("ItemsPerPage")]
    public int ItemsPerPage { get; set; } = 8;
}
