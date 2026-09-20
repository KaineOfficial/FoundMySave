# FoundMySave - RuneScape: Dragonwilds

*[Version française](README.md)*

Finds your Dragonwilds worlds on your PC, **including Game Pass ones**, and gets them
ready for a dedicated server.

On Steam your worlds are plain `.sav` files sitting in a normal folder. On Game Pass
it is a different story: they are **compressed**, **renamed to unreadable
identifiers**, **without any extension**, inside a folder that **Windows Search does
not index**. Four obstacles that together make sure no ordinary search ever finds
anything, no matter what you type.

FoundMySave finds them anyway, by identifying files from their contents rather than
their names.

## What it does

- Detects **Steam** and **Game Pass** installations
- Recognises worlds by their internal signature, not by their file name
- **Decompresses** Game Pass worlds
- Reads the **world name** and the **save date** from inside the file
- Skips the game's own backups and keeps only the most recent version of each world
- Exports under the correct name, ready to use
- Prepares a complete folder for a **dedicated server**, with step by step instructions
- Optionally keeps a timestamped copy, handy before a game update
- French and English interface, switchable at any time

Your original files are **never modified**. The tool only reads and copies.

## Install

Download `FoundMySave.exe` from the
[Releases](https://github.com/KaineOfficial/FoundMySave/releases) page and run it.

Nothing to install, no dependency, no administrator rights needed.

> Windows may show a "Windows protected your PC" warning for a program few people have
> downloaded yet. Click **More info** then **Run anyway**. All of this tool's code is
> readable right here.

## Where the saves live

| Installation | Location | Format |
|---|---|---|
| Steam | `%LOCALAPPDATA%\RSDragonwilds\Saved\SaveGames\` | plain `.sav`, named after the world |
| Game Pass | `%LOCALAPPDATA%\Packages\JagexLimited.Dominion_*\SystemAppData\wgs\` | compressed, identifier names, no extension |

The `Packages` folder is not indexed by Windows Search. That is the main reason Game
Pass players never find their files.

## Save format

What follows was established by looking at real files, and it is what the tool relies
on.

**A world** starts with the four bytes `53 41 56 45`, the letters `SAVE`. Then come an
`INFO` block, the save date as plain ISO text, and the table of world field names:
`WorldName`, `WorldMapName`, `FriendlyFire`, `SurvivalDifficulty`, `HardcoreState`,
`TimeOfSave`, `SessionPrivacy`, `SessionPasswd`, `CrossplayEnabled`, `WorldOwnerId`.

**The world name** is the last readable string before the `L_World` marker. The field
table cannot be used to read it: it is a list of field names, where `WorldName` is
followed by `WorldMapName` rather than by a value.

**A character** is a JSON document starting with `{"Version": 83,` and containing a
`meta_data` block.

**On Game Pass**, the file is preceded by a 12 byte header, followed by a **zlib**
stream (`78 9C`). The tool looks for that signature within the first 64 bytes instead
of assuming a fixed position, so a format change will not break it outright.

**What is not decoded**: the values of the world settings. Their names are readable,
but the values live elsewhere and refer back to the table by index. Decoding them
would mean reverse engineering the whole serialisation format, and would break with
every game update. The tool therefore does not claim to read them.

## Importing a world onto a dedicated server

The **Export for a server** button prepares a folder containing the correctly named
`.sav` and a `README.txt` with the instructions. In short:

1. Stop the server
2. Empty its `SaveGames` folder, without deleting the folder itself
3. Copy the `.sav` into it, **without renaming it**
4. Set `DefaultWorldName` in `DedicatedServer.ini` to the world name
5. Start it again

Two mistakes that cost dearly:

- **Never rename a `.sav`.** The file name is the world name; changing it loses your
  progress.
- **Never edit `DedicatedServer.ini` while the server is running.** Those changes are
  lost on restart.

Worth knowing: world settings (difficulty, keep inventory on death, friendly fire) are
stored **inside the `.sav`**, not in the server configuration. Importing your own
world is therefore the only way to keep them on a dedicated server.

## Building it yourself

Requires the .NET 8 SDK.

```bash
git clone https://github.com/KaineOfficial/FoundMySave.git
cd FoundMySave
dotnet build -c Release
dotnet run --project tests/FoundMySave.Check   # checks the engine against your own saves
dotnet publish src/FoundMySave -c Release -o publish
```

`FoundMySave.Check` is a console tool that walks through your saves, prints what it
recognises and verifies that everything is consistent. It includes a self test that
builds a file in the Game Pass format and checks it comes back identical, which
validates that part without owning the Game Pass version. It also checks that both
languages are complete and that their placeholders match.

The icon and logo are generated by `tools/make-icon.ps1`, so no binary asset is
committed by hand.

## Code layout

```
src/FoundMySave.Core/     reading, decompression, detection, translations (no UI, testable)
src/FoundMySave/          WPF interface
tests/FoundMySave.Check/  console verification + self tests
tools/make-icon.ps1       generates the icon and the logo
```

## Licence

MIT. See [LICENSE](LICENSE).

Independent project, not affiliated with Jagex. RuneScape and Dragonwilds belong to
their respective owners.
