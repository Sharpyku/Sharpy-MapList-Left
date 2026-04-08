using MySqlConnector;

namespace SharpyMapList;

public class Database
{
    private readonly string _connectionString;
    private readonly string _recordsTable;
    private readonly string _statsTable;

    public Database(PluginConfig config)
    {
        _recordsTable = config.PlayerRecordsTable;
        _statsTable = config.PlayerStatsTable;
        _connectionString = new MySqlConnectionStringBuilder
        {
            Server = config.DatabaseHost,
            Port = (uint)config.DatabasePort,
            Database = config.DatabaseName,
            UserID = config.DatabaseUser,
            Password = config.DatabasePassword,
            SslMode = MySqlSslMode.Preferred,
            ConnectionTimeout = 30
        }.ConnectionString;
    }

    private async Task<MySqlConnection> OpenAsync()
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var conn = await OpenAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<List<(string MapName, int TimerTicks)>> GetPlayerMapsDoneAsync(string steamId, int style)
    {
        using var conn = await OpenAsync();
        using var cmd = new MySqlCommand($@"
            SELECT `MapName`, MIN(`TimerTicks`) AS BestTime
            FROM `{_recordsTable}`
            WHERE `SteamID` = @SteamID AND `Style` = @Style
              AND `MapName` NOT LIKE '%\_bonus%'
              AND `MapName` REGEXP '^[a-zA-Z]'
            GROUP BY `MapName`
            ORDER BY `MapName` ASC;", conn);
        cmd.Parameters.AddWithValue("@SteamID", steamId);
        cmd.Parameters.AddWithValue("@Style", style);

        var results = new List<(string, int)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        return results;
    }

    public async Task<List<string>> GetPlayerMapsLeftAsync(string steamId, int style)
    {
        using var conn = await OpenAsync();

        // Try MapPool first
        bool mapPoolExists = false;
        try
        {
            using var check = new MySqlCommand("SELECT 1 FROM `MapPool` LIMIT 1;", conn);
            await check.ExecuteScalarAsync();
            mapPoolExists = true;
        }
        catch { }

        MySqlCommand cmd;
        if (mapPoolExists)
        {
            cmd = new MySqlCommand($@"
                SELECT `MapName` FROM `MapPool`
                WHERE `MapName` NOT IN (
                    SELECT DISTINCT `MapName` FROM `{_recordsTable}`
                    WHERE `SteamID` = @SteamID AND `Style` = @Style
                      AND `MapName` NOT LIKE '%\_bonus%'
                )
                ORDER BY `MapName` ASC;", conn);
        }
        else
        {
            cmd = new MySqlCommand($@"
                SELECT DISTINCT `MapName`
                FROM `{_recordsTable}`
                WHERE `MapName` NOT LIKE '%\_bonus%'
                  AND `MapName` REGEXP '^[a-zA-Z]'
                  AND `Style` = @Style
                  AND `MapName` NOT IN (
                      SELECT DISTINCT `MapName` FROM `{_recordsTable}`
                      WHERE `SteamID` = @SteamID AND `Style` = @Style
                  )
                ORDER BY `MapName` ASC;", conn);
        }
        cmd.Parameters.AddWithValue("@SteamID", steamId);
        cmd.Parameters.AddWithValue("@Style", style);

        var results = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        return results;
    }

    public async Task<int> GetTotalValidMapsAsync(int style)
    {
        using var conn = await OpenAsync();

        bool mapPoolExists = false;
        try
        {
            using var check = new MySqlCommand("SELECT 1 FROM `MapPool` LIMIT 1;", conn);
            await check.ExecuteScalarAsync();
            mapPoolExists = true;
        }
        catch { }

        MySqlCommand cmd;
        if (mapPoolExists)
        {
            cmd = new MySqlCommand("SELECT COUNT(*) FROM `MapPool`;", conn);
        }
        else
        {
            cmd = new MySqlCommand($@"
                SELECT COUNT(DISTINCT `MapName`)
                FROM `{_recordsTable}`
                WHERE `MapName` NOT LIKE '%\_bonus%'
                  AND `MapName` REGEXP '^[a-zA-Z]'
                  AND `Style` = @Style;", conn);
        }
        cmd.Parameters.AddWithValue("@Style", style);

        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
    }

    public async Task<(int TimesConnected, int GlobalPoints, int TotalPlaytime, int MapsDone, int TotalMaps, int ServerRank, int TotalRanked)> GetPlayerProfileAsync(string steamId, int style)
    {
        using var conn = await OpenAsync();

        int timesConnected = 0, globalPoints = 0, totalPlaytime = 0;
        using (var statsCmd = new MySqlCommand($@"
            SELECT `TimesConnected`, COALESCE(`GlobalPoints`, 0), COALESCE(`TotalPlaytime`, 0)
            FROM `{_statsTable}`
            WHERE `SteamID` = @SteamID;", conn))
        {
            statsCmd.Parameters.AddWithValue("@SteamID", steamId);
            using var reader = await statsCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                timesConnected = reader.GetInt32(0);
                globalPoints = reader.GetInt32(1);
                totalPlaytime = reader.GetInt32(2);
            }
        }

        using (var doneCmd = new MySqlCommand($@"
            SELECT COUNT(DISTINCT `MapName`)
            FROM `{_recordsTable}`
            WHERE `SteamID` = @SteamID AND `Style` = @Style
              AND `MapName` NOT LIKE '%\_bonus%'
              AND `MapName` REGEXP '^[a-zA-Z]';", conn))
        {
            doneCmd.Parameters.AddWithValue("@SteamID", steamId);
            doneCmd.Parameters.AddWithValue("@Style", style);
            var r = await doneCmd.ExecuteScalarAsync();
            var mapsDone = r != null ? Convert.ToInt32(r) : 0;

            int totalMaps;
            using (var totalCmd = new MySqlCommand($@"
                SELECT COUNT(DISTINCT `MapName`)
                FROM `{_recordsTable}`
                WHERE `MapName` NOT LIKE '%\_bonus%'
                  AND `MapName` REGEXP '^[a-zA-Z]'
                  AND `Style` = @Style;", conn))
            {
                totalCmd.Parameters.AddWithValue("@Style", style);
                var t = await totalCmd.ExecuteScalarAsync();
                totalMaps = t != null ? Convert.ToInt32(t) : 0;
            }

            int serverRank = 0, totalRanked = 0;
            if (globalPoints > 0)
            {
                using (var rankCmd = new MySqlCommand($@"
                    SELECT COUNT(*) FROM `{_statsTable}`
                    WHERE `GlobalPoints` > @Points;", conn))
                {
                    rankCmd.Parameters.AddWithValue("@Points", globalPoints);
                    serverRank = Convert.ToInt32(await rankCmd.ExecuteScalarAsync() ?? 0) + 1;
                }

                using (var totalRankedCmd = new MySqlCommand($@"
                    SELECT COUNT(*) FROM `{_statsTable}`
                    WHERE `GlobalPoints` > 0;", conn))
                {
                    totalRanked = Convert.ToInt32(await totalRankedCmd.ExecuteScalarAsync() ?? 0);
                }
            }

            return (timesConnected, globalPoints, totalPlaytime, mapsDone, totalMaps, serverRank, totalRanked);
        }
    }
}
