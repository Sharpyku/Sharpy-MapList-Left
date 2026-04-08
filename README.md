# Sharpy-MapList

A standalone **CounterStrikeSharp** plugin that adds `!mapsdone`, `!mapsleft`, and `!profile` commands for servers running [SharpTimer](https://github.com/DEAFPS/SharpTimer).

Reads directly from SharpTimer's MySQL database — no modifications to SharpTimer needed.

## Features

- **!mapsdone / !md** — Paginated list of maps you've completed with your best time
- **!mapsleft / !ml** — Paginated list of maps you haven't completed yet  
- **!profile / !stats / !p** — Player profile showing maps completed, points, rank, playtime, connections
- Per-style filtering (Normal, Sideways, Auto-Strafe, etc.)
- Auto-generating config JSON
- Uses SharpTimer's `MapPool` table when available for accurate total map counts

## Requirements

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) v1.0.364+
- [SharpTimer](https://github.com/DEAFPS/SharpTimer) with MySQL enabled
- .NET 8.0 Runtime

## Installation

1. Download the latest release from [Releases](https://github.com/Sharpyku/Sharpy-MapList/releases)
2. Extract to `csgo/addons/counterstrikesharp/plugins/Sharpy-MapList/`
3. Restart server or load the plugin
4. Edit the auto-generated config at `csgo/addons/counterstrikesharp/configs/plugins/Sharpy-MapList/Sharpy-MapList.json`
5. Set your database credentials (same as SharpTimer's `mysqlConfig.json`)
6. Restart/reload

## Config

```json
{
  "DatabaseHost": "localhost",
  "DatabasePort": 3306,
  "DatabaseName": "sharptimer",
  "DatabaseUser": "root",
  "DatabasePassword": "",
  "PlayerRecordsTable": "PlayerRecords",
  "PlayerStatsTable": "PlayerStats",
  "ChatPrefix": " [{GREEN}★{DEFAULT}] {GOLD}EliteGames{DEFAULT}",
  "ItemsPerPage": 8
}
```

## Commands

| Command | Alias | Description | Usage |
|---------|-------|-------------|-------|
| `!mapsdone` | `!md` | Maps completed | `!md [page] [style]` |
| `!mapsleft` | `!ml` | Maps remaining | `!ml [page] [style]` |
| `!profile` | `!p`, `!stats` | Player profile | `!p [style]` |

### Style IDs

| ID | Style |
|----|-------|
| 0 | Normal |
| 1 | Sideways |
| 2 | Half-Sideways |
| 3 | Backwards |
| 4 | Low Gravity |
| 5 | Slow Motion |
| 10 | Auto-Strafe |
| 11 | 250 Vel Max |
| 12 | 400 Vel Max |

## Building

```bash
dotnet build --configuration Release
```

Output: `bin/Release/net8.0/Sharpy-MapList.dll`

## License

MIT
