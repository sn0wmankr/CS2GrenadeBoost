using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Numerics;
using System.Text.Json.Serialization;

namespace GrenadeBoost;

[MinimumApiVersion(200)]
public class GrenadeBoostConfig : BasePluginConfig
{
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

    [JsonPropertyName("ExplosionRadius")]
    public float ExplosionRadius { get; set; } = 150.0f;

    // === Gameplay Features ===
    [JsonPropertyName("EnableAirAccuracy")]
    public bool EnableAirAccuracy { get; set; } = false;

    [JsonPropertyName("DisableHEGrenadeDamage")]
    public bool DisableHEGrenadeDamage { get; set; } = false;

    [JsonPropertyName("DisableFallDamage")]
    public bool DisableFallDamage { get; set; } = false;

    [JsonPropertyName("OnlyBoostInAir")]
    public bool OnlyBoostInAir { get; set; } = false;

    [JsonPropertyName("IgnoreTeamGrenades")]
    public bool IgnoreTeamGrenades { get; set; } = true;
}

public class GrenadeBoost : BasePlugin, IPluginConfig<GrenadeBoostConfig>
{
    public override string ModuleName => "CS2GrenadeBoost";
    public override string ModuleVersion => "1.0.3";
    public override string ModuleAuthor => "sn0wman";
    public override string ModuleDescription => "Allows players to boost themselves by throwing grenades on the ground";

    public GrenadeBoostConfig Config { get; set; } = new();

    private ConVar? _weaponAccuracyNospreadConVar = null;
    private ConVar? _svFallDamageScaleConVar = null;
    private ConVar? _svHegrenadeDamageMultiplierConVar = null;
    private float _originalFallDamageScale = 1.0f;
    private float _originalHegrenadeDamageMultiplier = 1.0f;

    public void OnConfigParsed(GrenadeBoostConfig config)
    {
        Config = config;
        
        // Ensure Version is set correctly
        if (Config.Version < 2)
        {
            Config.Version = 2;
        }
        
        // Setup ConVars
        try
        {
            if (Config.EnableAirAccuracy)
            {
                _weaponAccuracyNospreadConVar = ConVar.Find("weapon_accuracy_nospread");
                if (_weaponAccuracyNospreadConVar != null)
                {
                    Console.WriteLine("[GrenadeBoost] Found weapon_accuracy_nospread ConVar for air accuracy");
                }
            }
            
            if (Config.DisableFallDamage)
            {
                _svFallDamageScaleConVar = ConVar.Find("sv_falldamage_scale");
                if (_svFallDamageScaleConVar != null)
                {
                    _originalFallDamageScale = _svFallDamageScaleConVar.GetPrimitiveValue<float>();
                    Console.WriteLine($"[GrenadeBoost] Found sv_falldamage_scale ConVar (original value: {_originalFallDamageScale})");
                }
            }
            
            if (Config.DisableHEGrenadeDamage)
            {
                _svHegrenadeDamageMultiplierConVar = ConVar.Find("sv_hegrenade_damage_multiplier");
                if (_svHegrenadeDamageMultiplierConVar != null)
                {
                    _originalHegrenadeDamageMultiplier = _svHegrenadeDamageMultiplierConVar.GetPrimitiveValue<float>();
                    Console.WriteLine($"[GrenadeBoost] Found sv_hegrenade_damage_multiplier ConVar (original value: {_originalHegrenadeDamageMultiplier})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error finding ConVars: {ex.Message}");
        }
    }

    private Dictionary<IntPtr, bool> _trackedGrenades = new();

    public override void Load(bool hotReload)
    {
        try
        {
            RegisterEventHandler<EventHegrenadeDetonate>(OnHeGrenadeDetonate);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            
            Console.WriteLine("[GrenadeBoost] Plugin loaded successfully!");
            Console.WriteLine($"[GrenadeBoost] Config Version: {Config.Version}");
            if (Config.AutoGiveHEGrenade)
            {
                Console.WriteLine($"[GrenadeBoost] Auto give HE grenade enabled (Max: {Config.MaxHEGrenades})");
            }
            if (Config.EnableAirAccuracy)
            {
                Console.WriteLine("[GrenadeBoost] Air accuracy enabled (using weapon_accuracy_nospread)");
            }
            if (Config.DisableHEGrenadeDamage)
            {
                Console.WriteLine("[GrenadeBoost] HE grenade damage disabled (using sv_hegrenade_damage_multiplier)");
            }
            if (Config.DisableFallDamage)
            {
                Console.WriteLine("[GrenadeBoost] Fall damage disabled (using sv_falldamage_scale)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error during plugin load: {ex.Message}");
            throw;
        }
    }

    public override void Unload(bool hotReload)
    {
        // Restore ConVars on unload
        try
        {
            if (_weaponAccuracyNospreadConVar != null)
            {
                _weaponAccuracyNospreadConVar.SetValue(false);
                Console.WriteLine("[GrenadeBoost] Restored weapon_accuracy_nospread to default");
            }
            
            if (_svFallDamageScaleConVar != null)
            {
                _svFallDamageScaleConVar.SetValue(_originalFallDamageScale);
                Console.WriteLine($"[GrenadeBoost] Restored sv_falldamage_scale to {_originalFallDamageScale}");
            }
            
            if (_svHegrenadeDamageMultiplierConVar != null)
            {
                _svHegrenadeDamageMultiplierConVar.SetValue(_originalHegrenadeDamageMultiplier);
                Console.WriteLine($"[GrenadeBoost] Restored sv_hegrenade_damage_multiplier to {_originalHegrenadeDamageMultiplier}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error restoring ConVars: {ex.Message}");
        }
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        try
        {
            // Apply ConVar settings at round start
            if (Config.EnableAirAccuracy && _weaponAccuracyNospreadConVar != null)
            {
                _weaponAccuracyNospreadConVar.SetValue(true);
            }
            
            if (Config.DisableFallDamage && _svFallDamageScaleConVar != null)
            {
                _svFallDamageScaleConVar.SetValue(0.0f);
            }
            
            if (Config.DisableHEGrenadeDamage && _svHegrenadeDamageMultiplierConVar != null)
            {
                _svHegrenadeDamageMultiplierConVar.SetValue(0.0f);
            }

            if (!Config.AutoGiveHEGrenade)
                return HookResult.Continue;

            // Give HE grenade to all players at round start
            Server.NextFrame(() =>
            {
                try
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GrenadeBoost] Error giving grenades: {ex.Message}");
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
    public HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        try
        {
            if (!Config.Enabled)
                return HookResult.Continue;

            var userId = @event.Userid;
            if (userId == null || !userId.IsValid)
                return HookResult.Continue;

            // Get explosion position
            float grenadeX = @event.X;
            float grenadeY = @event.Y;
            float grenadeZ = @event.Z;

            Vector3 grenadePos = new Vector3(grenadeX, grenadeY, grenadeZ);

            // Find all players within explosion radius
            var players = Utilities.GetPlayers();
            if (players == null)
                return HookResult.Continue;

            foreach (var player in players)
            {
                if (player == null || !player.IsValid || !player.PawnIsAlive)
                    continue;

                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn == null || !playerPawn.IsValid)
                    continue;

                var playerPos = playerPawn.AbsOrigin;
                if (playerPos == null)
                    continue;

                // Check if should ignore team grenades
                if (Config.IgnoreTeamGrenades)
                {
                    // Skip if grenade thrower and player are on same team
                    if (userId.TeamNum == player.TeamNum)
                    {
                        continue;
                    }
                }

                // Check if player is on ground (only boost in air mode)
                if (Config.OnlyBoostInAir)
                {
                    var flags = (PlayerFlags)playerPawn.Flags;
                    if (flags.HasFlag(PlayerFlags.FL_ONGROUND))
                    {
                        continue; // Skip players on ground
                    }
                }

                // Calculate distance between player and grenade
                Vector3 playerPosVec = new Vector3(playerPos.X, playerPos.Y, playerPos.Z);
                float distance = Vector3.Distance(playerPosVec, grenadePos);

                // Use configurable explosion radius
                float maxRadius = Config.ExplosionRadius;

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
            // Log error but don't crash the plugin
            Console.WriteLine($"[GrenadeBoost] Error in OnHeGrenadeDetonate: {ex.Message}");
        }

        return HookResult.Continue;
    }
}
