# GambleUp

A quality-of-life / enhancement mod for **Gamble With Your Friends** (TENSTACK), built on
[MelonLoader](https://github.com/LavaGang/MelonLoader).

Right now GambleUp gives you one thing: **a row of slot machines you can mash to earn money when
you are broke.** No bet is taken, the reels never spin, and every press pays out a flat amount.
It is meant as a bridge until the proper systems land.

## Roadmap

| Status | Feature | Notes |
| --- | --- | --- |
| **Done** | Slot machine money glitch | Place any number of slot machines at fixed world coordinates; each press grants a flat balance payout. |
| Planned | Independent economy system | Decouple the player's wallet from the casino's ledger so balances, payouts and losses are tracked and settled by GambleUp itself instead of the stock `MoneyManager` flow. |
| Planned | Player loan system | Let players take out a loan against their balance / items, accrue interest over in-game days, and settle or default on it. Will build on top of the independent economy. |

## Requirements

- Windows 10 / 11, 64-bit.
- A legitimate copy of **Gamble With Your Friends** (Steam).
- **No extra installer needed.** MelonLoader is already shipped with the game. You only need to drop
  one DLL into the `Mods` folder.

## Installation (for normal users)

You need exactly one file: `GambleUp.dll`.

### 1. Find your game folder

Open Steam, right-click **Gamble With Your Friends** -> **Manage** -> **Browse local files**.
Steam will open the game folder. It looks like this:

```
...\steamapps\common\Gamble With Your Friends\
    Gamble With Your Friends.exe
    Gamble With Your Friends_Data\
    MelonLoader\        <- already there, you do not need to install anything
    Mods\
```

Remember this path, you will need it in step 3.

### 2. Get `GambleUp.dll`

Download it from the
[Releases](https://github.com/gushen610140/GambleUp/releases) page (or build it yourself, see
[Building from source](#building-from-source)).

### 3. Install it

1. Open the `Mods` folder inside the game folder. If it does not exist, create it yourself: right
   click in the game folder -> **New** -> **Folder** -> name it exactly `Mods`.
2. Copy `GambleUp.dll` into that `Mods` folder.

The result must be:

```
<game folder>\Mods\GambleUp.dll
```

> **Important:** the DLL must go directly into `Mods`. Do **not** put it in a subfolder unless you
> also add a `manifest.json` inside that subfolder.

### 4. Start the game the normal way

Launch through Steam as you always do. A MelonLoader console window appears before the game, and
GambleUp prints a banner confirming it loaded:

```
================================================================
  GambleUp probe online
================================================================
```

That is all the installation there is.

## Configuration (optional)

GambleUp reads an optional text config every time it spawns, so you can change things **without
rebuilding or reinstalling**.

Create the file:

```
%LOCALAPPDATA%\Low\TENSTACK\Gamble With Your Friends\GambleUp_cheat.txt
```

(`%LOCALAPPDATA%` is usually `C:\Users\<you>\AppData\Local`. You can also press
`Win + R` and paste `%LOCALAPPDATA%\Low\TENSTACK\Gamble With Your Friends` to jump straight there.)

Contents:

```ini
# GambleUp cheat slot config
# "key = value", "#" starts a comment.

enabled = true

# Flat balance added to the interacting player each time a machine's button is pressed.
reward = 5

# One machine per line. Yaw follows your heading, flipped 180 degrees.
pos = -6, 0, 70
pos = -3, 0, 70
pos = 3, 0, 70
pos = 6, 0, 70
```

| Key | Meaning |
| --- | --- |
| `enabled` | `true` / `false`. Set to `false` to stop GambleUp from creating anything. |
| `reward` | Balance added per button press. Fractional values are fine, e.g. `reward = 2.5`. |
| `pos` | World position `x, y, z` of one machine. Repeat the line for more machines. You can also put several on one line separated by `;`. Delete every `pos` line to place a single machine 2.5 m in front of your character instead. |

Changes are picked up the next time a casino floor loads, so you can edit the file while the game is
closed, start a run, and see the result.

## Multiplayer

**Only the host needs to install GambleUp.** Clients do not.

The game runs on a host-authoritative [Mirror](https://mirror-networking.gitbook.io/docs/) networking
model:

- Machines are created with `NetworkServer.Spawn`, which only does anything where
  `NetworkServer.active` is true, i.e. on the host. On a client the mod skips itself entirely.
- Once spawned, Mirror replicates the machines to every client automatically, so everyone sees them.
- `MoneyManager` keeps `balance` and `ticketBalance` as SyncVars, so a payout applied on the host is
  replicated to all players. Remote teammates can press the machines and get paid too.
- The only exception is a **dedicated server** setup, where the DLL has to sit on the server machine
  rather than on a player's client.

## Log file

GambleUp writes a detailed log next to the game:

```
%LOCALAPPDATA%\Low\TENSTACK\Gamble With Your Friends\GambleUp_probe.log
```

It contains scene entry events, machine spawn coordinates, and a live two-second broadcast of your
position, balance and tickets. If something looks wrong, this is the first file to look at (or to
attach to a bug report).

To silence the running commentary, create an empty file named `GambleUp_probe.paused` in the same
folder. Delete it to resume. Milestone banners are always printed regardless.

## Building from source

Requirements: the [.NET 9 SDK](https://dotnet.microsoft.com/download) (or any SDK able to target
`netstandard2.1`) and a local copy of the game.

```powershell
git clone https://github.com/gushen610140/GambleUp.git
cd GambleUp

# Point the build at your game folder, either per-build...
dotnet build -c Release -p:GamePath="D:\Steam\steamapps\common\Gamble With Your Friends"

# ...or once, for the whole machine
$env:GAMBLEUP_GAME_PATH = "D:\Steam\steamapps\common\Gamble With Your Friends"
dotnet build -c Release
```

The project references the game's own `Managed` assemblies (`Assembly-CSharp.dll`, `Mirror.dll`,
`UnityEngine.*`, `MelonLoader.dll`, `0Harmony.dll`) by `HintPath`, so the game must be installed
before the build will succeed.

The build automatically copies the resulting `GambleUp.dll` into `<game folder>\Mods`, so
"build and install" is a single step.

## Project layout

```
GambleUp.sln
GambleUp/
  Core.cs                  MelonMod entry point, Harmony-free bootstrap, scene hooks
  Probe.cs                 logging, banners, live player/state broadcast
  Cheat.cs                 machine spawning, interact rewiring, payout
  GambleUp.csproj          netstandard2.1, x64, references the game's assemblies
  Directory.Build.props    resolves $(GamePath)
```

## How the machines work

For anyone curious enough to read the source:

1. The casino floor is detected by scene name, `CasinoScene`. Every other scene is ignored.
2. A prefab is obtained by walking all `GameStamp` components in the scene and reading their private
   `gamePrefab` field, picking the first prefab that carries a `Slots` component. This is the same
   prefab the game itself uses in `GameStamp.SpawnGame`.
   * Cloning a live machine instead does not work: `NetworkIdentity.hasSpawned` is a private field,
     so `Object.Instantiate` on a scene object copies `hasSpawned = true`, and
     `NetworkIdentity.Awake()` immediately destroys the copy with
     *"has already spawned. Don't call Instantiate for NetworkIdentities that were in the scene"*.
     There is an inactive-clone fallback in the code for the case where no prefab can be found.
3. The prefab is instantiated, its `NetworkIdentity` is spawned with `NetworkServer.Spawn`, and
   `GameBase.NetworkcasinoLevel` is set from the floor index, exactly like `GameStamp` does.
4. The machine's `InteractableEventTrigger.serverOnInteractEvent` UnityEvent is replaced with a
   fresh one. The original serialized listener that called `GameBase.TryStartGame` is dropped, so no
   bet is taken and `StartGame` / `Payout` never run.
5. The new listener calls `MoneyManager.TryChangeBalance(reward, profile, ChangeType.GameResult)`.

Relevant decompiled types, for reference:

| Type | Role |
| --- | --- |
| `Slots : GameBase : NetworkBehaviour` | the slot machine |
| `GameBase.TryStartGame(PlayerInteract)` | the original button behaviour that gets bypassed |
| `GameStamp.gamePrefab` / `GameStamp.SpawnGame` | the game's own machine-spawning path |
| `InteractableEventTrigger` | holds the `serverOnInteractEvent` UnityEvent that gets rewired |
| `MoneyManager.TryChangeBalance` | the payout entry point |
| `CasinoFloor.floorIndex` | floor number written into `casinoLevel` |

## Legal

GambleUp is an unofficial third-party modification. It is not affiliated with, endorsed by, or
supported by TENSTACK.

Use it in single player, or in multiplayer only with people who have explicitly agreed to it.
Modifying a game you do not own may violate the terms of service of the platform you bought it on,
and using it in competitive or public games is a good way to get yourself removed from them. That
risk is yours to take.

The repository intentionally contains **no** game assets or decompiled game source. Those are
extracted locally at development time and are excluded by `.gitignore`.
