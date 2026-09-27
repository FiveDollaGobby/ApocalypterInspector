# Apocalypter Inspector

A debug menu for [Apocalypter](https://store.steampowered.com/search/?term=Apocalypter) that I made while trying to figure out how the game works.

Pretty much everything in this game (health, fuel, movement, car parts) is driven by PlayMaker FSMs. The only way to find out what they're called is to look while the game is running, so I made a menu that does that. Then I kept adding cheats because it was fun.

It's a BepInEx mod and it doesn't change any of the game's files. If you delete the two DLLs, the game is exactly how it was.

## What's in it

Press <kbd>Insert</kbd> in game to open the menu. You can drag it around by the title bar.

### Player

- set health, or lock it so nothing can take it down
- speed, gravity and sprint, all lockable (the game resets one-off changes, so locking just writes the value again every tick)
- lock hunger / thirst / fatigue, *if* the game's variable names contain those words
- godmode and jump values
- noclip. <kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> to move, <kbd>Space</kbd> to go up, <kbd>Ctrl</kbd> to go down
- 3 quick teleport slots, plus a list of named places that gets saved to disk. You can also type `x y z` coords and go straight there
- up to 3 saved loadouts with speed, gravity, sprint, godmode and time scale. Hit Apply and it all goes back on, locks included
- fill or empty gas cans

### World

- time scale
- every liquid container and every part with a condition value that's loaded, with empty/fill buttons for each
- car stuff: repair it, fill it up, teleport to it, or pull it over to you
- fog, plus any weather FSMs that are in the scene

### Spawn

- lists every item it's seen loaded this session, grouped by type
- spawns a copy right in front of you
- kit box: type a bunch of item ids separated by commas and it spawns them all

### Inspect

This is the tab I actually built the mod for.

- search any FSM in the scene
- edit its floats, ints, bools and strings live
- force it into a state or send it an event
- see what components are on the object

### Hotkeys

These two work without the menu open:

| Key | What it does |
| --- | --- |
| <kbd>F8</kbd> | sets your health to 25 and gas cans to 1. first thing I ever tested with, never took it out |
| <kbd>F9</kbd> | dumps every live FSM and all its variables to a text file |

## Installing

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) into your Apocalypter folder. I'm on 5.4.23.5 x64. Run the game once so it makes its folders, then close it.
2. Grab the zip from [Releases](../../releases).
3. Drop the two DLLs in so it ends up looking like this:

```
Apocalypter/
└── BepInEx/
    ├── patchers/
    │   └── ApocalypterInspectorPatcher.dll
    └── plugins/
        └── ApocalypterInspector.dll
```

4. Start the game, load a save and press <kbd>Insert</kbd>.

> [!IMPORTANT]
> You need **both** DLLs. With only the plugin it loads and then nothing happens. There's a bit further down on why.

If Windows says the file is in use, the game's still open. Close it and try again.

## Files it makes

All of these go in your `BepInEx` folder:

- `ApocalypterInspector-fsm-<scene>.txt` is the FSM dump. It writes one automatically once you're in the world, and another every time you press F9
- `ApocalypterInspector-places.txt` has your saved places
- `ApocalypterInspector-loadouts.txt` has your saved loadouts
- `ApocalypterInspector-patcher.txt` is the log. **Check this first if something's broken.** BepInEx's own log misses some of it

Each entry in a dump is laid out roughly like this:

```
OBJECT: Player
SCENE: Game
FSM: Health
STATE: Idle
  [FLOAT] Health = 100
```

## Why is there a patcher?

I lost a lot of time on this one. In Apocalypter a normal BepInEx plugin never gets its `Update` called, and Harmony patches install fine but never actually run (the log says `Supports SRE: False`). So the normal way of writing a mod just does nothing, and it doesn't give you an error either.

The patcher gets around that. It runs before the game loads, and in memory it adds a tiny call at the start of the game's own `Update` and `OnGUI` methods. That call is what runs the menu. Nothing gets written to disk.

## Stuff to know

- You can only spawn things the game has already loaded this session. If you haven't been near an item, it won't be in the list. I tried to avoid a big list of guessed ids that don't work.
- If the needs buttons aren't showing up, the game probably calls those variables something else. Look them up in Inspect.
- Car tools use whatever the game says you're sitting in. On foot they grab the nearest thing with a condition value, which could be a random part on the ground.
- It's a dev tool, so some buttons will do weird things. **Save before you mess with godmode or force states.**

## Building it yourself

You'll need Visual Studio 2022 with the .NET Framework 4.7.2 targeting pack.

```
git clone https://github.com/FiveDollaGobby/ApocalypterInspector.git
```

1. Open `ApocalypterInspector.slnx`
2. Both `.csproj` files expect the game at `D:\Steam\steamapps\common\Apocalypter`. If yours is somewhere else, fix the `HintPath`s
3. Build Debug
4. Copy the DLLs over:
   - `ApocalypterInspector\bin\Debug\ApocalypterInspector.dll` → `BepInEx\plugins`
   - `ApocalypterInspectorPatcher\bin\Debug\ApocalypterInspectorPatcher.dll` → `BepInEx\patchers`

The game DLLs are referenced with Copy Local off, so none of the game's files end up in the build or the repo.

## License

MIT, do whatever you want with it. If you end up making something with the names you find, let me know. I'd like to see it.
