# MapleStory 2 Handbook - Backend (GameParser)

**Project Path:** `d:\Projetos\Maple2_Codex\MapleStory2-Handbook-BackEnd`
**Related Frontend:** `d:\Projetos\Maple2_Codex\MapleStory2-Handbook`

## Project Overview

A .NET data parsing pipeline that extracts raw MapleStory 2 game data and populates a MySQL database. The parsed data is then served to the frontend SvelteKit application via Prisma ORM.

**Technology Stack:**
- C# (.NET 8 for GameParser, .NET 6 for Maple2Storage)
- MySQL 8 with MariaDB adapter support
- Game Data Parser (Maple2.File.Parser NuGet package)
- 3D Model Conversion (NIF → GLTF)
- Lua scripting for game calculations

## Project Structure

```
MapleStory2-Handbook-BackEnd/
├── GameParser/                 # Main parsing application (.NET 8)
│   ├── Parsers/               # 7+ parser implementations
│   │   ├── ItemParser.cs      # Item definitions, stats, descriptions
│   │   ├── NpcParser.cs       # NPC data, stats, levels
│   │   ├── MapNameParser.cs   # Map definitions
│   │   ├── QuestParser.cs     # Quest data with NPC relationships
│   │   ├── AchieveParser.cs   # Achievement/trophy data
│   │   ├── ItemDropParser.cs  # Loot box definitions
│   │   └── AdditionalEffectParser.cs # Buff/effect descriptions
│   ├── DescriptionHelper/     # Text processing for descriptions
│   ├── Tools/                 # Utilities (ScriptLoader, Stats parsing)
│   ├── SQL/                   # Database schema SQL files
│   ├── Program.cs             # Entry point - orchestrates parsing
│   └── QueryManager.cs        # MySQL connection management
│
├── Maple2Storage/              # Core data structures (.NET 6)
│   ├── Types/                 # Domain models (Item, NPC, Quest, etc.)
│   ├── Enums/                 # Game enumerations
│   ├── Json/                  # Static JSON configurations
│   ├── Scripts/               # Embedded Lua scripts for calculations
│   ├── Resources/             # Game data (Xml.m2d, itemWebfinder.xml)
│   ├── Extensions/            # Helper methods
│   └── Tools/                 # DotEnv, Console utilities
│
├── NifToGltf/                 # 3D model converter (optional)
└── Maple2Codex.sln            # Visual Studio solution
```

## Database Schema

The parser creates/updates 8 tables in MySQL:

| Table | Purpose |
|-------|---------|
| `items` | Item definitions (stats, descriptions, job limits, prices) |
| `npcs` | NPC data (stats, portraits, titles, animations) |
| `maps` | Map definitions (names, icons, minimaps, backgrounds) |
| `quests` | Quest data (descriptions, rewards, NPC links) |
| `achieves` | Achievement/trophy data with multi-grade rewards |
| `item_boxes` | Loot box definitions (item drops, chances) |
| `additional_effects` | Buff/effect descriptions with leveled data |

**Schema Definition:** `d:\Projetos\Maple2_Codex\MapleStory2-Handbook\prisma\schema.prisma`

## Data Parsing Pipeline

### Input Data Source

Raw MapleStory 2 game client files placed in `Maple2Storage/Resources/`:

| File | Purpose |
|------|---------|
| `Xml.m2d` | Compressed XML with all game definitions |
| `Xml.m2h` | Compressed XML header file |
| `itemWebfinder.xml` | Item rarity and grade mappings |

### Parsing Flow (in Program.cs)

```
1. ItemParser
   - Parse item definitions from Xml.m2d
   - Extract descriptions from koritemdescription.xml
   - Process item options (constant, static, random stats)
   - Calculate gear scores using embedded Lua script
   - Determine job limits, gender restrictions, level requirements
   - Serialize complex data as JSON (job_limit, stats, kfms)
   - Insert into MySQL

2. ItemDropParser
   - Parse loot box configurations
   - Link items to item_boxes

3. NpcParser
   - Extract NPC data (stats, animations, level)
   - Extract portrait paths
   - Insert into MySQL

4. MapNameParser
   - Parse map definitions
   - Extract icons, backgrounds, minimaps

5. AchieveParser
   - Parse achievements with grade rewards
   - Link achievement rewards to items

6. AdditionalEffectParser
   - Parse buff/effect descriptions
   - Handle leveled effect data

7. QuestParser
   - Parse quest definitions
   - Link to NPCs (start/complete)
   - Extract rewards and requirements
```

### Key Parsing Classes

- **ItemParser.cs** - Main item parsing logic (~400 lines)
- **StatsParser.cs** - Item stat extraction and formatting
- **DescriptionHelper.cs** - Text processing for descriptions
- **Paths.cs** - Resolves game resource file locations
- **QueryManager.cs** - MySQL connection pool
- **SerializeOptions.cs** - JSON serialization settings

## Database Connection

**Configuration:** `.env` file in project root

```
DB_IP=localhost
DB_PORT=3306
DB_USER=root
DB_PASSWORD=root
DB_NAME=maple2_codex
```

**Connection Management:** `QueryManager.cs` manages MySQL connections via SqlKata QueryFactory

## Frontend Integration

### ⚠️ Important: Frontend Cannot Create New Data

The frontend (SvelteKit) has **read-only access** to the MySQL database through Prisma ORM.

- **Frontend Prisma schema** reflects the database structure created by this backend parser
- **Changing Prisma schema in frontend** does NOT create new tables or columns
- **All new data** comes from running GameParser with updated game files
- **Frontend modifications** can only filter, display, or format existing data

### Frontend API Routes

The frontend exposes search APIs that use Prisma to query the database:

```
/api/items?search=X&limit=20&page=0&rarity=X&job=X&type=X
/api/npcs?search=X&limit=20&page=0
/api/maps?search=X&limit=20&page=0
/api/quests?search=X&limit=20&page=0
/api/trophies?search=X&limit=20&page=0
```

**View Tracking:** POST endpoints increment `visit_count` for analytics (rate-limited)

## Development & Build

**Setup:**
```bash
# 1. Place game files in Maple2Storage/Resources/
#    - Xml.m2d
#    - Xml.m2h
#    - itemWebfinder.xml

# 2. Configure database connection (.env)
DB_IP=localhost
DB_PORT=3306
DB_USER=root
DB_PASSWORD=root
DB_NAME=maple2_codex

# 3. Build solution
dotnet build Maple2Codex.sln

# 4. Run parser
dotnet run --project GameParser/GameParser.csproj
```

**Parser Execution:**
1. Creates database if it doesn't exist
2. Prompts for confirmation before dropping/recreating each table
3. Runs all 7 parsers sequentially
4. Reports parsing results

**Build Tool:** Visual Studio 2022 or dotnet CLI

## Data Update Workflow

When MapleStory 2 receives an update with new content:

1. Extract new game files from game client
2. Place updated `Xml.m2d` and `Xml.m2h` in `Maple2Storage/Resources/`
3. Run GameParser to parse new content
4. GameParser populates database
5. Frontend automatically uses new data via Prisma (may need to regenerate Prisma client)

## Key Files Reference

**Parser Orchestration:**
- `GameParser/Program.cs` - Entry point, runs all parsers in sequence
- `GameParser/QueryManager.cs` - Database configuration and connection

**Parsers:**
- `GameParser/Parsers/ItemParser.cs` - Item definitions and stats
- `GameParser/Parsers/NpcParser.cs` - NPC data
- `GameParser/Parsers/MapNameParser.cs` - Map definitions
- `GameParser/Parsers/QuestParser.cs` - Quest data

**Game Data Definitions:**
- `GameParser/SQL/*.sql` - Database schema
- `Maple2Storage/Types/*.cs` - Domain models
- `Maple2Storage/Enums/*.cs` - Game enumerations

**Configuration:**
- `.env` - Database connection parameters
- `Maple2Codex.sln` - Visual Studio solution file

## When to Modify Backend Parser

- New MapleStory 2 game content (items, NPCs, quests, etc.)
- Changing how raw data is transformed before storage
- Adding new fields/columns to database
- Fixing bugs in data parsing or calculation
- Updating schema when game data structure changes

## When to Contact Frontend Team

- Issues with how frontend displays parsed data
- New search filters or sorting options needed
- UI bugs or feature requests for the handbook
- Performance issues in frontend API routes

## Notes for Frontend Developers

- **Do not** expect new columns in the database from Prisma schema changes
- **Run GameParser** after extracting updated game files
- **Regenerate Prisma client** (`pnpm exec prisma generate`) after GameParser updates
- Frontend API routes use raw SQL for complex searches, not just Prisma queries
- Database is treated as the source of truth; frontend is read-only
