using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using System.Numerics;
using System.Text.Json.Serialization;

namespace GrenadeBoost;

public class GrenadeBoostConfig : BasePluginConfig
{
    [JsonPropertyName("ConfigVersion")]
    public int ConfigVersion { get; set; } = 1;

    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    // === Grenade Settings ===
    [JsonPropertyName("AutoGiveHEGrenade")]
    public bool AutoGiveHEGrenade { get; set; } = true;

    [JsonPropertyName("MaxHEGrenades")]
    public int MaxHEGrenades { get; set; } = 1;

    // === Boost Physics ===
    [JsonPropertyName("HorizontalBoost")]
    public float HorizontalBoost { get; set; } = 800.0f;

    [JsonPropertyName("VerticalBoost")]
    public float VerticalBoost { get; set; } = 400.0f;

    [JsonPropertyName("BoostMultiplier")]
    public float BoostMultiplier { get; set; } = 1.2f;

    [JsonPropertyName("MaxBoostVelocity")]
    public float MaxBoostVelocity { get; set; } = 3500.0f;

    // === Gameplay Features ===
    [JsonPropertyName("EnableAirAccuracy")]
    public bool EnableAirAccuracy { get; set; } = false;

    [JsonPropertyName("DisableHEGrenadeDamage")]
    public bool DisableHEGrenadeDamage { get; set; } = false;

    [JsonPropertyName("DisableFallDamage")]
    public bool DisableFallDamage { get; set; } = false;
}

public class GrenadeBoost : BasePlugin, IPluginConfig<GrenadeBoostConfig>
{
    public override string ModuleName => "CS2GrenadeBoost";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "sn0wman";
    public override string ModuleDescription => "Allows players to boost themselves by throwing grenades on the ground";

    private const int CURRENT_CONFIG_VERSION = 1;
    public GrenadeBoostConfig Config { get; set; } = new();

    public void OnConfigParsed(GrenadeBoostConfig config)
    {
        // Check config version and update if needed
        if (config.ConfigVersion < CURRENT_CONFIG_VERSION)
        {
            Console.WriteLine($"[GrenadeBoost] Config version mismatch! Current: {config.ConfigVersion}, Required: {CURRENT_CONFIG_VERSION}");
            Console.WriteLine($"[GrenadeBoost] Updating config to version {CURRENT_CONFIG_VERSION}...");
            
            // Force update to new version with default values
            config.ConfigVersion = CURRENT_CONFIG_VERSION;
            config.HorizontalBoost = 800.0f;
            config.VerticalBoost = 400.0f;
            config.EnableAirAccuracy = false;
            config.DisableHEGrenadeDamage = false;
            config.DisableFallDamage = false;

            // Save updated config
            var configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/GrenadeBoost/GrenadeBoost.json");
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(configPath, json);
                Console.WriteLine($"[GrenadeBoost] Config updated successfully to version {CURRENT_CONFIG_VERSION}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GrenadeBoost] Failed to save updated config: {ex.Message}");
            }
        }

        Config = config;
        
        // Ensure config file exists - create if missing
        var configFilePath = Path.Combine(ModuleDirectory, "../../configs/plugins/GrenadeBoost/GrenadeBoost.json");
        if (!File.Exists(configFilePath))
        {
            try
            {
                var directory = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }
                
                var json = System.Text.Json.JsonSerializer.Serialize(Config, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(configFilePath, json);
                Console.WriteLine($"[GrenadeBoost] Config file created at {configFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GrenadeBoost] Failed to create config: {ex.Message}");
            }
        }
    }

    private Dictionary<IntPtr, bool> _trackedGrenades = new();

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventHegrenadeDetonate>(OnHeGrenadeDetonate);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        
        // Register tick listener for air accuracy
        if (Config.EnableAirAccuracy)
        {
            RegisterListener<Listeners.OnTick>(OnTick);
        }
        
        Console.WriteLine("[GrenadeBoost] Plugin loaded successfully!");
        Console.WriteLine($"[GrenadeBoost] Config Version: {Config.ConfigVersion}");
        if (Config.AutoGiveHEGrenade)
        {
            Console.WriteLine($"[GrenadeBoost] Auto give HE grenade enabled (Max: {Config.MaxHEGrenades})");
        }
        if (Config.EnableAirAccuracy)
        {
            Console.WriteLine("[GrenadeBoost] Air accuracy enabled");
        }
        if (Config.DisableHEGrenadeDamage)
        {
            Console.WriteLine("[GrenadeBoost] HE grenade damage disabled");
        }
        if (Config.DisableFallDamage)
        {
            Console.WriteLine("[GrenadeBoost] Fall damage disabled");
        }
    }

    private void OnTick()
    {
        try
        {
            // Air accuracy check
            if (Config.EnableAirAccuracy)
            {
                CheckAirAccuracy();
            }
        }
        catch
        {
            // Silent catch to prevent console spam
        }
    }

    private void CheckAirAccuracy()
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive)
                continue;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null)
                continue;

            // Check if player is in the air (not on ground)
            bool isOnGround = (playerPawn.Flags & (uint)PlayerFlags.FL_ONGROUND) != 0;

            var activeWeapon = playerPawn.WeaponServices?.ActiveWeapon.Value;
            if (activeWeapon != null && activeWeapon.IsValid)
            {
                var weaponData = activeWeapon.As<CCSWeaponBase>();
                if (weaponData != null && !isOnGround)
                {
                    // Set inaccuracy to 0 for perfect air accuracy
                    Schema.SetSchemaValue(activeWeapon.Handle, "CCSWeaponBaseVData", "m_fInaccuracyStand", 0.0f);
                    Schema.SetSchemaValue(activeWeapon.Handle, "CCSWeaponBaseVData", "m_fInaccuracyCrouch", 0.0f);
                    Schema.SetSchemaValue(activeWeapon.Handle, "CCSWeaponBaseVData", "m_fInaccuracyJump", 0.0f);
                    Schema.SetSchemaValue(activeWeapon.Handle, "CCSWeaponBaseVData", "m_fInaccuracyLand", 0.0f);
                    Schema.SetSchemaValue(activeWeapon.Handle, "CCSWeaponBaseVData", "m_fInaccuracyMove", 0.0f);
                }
            }
        }
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!Config.AutoGiveHEGrenade)
            return HookResult.Continue;

        try
        {
            // Give HE grenade to all players at round start
            Server.NextFrame(() =>
            {
                var players = Utilities.GetPlayers();
                foreach (var player in players)
                {
                    if (player == null || !player.IsValid || !player.PawnIsAlive)
                        continue;

                    var playerPawn = player.PlayerPawn.Value;
                    if (playerPawn == null)
                        continue;

                    // Count current HE grenades
                    int heGrenadeCount = 0;
                    var weapons = playerPawn.WeaponServices?.MyWeapons;
                    if (weapons != null)
                    {
                        foreach (var weaponHandle in weapons)
                        {
                            if (weaponHandle?.Value?.DesignerName.Contains("hegrenade") == true)
                            {
                                heGrenadeCount++;
                            }
                        }
                    }

                    // Give grenades up to max count
                    int toGive = Config.MaxHEGrenades - heGrenadeCount;
                    for (int i = 0; i < toGive; i++)
                    {
                        player.GiveNamedItem("weapon_hegrenade");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error in OnRoundStart: {ex.Message}");
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            var attacker = @event.Attacker;
            var weapon = @event.Weapon;
            var damage = @event.Health;

            // Disable HE grenade damage only
            if (Config.DisableHEGrenadeDamage && weapon == "hegrenade")
            {
                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn != null && playerPawn.Health > 0)
                {
                    // Restore health lost from grenade
                    playerPawn.Health += damage;
                    if (@event.Armor > 0)
                    {
                        playerPawn.ArmorValue = @event.Armor;
                    }
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
                }
                return HookResult.Handled;
            }

            // Disable fall damage
            if (Config.DisableFallDamage && weapon == "worldspawn")
            {
                // Check if it's fall damage (worldspawn with no attacker or self-damage)
                if (attacker == null || attacker == player)
                {
                    var playerPawn = player.PlayerPawn.Value;
                    if (playerPawn != null && playerPawn.Health > 0)
                    {
                        // Restore health lost from fall damage
                        playerPawn.Health += damage;
                        Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
                    }
                    return HookResult.Handled;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error in OnPlayerHurt: {ex.Message}");
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        try
        {
            if (!Config.Enabled)
                return HookResult.Continue;

            var userId = @event.Userid;
            if (userId == null)
                return HookResult.Continue;

            // Get explosion position
            float grenadeX = @event.X;
            float grenadeY = @event.Y;
            float grenadeZ = @event.Z;

            Vector3 grenadePos = new Vector3(grenadeX, grenadeY, grenadeZ);

            // Find all players within explosion radius
            var players = Utilities.GetPlayers();
            foreach (var player in players)
            {
                if (player == null || !player.IsValid || !player.PawnIsAlive)
                    continue;

                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn == null)
                    continue;

                var playerPos = playerPawn.AbsOrigin;
                if (playerPos == null)
                    continue;

                // Calculate distance between player and grenade
                Vector3 playerPosVec = new Vector3(playerPos.X, playerPos.Y, playerPos.Z);
                float distance = Vector3.Distance(playerPosVec, grenadePos);

                // HE grenade explosion radius is approximately 350 units
                float maxRadius = 350.0f;

                if (distance < maxRadius)
                {
                    // Calculate boost strength based on distance (closer = stronger)
                    float boostStrength = (1.0f - (distance / maxRadius)) * Config.BoostMultiplier;
                    
                    // Calculate direction vector from grenade to player
                    Vector3 direction = playerPosVec - grenadePos;

                    if (direction.Length() > 0.01f)
                    {
                        direction = Vector3.Normalize(direction);

                        // Calculate horizontal distance to determine if it's a vertical boost
                        float horizontalDistance = MathF.Sqrt(
                            (playerPosVec.X - grenadePos.X) * (playerPosVec.X - grenadePos.X) +
                            (playerPosVec.Y - grenadePos.Y) * (playerPosVec.Y - grenadePos.Y)
                        );

                        // If grenade is directly below player (small horizontal distance), boost mostly vertical
                        float horizontalRatio = MathF.Min(horizontalDistance / 100.0f, 1.0f); // 100 units threshold
                        
                        // Calculate boost vector (emphasize upward direction when grenade is below)
                        float horizontalBoost = Config.HorizontalBoost * boostStrength * horizontalRatio;
                        float verticalBoost = Config.VerticalBoost * boostStrength;

                        Vector3 boost = new Vector3(
                            direction.X * horizontalBoost,
                            direction.Y * horizontalBoost,
                            verticalBoost // Always upward, stronger when directly below
                        );

                        // Get current velocity
                        var velocity = playerPawn.AbsVelocity;
                        if (velocity != null)
                        {
                            // Add boost to current velocity
                            Vector3 newVelocity = new Vector3(
                                velocity.X + boost.X,
                                velocity.Y + boost.Y,
                                velocity.Z + boost.Z
                            );

                            // Apply maximum velocity limit
                            if (newVelocity.Length() > Config.MaxBoostVelocity)
                            {
                                newVelocity = Vector3.Normalize(newVelocity) * Config.MaxBoostVelocity;
                            }

                            // Apply new velocity
                            playerPawn.AbsVelocity.X = newVelocity.X;
                            playerPawn.AbsVelocity.Y = newVelocity.Y;
                            playerPawn.AbsVelocity.Z = newVelocity.Z;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error in OnHeGrenadeDetonate: {ex.Message}");
        }

        return HookResult.Continue;
    }
}
