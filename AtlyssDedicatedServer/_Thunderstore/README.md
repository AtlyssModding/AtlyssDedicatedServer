# 🖧 ATLYSS Dedicated Server Plugin

[**Originally made by FleeTime.**](https://github.com/Flee-Time/AtlyssDedicatedServer)

This BepInEx plugin adds **headless dedicated server support** to **ATLYSS**, enabling you to run the game in a terminal as a dedicated server — no graphics or UI needed.

> ⚠️ This is for hosting servers only. It will disable itself when the game is being launched normally.

---

## ✅ Mod Compatibility

- Fully compatible with other BepInEx mods that modify or enhance hosting behavior.  
  This includes:
  - **Chat formatting mods** (e.g., colored messages)
  - **Uncapped party size** or connection limit mods
  - **Custom stat or balance changes**
  - **Lobby tweak plugins**

As long as the mod loads under BepInEx and applies during host/server initialization, it will work seamlessly with this dedicated server plugin.

## 🔍 Known Issues

- **Connecting to the dedicated server using the same Steam account is not possible.**  
  - While you may be able to launch a new game instance in parallel with the dedicated server, 
    it is not possible to connect to it using the same account as the one hosting the dedicated server.
  - As such, it's best to host the dedicated server using a separate Steam account with a copy of ATLYSS on it.

## ✅ Requirements

- **BepInEx 5.x**
- **ATLYSS 112025.a4**
- Must launch the game with the following arguments:

```sh
-batchmode -nographics -server
```

---

## 🛠️ Launch Syntax

```sh
ATLYSS.exe -batchmode -nographics -server [options...]
```

You **must** include at least`-server` before your own custom arguments for the mod to activate.

Specifying `-batchmode -nographics` is optional, but desirable to enable proper headless support.

The mod also generates `start_dedicated_server.cmd` on Windows and `start_dedicated_server.sh` on Linux that do it for you, however you might need to have the Steam client active for it to work correctly.

---

## 🔧 Available Arguments

| Argument                | Config Setting                        | Description                                                       | Values                                      | Default                     |
|-------------------------|---------------------------------------|-------------------------------------------------------------------|---------------------------------------------|-----------------------------|
| `-server`               |                                       | Enables dedicated server mode                                     |                                             |                             |
| `-hostsave N`           | CharacterSlotToUse                    | Selects the save slot to use for the host character               | `int`, min 0, max 104                       | `0`                         |
| `-name "MyServer"`      |                                       | Sets the server name (max 20 characters)                          | `string`, max 20                            | `ATLYSS Server`             |
| `-password "1234"`      | ServerPassword                        | Sets a join password                                              | `string`                                    | <empty>                     |
| `-motd "Message"`       | ServerMOTD                            | Sets a Message of the Day                                         | `string`                                    | `Welcome to ATLYSS Server!` |
| `-maxplayers N`         | ServerMaxPlayers                      | Max number of players                                             | `int`, min 2, max 250                       | `16`                        |
| `-public`               | ServerType (PUBLIC)                   | Makes server public                                               |                                             | default type                |
| `-private`              | ServerType (PRIVATE)                  | Makes server private                                              |                                             |                             |
| `-friends`              | ServerType (FRIENDS)                  | Makes server visible only to Steam friends                        |                                             |                             |
| `-pve`                  | ServerTag (GAIA)                      | Lobby focus: PvE                                                  |                                             | default tag                 |
| `-pvp`                  | ServerTag (DUALOS)                    | Lobby focus: PvP                                                  |                                             |                             |
| `-social`               | ServerTag (NOTH)                      | Lobby focus: Social                                               |                                             |                             |
| `-rp`                   | ServerTag (LOODIA)                    | Lobby focus: RP                                                   |                                             |                             |
| `-autorestart`          | PeriodicRestartEnabled                | Enables automatic restarts                                        | `true/false`                                | `false`                     |
| `-autorestartin`        | PeriodicRestartInterval               | Specifies time between restarts (like `1d` or `6h`)               | `string`, format like `1d12h30m`, min `30m` | `6h` (6 hours)              |
| `-autorestartthreshold` | PeriodicRestartPlayerThresholdPercent | Specifies player count % below which the server will auto restart | `int`, min 0, max 100 (= % of max players)  | `100` (percent)             |

> ⚠️ The priority of server type argument is `-public`, `-private`, then `-friends` in that order.  
> ⚠️ The priority of server tag argument is  `-pve`, `-pvp`, `-social`, then `-rp` in that order.  
> ⚠️ If `-hostsave` is not specified, the server will default to using **save slot 0**.  
> ⚠️ If there is no valid character in the given slot, a new random one will be created in it.

---

## 📦 Example Usages

### Start a public PvE server with 16 players:

```sh
ATLYSS.exe -batchmode -nographics -server -name "MyServer" -motd "Welcome!" -maxplayers 16 -public -pve
```

### Start a private PvP server with a password:

```sh
ATLYSS.exe -batchmode -nographics -server -name "Private Warzone" -password "hunter2" -maxplayers 10 -private -pvp
```

### Start a friends-only social server:

```sh
ATLYSS.exe -batchmode -nographics -server -name "CozyHub" -motd "Grab tea and chill." -friends -social
```

### Start a public PvE server with 64 players that restarts every 36 hours when the server is less than 25% full:

```sh
ATLYSS.exe -batchmode -nographics -server -name "Poontastic" -motd "Welcome!" -maxplayers 64 -public -pve -autorestart -autorestartin 36h -autorestartthreshold 25
```

---

## 🧠 Behavior Notes

- The host player is fully hidden from view.
- Console input works for typing commands directly
- Server can be force restarted with `/restart`, and force shutdown with `/shutdown` in the console window
- Server shutdowns and restarts (including the automatic restart) can be cancelled with `/cancelrt` or `/cancelsd` in the console window
- In-game chat is mirrored to the terminal (color tags handled)
- Audio is disabled via `AudioListener.volume = 0`
- Server config and startup logs are shown in the console

---

## 🧪 Troubleshooting

- ❌ **Nothing happens?** Make sure you're running with `-batchmode -nographics -server`
- ❌ **Can't see the terminal?** Check if console logging is enabled in `BepInEx/config/BepInEx.cfg`, or launch it through a CMD / bash shell
