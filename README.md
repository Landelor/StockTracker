# StockTracker

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that tracks
items you craft and sell, and tells you which ones have sold out and need to be recrafted.

![icon](images/icon.png)

## What it does

- Maintain one or more **named lists** of items you craft and sell, each with a target
  stock quantity.
- StockTracker watches each tracked item's **total market listing quantity across all of
  your retainers combined** (via Allagan Tools) and automatically builds a **"Needs
  Recraft"** list whenever an item's total stock drops below its target — clearing the
  entry again once you restock.
- Optionally adds a right-click entry to the **Crafting Log** to add the selected recipe
  straight to a list (toggle in Settings).
- **Export** the Needs Recraft list directly to [Artisan's](https://github.com/Askareth/Artisan)
  crafting list format via the clipboard.

## Requirements

- [Dalamud](https://github.com/goatcorp/Dalamud) (FFXIV plugin framework)
- [Allagan Tools (InventoryTools)](https://github.com/Critical-Impact/InventoryTools) —
  required for retainer market listing data
- [Artisan](https://github.com/Askareth/Artisan) — optional, only needed if you want to
  use the "Export to Artisan" button

## Installation

StockTracker is distributed via a custom Dalamud plugin repository.

1. In-game, open the Dalamud Settings (`/xlsettings`) → **Experimental** tab, and add the
   following URL under **Custom Plugin Repositories**:

   ```
   https://raw.githubusercontent.com/Landelor/StockTracker/master/pluginmaster.json
   ```
2. Open the Plugin Installer (`/xlplugins`) and install **StockTracker** from the
   "Available" list.

## Usage

Open the plugin window with:

```
/stocktracker
```
or the shorthand:
```
/st
```

From the window you can:
- **Dashboard** — overview of tracked lists and their status.
- **Tracked Items** — add/remove items and set target stock quantities per list.
- **Needs Recraft** — items currently below their target stock; export the list to
  Artisan's crafting list format via the clipboard.
- **Settings** — configure options, including enabling the Crafting Log right-click
  "add to list" context menu entry.

## Building from source

This is a standard Dalamud plugin project.

```bash
dotnet build StockTracker.sln
```

Building requires a local Dalamud install (`DALAMUD_HOME` pointing at your
`~/.xlcore/dalamud/Hooks/<version>` directory, or the equivalent on your platform) since
the project targets `Dalamud.NET.Sdk`.

## License

[AGPL-3.0-or-later](https://www.gnu.org/licenses/agpl-3.0.html)
