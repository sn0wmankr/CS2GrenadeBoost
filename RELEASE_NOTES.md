# Release Notes

## Version 1.0.4

### 🔧 Changed
- **Config Version**: Updated to version `4`
- **DisableHEGrenadeDamage** default changed from `false` → `true` (HE grenade damage disabled by default)
- **DisableFallDamage** default changed from `false` → `true` (Fall damage disabled by default)
- **IgnoreTeamGrenades** logic improved: Now allows self-boost (added `userId != player` check)
  - Same team grenades are still ignored, but you can boost yourself with your own grenades

### ❌ Removed
- **StickyGrenades** feature removed (didn't work properly due to API limitations)

### 📝 Technical Details
- Better default configuration for new users
- Self-boost logic now properly checks if grenade owner is the same player
- Cleaner codebase with removed non-functional sticky grenade implementation

---

## Version 1.0.3

### ✨ New Features
- **Configurable Explosion Radius**: Added `ExplosionRadius` setting (default: 150 units)
  - Adjust how far from the grenade players can be boosted
  - Smaller radius = more precise grenade throws required
- **Only Boost In Air**: Added `OnlyBoostInAir` toggle (default: false)
  - When enabled, players on ground won't be affected by grenade boost
  - Perfect for preventing unwanted knockback while standing
- **Ignore Team Grenades**: Added `IgnoreTeamGrenades` toggle (default: false)
  - When enabled, only enemy grenades boost you
  - Same team grenades won't affect you
  - Useful for competitive gameplay to prevent team trolling

### ⚙️ Configuration Changes
- Added `ExplosionRadius` (float, default: 150.0) - Changed from hardcoded 350
- Added `OnlyBoostInAir` (bool, default: false)
- Added `IgnoreTeamGrenades` (bool, default: true) - Only boost from enemy grenades by default

### 📝 Technical Details
- Checks player ground state using `PlayerFlags.FL_ONGROUND`
- Checks team affiliation using `TeamNum` comparison
- Dynamic explosion radius from config instead of hardcoded value
- Smaller radius encourages more precise grenade throws

---

## Version 1.0.2

### 🔧 Critical Fix
- **Changed HE Grenade Damage Blocking Method**: Replaced Native Hook with `sv_hegrenade_damage_multiplier` ConVar
- **More Reliable**: Simpler and more stable implementation at engine level
- **Bug Fix**: Fixed HE grenade damage not being properly blocked in v1.0.1
- **Compatibility**: Improved compatibility with other plugins

### 📝 Technical Changes
- Removed Native Hook (TakeDamage) system
- Removed `CounterStrikeSharp.API.Modules.Memory` dependencies
- Added `sv_hegrenade_damage_multiplier` ConVar management
- Cleaner and more maintainable code

---

## Version 1.0.1

### 🔧 Changes
- **Native Hook**: HE grenade damage now uses native TakeDamage hook (perfect blocking)
- **Air Accuracy**: Changed to use `weapon_accuracy_nospread` ConVar
- **Fall Damage**: Changed to use `sv_falldamage_scale` ConVar
- **Bug Fix**: Fixed WeaponPaints compatibility issues
- **Performance**: Improved damage blocking efficiency

---

## Version 1.0.0 - Initial Release

### 🎉 What's New

A Counter-Strike 2 plugin that allows players to boost themselves by throwing HE grenades on the ground.

### ✨ Features

- **Grenade Boost Mechanics** - Throw HE grenades to propel yourself upward/forward
- **Customizable Physics** - Separate horizontal (800) and vertical (400) boost values
- **Air Accuracy** - Optional perfect AWP accuracy while airborne
- **Auto-Give Grenades** - Automatically give HE grenades at round start (configurable max count)
- **Damage Control** - Optional HE grenade and fall damage negation
- **Smart Config** - Auto-generates and updates configuration file

### 🚀 Quick Start

1. Install CounterStrikeSharp
2. Place `GrenadeBoost.dll` in `addons/counterstrikesharp/plugins/GrenadeBoost/`
3. Restart server or use `css_plugins load GrenadeBoost`
4. Config auto-generates at first load

### 💡 Usage Tips

- Throw grenades straight down for vertical boost
- Throw at an angle for horizontal movement
- Enable air accuracy for AWP servers
- Adjust boost values to match your gameplay style