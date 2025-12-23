# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2025-Dec-23

### Added

- Added a configuration file under `BepInEx/config/FleesDedicatedServer.cfg` as an additional option to specify dedicated server settings
- Added an option to schedule a server restart after a specified amount of time
  - The automatic restart can be configured to go through only when the server player count drops below a certain percentage of the maximum player count
  - The time interval is specified in the `[XXd][XXh][XXm][XXs]` format, such as `4h` for 4 hours, `1d6h30m` for 1 day, 6 hours and 30 minutes, etc.
  - The minimum time interval is 30 minutes
- Added `/restart` to the server console - this will restart the server after 20 seconds have elapsed
- Added `/cancelrt` - this command and `/cancelsd` will cancel a pending server shutdown or restart
- The mod will now generate `start_dedicated_server.cmd` on Windows and `start_dedicated_server.sh` on Linux with Proton
  - These scripts can be used to launch a dedicated server directly instead of having to specify launch options through Steam
  - You will need to first launch the game normally using this mod for those scripts to appear or be updated
- The mod will now generate `steam_appid.txt` to allow to boot the game without going through the Steam Client

### Changed

- In-game audio is now muted while running with `-batchmode -nographics`
- The host player is now hidden from view instead of being teleported in an off-screen area

## [0.0.7] - 2025-Nov-26

**Version bump without significant changes**

## [0.0.6] - 2025-Apr-09

### Fixed

- Updated mod code to work with the new update of ATLYSS.
- Cleaned up console output.

## [0.0.5] - 2025-Apr-09

### Fixed

- Removed deprecated dependency from mod (thanks for the heads up Marioalexsan).

## [0.0.4] - 2025-Apr-09

### Fixed

- Updated mod code to work with the new update of ATLYSS.

## [0.0.3] - 2025-Apr-09

** Undocumented update **

## [0.0.2] - 2025-Apr-09

### Added

- `-hostsave` argument to specify which save slot the server host character uses (range: 0–6).
  - If not provided or out of range, defaults to save slot 0.
  - Warning logged if an invalid slot is given.

### Changed

- You must have a valid character in the selected save slot to start the server.

## [0.0.1] - 2025-Apr-09

**Initial mod release**