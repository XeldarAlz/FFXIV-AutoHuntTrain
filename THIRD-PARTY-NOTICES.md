# Third-party notices

Auto Hunt Train ships spawn points derived from the MIT licensed dataset below. The generator in `tools/HuntSpawns` reads it at the pinned commit listed here and writes the spawn points hunt marks share per zone to `AutoHuntTrain/Core/Marks/Data/HuntSpawnTable.g.cs`. Only the derived coordinates are shipped; the dataset itself is not redistributed.

## Hunt Helper

- Source: https://github.com/imaginary-png/HuntHelper, file `HuntHelper/Data/SpawnPointData.json` at commit `aadf7cb1945b05544b8d0340042c6a38497860bf`.
- Used: the spawn points of every open-world hunt zone, each flagged with the mark ranks (B, A, S) that can appear at it. The map coordinates are converted to world X and Z; heights are not in the dataset, so the plugin snaps each point to the ground. A ride over a hunt mark visits the points of its rank in its zone, after the spot the conductor's flag points at.

```text
MIT License

Copyright (c) 2022 imaginary-png

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## HuntAlerts

- Source: HuntAlerts by Asuna, https://projects.gamba.pro/Asuna/huntalerts at commit `69ec1140add0e5ce97553785c8ad04d04ac68224`, licensed AGPL-3.0-or-later like this plugin.
- HuntAlerts is also the plugin that relays the train announcements this plugin reads over IPC; the code below was ported from it and rewritten into this codebase's style, not copied verbatim.
- Ported:
  - `HuntAlerts/Helpers/Utilities.cs`, `OpenPartyFinder`: opening the Party Finder and switching it to the Hunts category, in `AutoHuntTrain/Core/Game/PartyFinderOpener.cs`.
  - `HuntAlerts/Helpers/MapManager.cs`, `OpenMapWithMarker`: opening a zone's map with a flag through a map link payload, in `AutoHuntTrain/Core/Game/MapFlag.cs`.
  - `HuntAlerts/Helpers/ArrowWaypoint.cs`: the navigation waypoint and its 30 minute lifetime, in `AutoHuntTrain/Core/Travel/NavWaypoint.cs`.
  - `HuntAlerts/Windows/WorldArrowWindow.cs`: the on-screen arrow overlay, its camera yaw calibration, the arrow and arrival shapes and right-click to clear, in `AutoHuntTrain/Windows/NavArrowWindow.cs`.
  - `HuntAlerts/Helpers/RelayChannels.cs`: the relay channel list and the one-line relay text with `<flag>` in place of coordinates, in `AutoHuntTrain/Core/Feed/TrainRelay.cs`.
