using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
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
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "sn0wman";
    public override string ModuleDescription => "Allows players to boost themselves by throwing grenades on the ground";

    public GrenadeBoostConfig Config { get; set; } = new();

    private ConVar? _weaponAccuracyNospreadConVar = null;
    private ConVar? _svFallDamageScaleConVar = null;
    private float _originalFallDamageScale = 1.0f;
    
    // Native hook for TakeDamage
    private MemoryFunctionWithReturn<CCSPlayerPawn, CTakeDamageInfo, CCSPlayerPawn, int>? _takeDamageFunc = null;

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
            
            // Setup TakeDamage hook for HE grenade damage blocking
            if (Config.DisableHEGrenadeDamage)
            {
                _takeDamageFunc = new MemoryFunctionWithReturn<CCSPlayerPawn, CTakeDamageInfo, CCSPlayerPawn, int>(GameData.GetSignature("CBaseEntity_TakeDamageOld"));
                _takeDamageFunc.Hook(OnTakeDamage, HookMode.Pre);
                Console.WriteLine("[GrenadeBoost] Hooked TakeDamage for HE grenade damage blocking");
            }
            
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
                Console.WriteLine("[GrenadeBoost] HE grenade damage disabled (native hook method)");
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

    private HookResult OnTakeDamage(DynamicHook hook)
    {
        try
        {
            if (!Config.DisableHEGrenadeDamage)
                return HookResult.Continue;

            var damageInfo = hook.GetParam<CTakeDamageInfo>(1);
            if (damageInfo == null)
                return HookResult.Continue;

            // Check if damage is from HE grenade by checking the ability (weapon)
            var weapon = damageInfo.Ability.Value;
            if (weapon != null && weapon.DesignerName != null && weapon.DesignerName.Contains("hegrenade"))
            {
                // Block the damage by setting it to 0
                damageInfo.Damage = 0;
                return HookResult.Changed;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrenadeBoost] Error in OnTakeDamage: {ex.Message}");
        }

        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        // Unhook TakeDamage
        if (_takeDamageFunc != null)
        {
            _takeDamageFunc.Unhook(OnTakeDamage, HookMode.Pre);
            Console.WriteLine("[GrenadeBoost] Unhooked TakeDamage");
        }
        
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
            // Log error but don't crash the plugin
            Console.WriteLine($"[GrenadeBoost] Error in OnHeGrenadeDetonate: {ex.Message}");
        }

        return HookResult.Continue;
    }
}
