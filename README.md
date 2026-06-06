# EntBossHP

A Counter-Strike 2 plugin for displaying boss health information to players during gameplay.

## Overview

EntBossHP tracks and displays the health of configured boss entities in CS2 maps through center screen messages. The plugin supports breakable entities, math counters, and HP bar systems.

## Features

- **Multiple Boss Types Support**:
  - Breakable entities (`func_physbox`, `func_breakable`, `prop_dynamic`)
  - Math counter entities
  - HP bar systems with iterator and backup counters

- **Real-time HP Display**:
  - Boss HP shown to the attacking player
  - Visual HP bar with filled/empty blocks
  - Optional HP offset per boss config

- **Segmented Boss Support**:
  - Bosses with multiple health segments
  - Remaining segment display

- **Auto-configuration**:
  - Automatically detects and adds new boss entities
  - Per-map configuration files
  - Dynamic boss discovery during gameplay

## Installation

1. Install CounterStrikeSharp on your CS2 server.
2. Place the plugin files in your CounterStrikeSharp plugins directory.
3. Restart the server or load the plugin.

## Configuration

### Per-Map Configuration

The plugin creates configuration files for each map in JSON format:

```text
configs/plugins/EntBossHP/{mapname}.jsonc
```

### ConVars

- `css_bosshp_enablebhud` - Enable boss HP center HUD output (default: true)

### Config File Structure

```json
{
  "Breakable": [
    {
      "name": "Boss Name",
      "enabled": true,
      "breakable": "entity_name",
      "health_segment_counter": "counter_name",
      "health_segment_counter_mode": 1,
      "hp_offset": 0
    }
  ],
  "MathCounter": [
    {
      "name": "Math Boss",
      "enabled": true,
      "mathcounter": "counter_name",
      "mathcounter_mode": 1,
      "health_segment_counter": "segment_counter",
      "health_segment_counter_mode": 1,
      "hp_offset": 0
    }
  ],
  "HPBar": [
    {
      "name": "HP Bar Boss",
      "enabled": true,
      "mathcounter": "main_counter",
      "mathcounter_mode": 1,
      "iterator": "iterator_name",
      "iterator_mode": 1,
      "backup": "backup_counter",
      "hp_offset": 0
    }
  ]
}
```

## Boss Types

### 1. Breakable Boss

- Monitors breakable entities like `func_physbox`, `func_breakable`, and `prop_dynamic`
- Tracks entity health changes
- Supports segment counters for multi-phase bosses

### 2. Math Counter Boss

- Uses `math_counter` entities to track boss HP
- Supports different counter modes:
  - Mode 1: Direct counter value
  - Mode 2: Inverted counter value (max - current)
- Auto-detects counter min/max values

### 3. HP Bar Boss

- Advanced system using multiple math counters
- Main counter for primary HP
- Iterator counter for additional tracking
- Backup counter for fallback values

## Commands

- `boss_list` - Display configured bosses and their settings (console only)

## Display Features

- **HP Bar Visualization**: filled/empty center HUD bar
- **Segmented Boss Display**: shows remaining segments
- **Auto-hide**: bosses are hidden when HP reaches 0

## Auto-Detection

The plugin automatically detects new boss entities during gameplay:

- Entities with HP > 10 are automatically added as enabled
- Entities with HP <= 10 are added as disabled
- Configuration is saved automatically

## Technical Details

- **Version**: 2.1.0
- **Author**: Oylsister, Credits to Kxrnl, DarkerZ [RUS] / modified by Tsukasa
- **Target Framework**: .NET 10
- **Dependencies**: CounterStrikeSharp.API 1.0.369, Newtonsoft.Json 13.0.3
- **Supported Entities**: math_counter, func_physbox_multiplayer, func_physbox, func_breakable, prop_dynamic

## Troubleshooting

1. **Boss not appearing**: Check if the entity name matches the configuration.
2. **HP not updating**: Verify the entity is being damaged and outputs are firing.
3. **Configuration issues**: Check the JSON syntax in your map config file.

## License

This plugin is provided as-is for Counter-Strike 2 servers using CounterStrikeSharp.
