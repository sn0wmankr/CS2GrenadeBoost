# 🚀 CS2 Grenade Boost Plugin

<div align="center">

A Counter-Strike 2 plugin that allows players to boost themselves by throwing HE grenades on the ground

[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-v1.0.345%2B-blue)](https://github.com/roflmuffin/CounterStrikeSharp)

</div>

## 🎯 Features

- 🎮 **Grenade Boost Mechanics** - Players can boost themselves by throwing HE grenades
- ⚙️ **Customizable Physics** - Separate horizontal and vertical boost values
- 🎯 **Air Accuracy** - (Optional) perfect accuracy while airborne (AWP)
- 💥 **Damage Control** - Toggle HE grenade damage and fall damage
- 🎁 **Auto-Give Grenades** - Automatically give HE grenades at round start
- 📊 **Max Grenade Limit** - Set maximum HE grenades per player

## 📦 Requirements

- **[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)** v1.0.345 or newer
- **.NET 8.0 SDK** (for building from source)
- **Compatible with other plugins** including WeaponPaints

## 🔧 Installation

### Quick Install

1. **Install CounterStrikeSharp**
   - Download from [CounterStrikeSharp Releases](https://github.com/roflmuffin/CounterStrikeSharp/releases)
   - Extract to your CS2 server directory

2. **Install Plugin**
   - Download `GrenadeBoost.dll` from releases
   - Place in: `addons/counterstrikesharp/plugins/GrenadeBoost/`

3. **Load Plugin**
   - Restart server or use: `css_plugins load GrenadeBoost`
   - Config auto-generates at: `addons/counterstrikesharp/configs/plugins/GrenadeBoost/GrenadeBoost.json`

## ⚙️ Configuration

Config file auto-generates on first load. If deleted, it will be recreated automatically.

**Location:**
```
addons/counterstrikesharp/configs/plugins/GrenadeBoost/GrenadeBoost.json
```

### Full Configuration

```json
{
  "Version": 2,
  "Enabled": true,
  
  "AutoGiveHEGrenade": true,
  "MaxHEGrenades": 1,
  
  "HorizontalBoost": 800.0,
  "VerticalBoost": 400.0,
  "BoostMultiplier": 1.2,
  "MaxBoostVelocity": 3500.0,
  
  "EnableAirAccuracy": false,
  "DisableHEGrenadeDamage": false,
  "DisableFallDamage": false
}
```

### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| **General** |
| `Version` | int | `2` | Config file version (auto-managed) |
| `Enabled` | bool | `true` | Master switch for the plugin |
| **Grenade Settings** |
| `AutoGiveHEGrenade` | bool | `true` | Automatically give HE grenades at round start |
| `MaxHEGrenades` | int | `1` | Maximum HE grenades a player can hold |
| **Boost Physics** |
| `HorizontalBoost` | float | `800.0` | Horizontal boost force (forward/backward/sides) |
| `VerticalBoost` | float | `400.0` | Vertical boost force (upward) |
| `BoostMultiplier` | float | `1.2` | Overall boost strength multiplier |
| `MaxBoostVelocity` | float | `3500.0` | Maximum velocity cap to prevent overspeed |
| **Gameplay Features** |
| `EnableAirAccuracy` | bool | `false` | Perfect accuracy using `weapon_accuracy_nospread 1` |
| `DisableHEGrenadeDamage` | bool | `false` | Negate all HE grenade damage (restores health, capped at max) |
| `DisableFallDamage` | bool | `false` | Negate all fall damage using `sv_falldamage_scale 0` |

**Apply changes:** `css_plugins reload GrenadeBoost`

## 🎮 How to Use

1. **Get HE Grenades**
   - Automatically given at round start (if `AutoGiveHEGrenade` is enabled)
   - Or buy manually: `buy hegrenade`

2. **Perform Boost**
   - Throw HE grenade at the ground near you
   - Grenade explodes and propels you upward/forward
   - The closer to the explosion, the stronger the boost

3. **Tips**
   - Throw grenades straight down for maximum vertical boost
   - Throw at an angle for horizontal movement
   - Combine with strafing for directional control

## 🛠️ Building from Source

```bash
# Clone repository
git clone https://github.com/sn0wmankr/CS2GrenadeBoost
cd CS2GrenadeBoost

# Build
dotnet build -c Release

# Output: bin/Release/net8.0/GrenadeBoost.dll
```

## 🔍 How It Works

**Physics Calculation:**
```
Explosion Radius: 350 units
Distance Factor: (1 - distance/radius)
Boost Direction: Horizontal (XZ plane) + Vertical (Y axis)
Final Force: 
  - Horizontal: HorizontalBoost × DistanceFactor × BoostMultiplier
  - Vertical: VerticalBoost × DistanceFactor × BoostMultiplier
Velocity Cap: MaxBoostVelocity (prevents excessive speeds)
```

**Air Accuracy:**
- Uses `weapon_accuracy_nospread 1` ConVar (server-wide setting)
- Automatically enabled at round start when `EnableAirAccuracy` is true
- Provides perfect accuracy for all weapons in all situations
- Restored to default on plugin unload
- **Note**: This is a server-wide setting that affects all players

**Fall Damage:**
- Uses `sv_falldamage_scale` ConVar (server-wide setting)
- Set to `0` when `DisableFallDamage` is true (no fall damage)
- Set to `1` (or original value) when disabled or plugin unloads
- **Note**: This is a server-wide setting that affects all players

**HE Grenade Damage:**
- Uses **`sv_hegrenade_damage_multiplier` ConVar** (engine-level damage control)
- Set to `0.0` when `DisableHEGrenadeDamage` is true (no HE grenade damage)
- Set to `1.0` (or original value) when disabled or plugin unloads
- **Perfect blocking**: Damage calculation happens at engine level
- **Most reliable**: No complex hooks, pure ConVar approach
- **Best compatibility**: No conflicts with other plugins
- **Note**: This is a server-wide setting that affects all players

**Damage Control:**
- HE Grenade Damage: Uses server ConVar `sv_hegrenade_damage_multiplier` (0 = disabled, 1 = enabled)
- Fall Damage: Uses server ConVar `sv_falldamage_scale` (0 = disabled, 1 = enabled)

## 🐛 Troubleshooting

**Need more help?** Open an [Issue](../../issues) with:
- Server OS & CounterStrikeSharp version
- Console error messages
- Configuration file contents

## 🤝 Contributing

Contributions welcome! 

**Report bugs or suggest features:** Open an [Issue](../../issues)

**Code contributions:**
1. Fork the repository
2. Create feature branch: `git checkout -b feature/my-feature`
3. Commit changes: `git commit -m "Add feature"`
4. Push to branch: `git push origin feature/my-feature`
5. Open a Pull Request

<div align="center">

**⭐ Star this repo if you find it useful! ⭐**

</div>
