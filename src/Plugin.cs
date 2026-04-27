using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using LethalLib.Modules;
using static LethalLib.Modules.Levels;
using static LethalLib.Modules.Enemies;
using BepInEx.Logging;
using System.IO;
using BepInEx.Configuration;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using LethalConfig;
using System.Collections.Generic;
using DeviousTraps.src;
using Unity.Netcode;
using System;
using System.Threading.Tasks;
using LethalLib.Extras;
using UnityEngine.Rendering;
using GameNetcodeStuff;
using DeviousTraps.src.SoundCannon;
using DeviousTraps.src.MouseTrap;

namespace DeviousTraps
{
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    [BepInDependency("ainavt.lc.lethalconfig")]
    [BepInPlugin("DeviousTraps", "DeviousTraps", "1.4.8")]
    public class Plugin : BaseUnityPlugin
    {
        public static Harmony _harmony;
        public static new ManualLogSource Logger;

        // easter island net prefabs
        public static AssetBundle TrapBundle;

        // SawTurret net prefabs
        public static GameObject SawTurretPrefab;
        public static GameObject SawPrefab;
        public static SpawnableMapObjectDef SawTurretDef;

        // FlameTurret net prefabs
        public static GameObject FlameTurretPrefab;
        public static SpawnableMapObjectDef FlameTurretDef;

        // LRAD net prefabs
        public static GameObject LRADTurretPrefab;
        public static GameObject LRADBlastPrefab;
        public static GameObject LRADImpactPrefab;
        public static SpawnableMapObjectDef LRADTurretDef;

        // Mouse Trap Prefabs
        public static GameObject MouseTrapPrefab;
        public static GameObject MouseTrapSpawnerPrefab;
        public static SpawnableMapObjectDef MouseTrapDef;


        // Plasma Turret Prefabs
        public static GameObject PlasmaTurretPrefab;
        public static GameObject PlasmaBallPrefab;
        public static SpawnableMapObjectDef PlasmaTurretDef;

        public void LogIfDebugBuild(string text)
        {
#if DEBUG
            Plugin.Logger.LogInfo(text);
#endif
        }

        private void Awake()
        {
            Logger = base.Logger;
            PopulateAssets();
            bindVars();
            
            // load assemblies
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }

            // load net assets
            SawTurretPrefab = TrapBundle.LoadAsset<GameObject>("SawTurretTrap");
            SawPrefab = TrapBundle.LoadAsset<GameObject>("SawBlade");
            SawTurretDef = TrapBundle.LoadAsset<SpawnableMapObjectDef>("SawTurretDef");

            FlameTurretPrefab = TrapBundle.LoadAsset<GameObject>("FlameTurret");
            FlameTurretDef = TrapBundle.LoadAsset<SpawnableMapObjectDef>("FlamePillarDef");


            LRADTurretPrefab = TrapBundle.LoadAsset<GameObject>("LRAD");
            LRADTurretDef = TrapBundle.LoadAsset<SpawnableMapObjectDef>("SoundTurretDef");
            LRADBlastPrefab = TrapBundle.LoadAsset<GameObject>("SoundWave");
            LRADImpactPrefab = TrapBundle.LoadAsset<GameObject>("ImpactFX");

            MouseTrapPrefab = TrapBundle.LoadAsset<GameObject>("MouseTrap");
            MouseTrapDef = TrapBundle.LoadAsset<SpawnableMapObjectDef>("MouseTrapDef");
            MouseTrapSpawnerPrefab = TrapBundle.LoadAsset<GameObject>("MouseTrapSpawner");

            PlasmaTurretPrefab = TrapBundle.LoadAsset<GameObject>("PlasmaTurret");
            PlasmaTurretDef = TrapBundle.LoadAsset<SpawnableMapObjectDef>("PlasmaTurretDef");
            PlasmaBallPrefab = TrapBundle.LoadAsset<GameObject>("PlasmaBall");

            //Debug.Log("Saw Turret Prefab: " + SawTurretPrefab);
            //Debug.Log("Saw Blade Prefab: " + SawPrefab);
            //Debug.Log("Saw Turret Def: " + SawTurretDef);
            //Debug.Log("Flame Turret Prefab: " + FlameTurretPrefab);
            //Debug.Log("Flame Turret Def: " + FlameTurretDef);
            //Debug.Log("LRAD Impact FX " + LRADBlastPrefab);
            //Debug.Log("LRAD Turret Def: " + LRADTurretPrefab);
            //Debug.Log("Mouse Trap Prefab " + MouseTrapPrefab);
            //Debug.Log("Mouse Trap Def: " + MouseTrapDef);

            UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);

            // register phase 
            // supply a lambda later for mapping the trap to various selectable levels...
            LethalLib.Modules.MapObjects.RegisterMapObject(SawTurretDef, LevelTypes.All, (SelectableLevel _) =>
            {
                var minTurrets = 0;
                var maxTurrets = 4.8 * Plugin.SawSpawnrate.Value;
                AnimationCurve curve = new AnimationCurve(new Keyframe[]
{
                    new Keyframe(0f, (float)minTurrets, 0.267f, 0.267f, 0f, 0.246f),  // min turret reff from missile turret = 0
                    new Keyframe(1f, (float)maxTurrets, 61f, 61f, 0.015f * (float)maxTurrets, 0f)  // max turret ref from missile turret = 6
                });
                return curve;
            });

            // register phase 
            // supply a lambda later for mapping the trap to various selectable levels...
            LethalLib.Modules.MapObjects.RegisterMapObject(FlameTurretDef, LevelTypes.All, (SelectableLevel _) =>
            {
                var minTurrets = 0;
                var maxTurrets = 4.8 * Plugin.FlameSpawnrate.Value;
                AnimationCurve curve = new AnimationCurve(new Keyframe[]
{
                    new Keyframe(0f, (float)minTurrets, 0.267f, 0.267f, 0f, 0.246f),  // min turret reff from missile turret = 0
                    new Keyframe(1f, (float)maxTurrets, 61f, 61f, 0.015f * (float)maxTurrets, 0f)  // max turret ref from missile turret = 6
                });
                return curve;
            });

            // register phase 
            // supply a lambda later for mapping the trap to various selectable levels...
            LethalLib.Modules.MapObjects.RegisterMapObject(LRADTurretDef, LevelTypes.All, (SelectableLevel _) =>
            {
                var minTurrets = 0;
                var maxTurrets = 4.8 * Plugin.LRADSpawnrate.Value;
                AnimationCurve curve = new AnimationCurve(new Keyframe[]
{
                    new Keyframe(0f, (float)minTurrets, 0.267f, 0.267f, 0f, 0.246f),  // min turret reff from missile turret = 0
                    new Keyframe(1f, (float)maxTurrets, 61f, 61f, 0.015f * (float)maxTurrets, 0f)  // max turret ref from missile turret = 6
                });
                return curve;
            });

            // register phase 
            // supply a lambda later for mapping the trap to various selectable levels...
            LethalLib.Modules.MapObjects.RegisterMapObject(MouseTrapDef, LevelTypes.All, (SelectableLevel _) =>
            {
                var minTurrets = 0;
                var maxTurrets = 4.8 * Plugin.MouseTrapSpawnrate.Value;
                AnimationCurve curve = new AnimationCurve(new Keyframe[]
{
                    new Keyframe(0f, (float)minTurrets, 0.267f, 0.267f, 0f, 0.246f),  // min turret reff from missile turret = 0
                    new Keyframe(1f, (float)maxTurrets, 61f, 61f, 0.015f * (float)maxTurrets, 0f)  // max turret ref from missile turret = 6
                });
                return curve;
            });

            // register phase 
            // supply a lambda later for mapping the trap to various selectable levels...
            LethalLib.Modules.MapObjects.RegisterMapObject(PlasmaTurretDef, LevelTypes.All, (SelectableLevel _) =>
            {
                var minTurrets = 0;
                var maxTurrets = 4.8 * Plugin.PlasmaSpawnrate.Value;
                AnimationCurve curve = new AnimationCurve(new Keyframe[]
{
                    new Keyframe(0f, (float)minTurrets, 0.267f, 0.267f, 0f, 0.246f),  // min turret reff from missile turret = 0
                    new Keyframe(1f, (float)maxTurrets, 61f, 61f, 0.015f * (float)maxTurrets, 0f)  // max turret ref from missile turret = 6
                });
                return curve;
            });



            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(SawPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(SawTurretPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(FlameTurretPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(LRADTurretPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(LRADBlastPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(LRADImpactPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(MouseTrapPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(PlasmaTurretPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(PlasmaBallPrefab);
            Hooks();
        }

        public void Hooks()
        {
            // impactfx cleanup
            On.RoundManager.LoadNewLevel += (On.RoundManager.orig_LoadNewLevel orig, global::RoundManager self, int randomSeed, global::SelectableLevel newLevel) =>
            {
                orig.Invoke(self, randomSeed, newLevel);
                try
                {
                    if (ImpactFXState.Instances != null)
                    {
                        foreach (var fx in ImpactFXState.Instances)
                        {
                            if (fx != null)
                            {
                                Destroy(fx);
                            }
                        }
                        ImpactFXState.Instances.Clear();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Devious Traps: LRAD FX clear error: " + e.ToString() + " this is only an issue if you see red noise fx on your screen or if you are deaf. Otherwise this is OK.");
                }

                try
                {
                    if (MouseTrap.instances != null)
                    {
                        foreach (var trap in MouseTrap.instances)
                        {
                            if (trap != null)
                            {
                                if (trap.gameObject) { Destroy(trap.gameObject); }
                                else
                                {
                                    Destroy(trap);
                                }
                            }
                        }
                        MouseTrap.instances.Clear();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Devious Traps: Mouse Trap clear error: " + e.ToString() + " this is only an issue if you have mouse traps attached to you and you can't remove them. Otherwise this is OK.");
                }
            };
        }

        // SETTINGS SECTION
        // consider these multipliers for existing values
        public static ConfigEntry<float> SawSpawnrate; // 
        public static ConfigEntry<float> SawDmgMult; //
        public static ConfigEntry<float> SawTargetRange; //
        public static ConfigEntry<float> SawWindupTime; //
        public static ConfigEntry<float> SawFirerate; //
        public static ConfigEntry<int> SawAmmo; //
        public static ConfigEntry<float> SawReloadTime; //
        public static ConfigEntry<float> SawLaunchSpeed; //
        public static ConfigEntry<float> SawVolume;
        public static ConfigEntry<float> SawRotationSpeed;

        public static ConfigEntry<float> FlameSpawnrate; //
        public static ConfigEntry<float> FlameDmgMult; //
        public static ConfigEntry<float> FlameTargetRange; //
        public static ConfigEntry<float> FlameRisingTime; //
        public static ConfigEntry<float> FlameRestingTime; //
        public static ConfigEntry<float> FlameRotationSpeed; //
        public static ConfigEntry<float> FlameSpinningTime; //
        public static ConfigEntry<float> FlameVolume; //
        public static ConfigEntry<float> SmokeCooldown;

        public static ConfigEntry<float> LRADSpawnrate;
        public static ConfigEntry<float> LRADChargeTime;
        public static ConfigEntry<float> LRADReloadTime;
        public static ConfigEntry<float> LRADTargetRange; 
        public static ConfigEntry<float> LRADDmgMult;
        public static ConfigEntry<float> LRADDisorientPeriod;
        public static ConfigEntry<float> LRADDizzyMult;
        public static ConfigEntry<float> LRADDrunknessMult;
        public static ConfigEntry<float> LRADFXMult;
        public static ConfigEntry<float> LRADVolume; 
        public static ConfigEntry<float> LRADFXVolume;
        public static ConfigEntry<float> LRADProjectileSpeed;
        public static ConfigEntry<float> LRADRotationSpeed;

        public static ConfigEntry<float> MouseTrapSpawnrate;
        public static ConfigEntry<float> BigMTrapChance;
        public static ConfigEntry<float> GiantMTrapChance;
        public static ConfigEntry<float> MTrapScrapBaitForgiveness;
        public static ConfigEntry<int> SmallMTrapDmg;
        public static ConfigEntry<int> BigMTrapDmg;
        public static ConfigEntry<int> GiantMTrapDmg;
        public static ConfigEntry<bool> SmallMTrapAttaches;
        public static ConfigEntry<bool> AttachmentWepRequired;
        public static ConfigEntry<bool> MTrapCanBeDisabled;
        public static ConfigEntry<String> MTrapWhitelist;

        public static ConfigEntry<float> PlasmaSpawnrate;
        public static ConfigEntry<float> PlasmaReloadTime;
        public static ConfigEntry<float> PlasmaWindupTime;
        public static ConfigEntry<float> PlasmaTargetRange;
        public static ConfigEntry<int> PlasmaBallsPerBurst;
        public static ConfigEntry<int> PlasmaBurstQuantity;
        public static ConfigEntry<float> PlasmaProjDelay;
        public static ConfigEntry<float> PlasmaBurstDelay;
        public static ConfigEntry<float> PlasmaProjectileSpeed;
        public static ConfigEntry<float> PlasmaRotationSpeed;
        public static ConfigEntry<float> PlasmaBallLifetime;
        public static ConfigEntry<float> PlasmaTurretVolume;

        public void bindVars()
        {
            SawSpawnrate = Config.Bind("Saw Turret", "Spawnrate", 1.0f, "How often do these turrets spawn? (default 1.0)");
            SawFirerate = Config.Bind("Saw Turret", "Time Between Shots", 1.45f, "Length of time between each saw after windup. (default 1.45)");
            SawDmgMult = Config.Bind("Saw Turret", "Dmg Multiplier", 1.8f, "Damage multiplier for saw blades. Dmg is also dependent on the velocity of a saw blade. You can make them heal with negative values too. (default 1.8)");
            SawTargetRange = Config.Bind("Saw Turret", "Range", 25f, "How far away a saw turret can see you. They can't see through walls though. (default 25)");
            SawWindupTime = Config.Bind("Saw Turret", "Windup Time", 1.45f, "How long a saw turret takes before it starts firing saws at you. (default 1.45)");
            SawFirerate = Config.Bind("Saw Turret", "Time Between Shots", 1.45f, "Length of time between each saw after windup. (default 1.45)");
            SawAmmo = Config.Bind("Saw Turret", "Ammo", 4, "Shots it takes before the turret has to reload. (default 4)");
            SawReloadTime = Config.Bind("Saw Turret", "Reload Time", 8f, "How long it takes for a saw turret to reload its sawblades (default 8)");
            SawFirerate = Config.Bind("Saw Turret", "Time Between Shots", 1.45f, "Length of time between each saw after windup. (default 1.45)");
            SawLaunchSpeed = Config.Bind("Saw Turret", "Projectile Speed", 3000f, "How fast are the saws launched from the saw turret? Note that the faster a saw travels, the more damage it will deal. Assume you are applying a force in Newtons (N) to the object. (default 3000)");
            SawVolume = Config.Bind("Saw Turret", "Volume", 0.6f, "How loud are all sounds from this turret? (default 0.6)");
            SawRotationSpeed = Config.Bind("Saw Turret", "Rotation Speed", 100f, "How quickly does the Saw Turret rotate to face its target (degrees per second)? The lower the value, the easier it is to outmaneuver. (default 100)");

            FlameSpawnrate = Config.Bind("Flame Turret", "Spawnrate", 1.0f, "How often do these turrets spawn? (default 1.0)");
            FlameDmgMult = Config.Bind("Flame Turret", "Dmg Multiplier", 2.56f, "Damage Multiplier for flame turrets. You can make them heal with negative values too. (default 2.56)");
            FlameTargetRange = Config.Bind("Flame Turret", "Range", 7f, "How far away a flame turret can see you. They can't see through walls though. (default 7)");
            FlameRisingTime = Config.Bind("Flame Turret", "Rising Time", 1.4f, "Time in seconds it takes for a flame turret to rise up and fire at you. (default 1.4)");
            FlameRestingTime = Config.Bind("Flame Turret", "Resting Time", 3f, "How long does the flame turret rest before firing again? In seconds. (default 3)");
            FlameSpinningTime = Config.Bind("Flame Turret", "Spinning Time", 3f, "How long does the flame turret spin for (attack duration)? In seconds. (default 3)");
            FlameRotationSpeed = Config.Bind("Flame Turret", "Rotation Speed", 120f, "How fast does a flame turret rotate when attacking (degrees per second)? (default 120)");
            SmokeCooldown = Config.Bind("Flame Turret", "Warning Interval", 20f, "How often a flame turret releases smoke to warn the player of its presence. The smoke release phase lasts for 4.5 seconds. (default 20)");
            FlameVolume = Config.Bind("Flame Turret", "Volume", 0.6f, "How loud are all sounds from this turret? (default 0.6)");

            LRADSpawnrate = Config.Bind("Sound Turret", "Spawnrate", 0.8f, "How often do these turrets spawn? (default 0.8)");
            LRADTargetRange = Config.Bind("Sound Turret", "Range", 50f, "How far away the LRAD (sound turret) can see you. They can't see through walls though. (default 50)");
            LRADDmgMult = Config.Bind("Sound Turret", "Dmg Multiplier", 1f, "Damage Multiplier for sound turrets. You can make them heal with negative values too. (default 1)");
            LRADProjectileSpeed = Config.Bind("Sound Turret", "Projectile Speed Multiplier", 1.5f, "Multiplier for the Speed of the launched sound wave. (default 1.5)");
            LRADReloadTime = Config.Bind("Sound Turret", "Time Between Shots", 60f, "Length of time before the sound cannon can fire again. (default 30)");
            LRADChargeTime = Config.Bind("Sound Turret", "Charge Up Time", 5.53f, "Time it takes for the LRAD to charge and fire a sound wave in seconds. (default 5.53)");
            LRADDisorientPeriod = Config.Bind("Sound Turret", "Disorientation Period", 20f, "How long deafness, visual fx, drunkness, and dizziness takes to fade away in seconds (default 20).");
            LRADDizzyMult = Config.Bind("Sound Turret", "Dizzyness multiplier", 1f, "How much the LRAD cannon messes around with your movement, causing you to sway unpredictably. (default 1)");
            LRADDrunknessMult = Config.Bind("Sound Turret", "Drunkness Multiplier", 2f, "How strong the drunkness effect from the LRAD cannon is. (default 2)");
            LRADFXMult = Config.Bind("Sound Turret", "FX Intensity", 0.8f, "Opacity multiplier to the noise and glitch visual FX when hit by the cannon. Higher values are equivalent to total blindness (default 0.8)");
            LRADVolume = Config.Bind("Sound Turret", "Turret Volume", 1f, "How loud are all sounds from this turret? (default 1)");
            LRADFXVolume = Config.Bind("Sound Turret", "FX Volume", 1f, "How loud are all sounds from the Disorientation/Impact effect? (default 1)");
            LRADRotationSpeed = Config.Bind("Sound Turret", "Rotation Speed", 60f, "How quickly does the LRAD rotate to face its target (degrees per second)? The lower the value, the easier it is to outmaneuver. (default 60)");

            MouseTrapSpawnrate = Config.Bind("Mouse Trap", "Spawnrate", 0.8f, "How often do mouse traps spawn? Note that this is for 3 separate groups of mousetraps, not just one. (default 0.8)");
            BigMTrapChance = Config.Bind("Mouse Trap", "Big Mouse Trap Chance", 12f, "Percent chance for a mouse trap to spawn as a large one. (default 12%)");
            GiantMTrapChance = Config.Bind("Mouse Trap", "Giant Mouse Trap Chance", 8f, "Percent chance for a mouse trap to be giant. Giant traps have scrap as bait. You have to be really careful when stealing the bait! (default 8%)");
            SmallMTrapDmg = Config.Bind("Mouse Trap", "Small Mouse Trap Damage", 12, "Damage Amount for small traps. You can make them heal with negative values too. (default 12)");
            BigMTrapDmg = Config.Bind("Mouse Trap", "Big Mouse Trap Damage", 36, "Damage Amount for Medium traps. You can make them heal with negative values too. (default 36)");
            GiantMTrapDmg = Config.Bind("Mouse Trap", "Giant Mouse Trap Damage", 100, "Damage Amount for Large traps. You can make them heal with negative values too. (default 100)");
            SmallMTrapAttaches = Config.Bind("Mouse Trap", "Small Mouse Trap Attachment", true, "If Small Mouse Traps stick onto players. A weapon is required to remove the trap (default true).");
            AttachmentWepRequired = Config.Bind("Mouse Trap", "Weapon required to remove trap", true, "If mouse traps stuck onto players need a weapon to remove them. (default true)");
            MTrapCanBeDisabled = Config.Bind("Mouse Trap", "Traps can be disabled", true, "If all mouse traps can be disabled permanently by hitting them with a melee weapon. (default true)");
            MTrapWhitelist = Config.Bind("Mouse Trap", "Bait whitelist", "gift box, jar of pickles, gold bar, fancy lamp, golden cup, zed dog", "All scrap in this whitelist can be selected as bait for the giant trap. Enter the name that appears when you scan the scrap in-game. Comma separated list. (not case sensitive).");
            MTrapScrapBaitForgiveness = Config.Bind("Mouse Trap", "Mouse Trap Bait Forgiveness", 0.65f, "Alters the size of the hitbox that makes the giant mouse trap bait grabbable by players. The lower the value, the harder it is to get the item. 0.63 = insane, 0.65 = hard, 0.8 = forgiving, 1 = very forgiving (default 0.65)");

            PlasmaSpawnrate = Config.Bind("Plasma Turret", "Spawnrate", 1.0f, "How often do these turrets spawn? (default 1.0)");
            PlasmaWindupTime = Config.Bind("Plasma Turret", "Windup Time", 1.45f, "How long a plasma turret takes before it starts firing projectiles at you (in seconds). (default 1.45)");
            PlasmaReloadTime = Config.Bind("Plasma Turret", "Reload Time", 7f, "How long it takes for a plasma turret to reload (in seconds). (default 7)");
            PlasmaTargetRange = Config.Bind("Plasma Turret", "Range", 25f, "How far away a plasma turret can see you. They can't see through walls though. (default 25)");
            PlasmaBallsPerBurst = Config.Bind("Plasma Turret", "Burst Quantity", 3, "How many plasma balls are fired per burst?");
            PlasmaBurstQuantity = Config.Bind("Plasma Turret", "Bursts per reload", 3, "How many bursts are fired before reloading?");
            PlasmaProjDelay = Config.Bind("Plasma Turret", "Delay per projectile", 0.3f, "What is the delay between each plasma ball in a burst (in seconds)? (default 0.3)");
            PlasmaBurstDelay = Config.Bind("Plasma Turret", "Delay per Burst", 1.3f, "What is the delay between each burst of plasma (in seconds)? (default 1.3)");
            PlasmaProjectileSpeed = Config.Bind("Plasma Turret", "Projectile Launch Speed", 14f, "How fast are the plasma balls launched from this turret? Assume you are setting a speed in meters per second. (default 14)");
            PlasmaTurretVolume = Config.Bind("Plasma Turret", "Turret Volume", 0.65f, "How loud are all sounds from this turret and its projectiles? (default 0.65)");
            PlasmaBallLifetime = Config.Bind("Plasma Turret", "Plasma Ball Lifetime", 4f, "Lifetime of a plasma ball (in seconds). (default 4). Plasma balls are very bouncy, so higher values will give them more bounces and cause more chaos.");
            PlasmaRotationSpeed = Config.Bind("Plasma Turret", "Rotation Speed", 67f, "How quickly does the Plasma Turret rotate to face its target (degrees per second)? The lower the value, the easier it is to outmaneuver. (default 67)");

            var FlameSpawnrateEntry = new FloatInputFieldConfigItem(FlameSpawnrate, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });

            var FlameDmgMultEntry = new FloatSliderConfigItem(FlameDmgMult, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = -5,
                Max = 5,
            });

            var FlameTargetRangeEntry = new FloatSliderConfigItem(FlameTargetRange, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100,
            });

            var FlameRisingTimeEntry = new FloatSliderConfigItem(FlameRisingTime, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 10,
            });


            var FlameRestingTimeEntry = new FloatSliderConfigItem(FlameRestingTime, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 20,
            });

            var FlameSpinningTimeEntry = new FloatSliderConfigItem(FlameSpinningTime, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 20,
            });

            var FlameRotationSpeedEntry = new FloatSliderConfigItem(FlameRotationSpeed, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1080,
            });

            var FlameVolumeEntry = new FloatSliderConfigItem(FlameVolume, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 2,
            });

            var SmokeCooldownEntry = new FloatInputFieldConfigItem(SmokeCooldown, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 6f,
                Max = 99999999999,
            });

            LethalConfigManager.AddConfigItem(FlameSpawnrateEntry);
            LethalConfigManager.AddConfigItem(FlameDmgMultEntry);
            LethalConfigManager.AddConfigItem(SmokeCooldownEntry);
            LethalConfigManager.AddConfigItem(FlameTargetRangeEntry);
            LethalConfigManager.AddConfigItem(FlameRisingTimeEntry);
            LethalConfigManager.AddConfigItem(FlameRestingTimeEntry);
            LethalConfigManager.AddConfigItem(FlameSpinningTimeEntry);
            LethalConfigManager.AddConfigItem(FlameRotationSpeedEntry);
            LethalConfigManager.AddConfigItem(FlameVolumeEntry);

            var SawSpawnrateEntry = new FloatInputFieldConfigItem(SawSpawnrate, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });

            var SawDmgMultEntry = new FloatSliderConfigItem(SawDmgMult, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = -5,
                Max = 5,
            });

            var SawTargetRangeEntry = new FloatSliderConfigItem(SawTargetRange, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100,
            });

            var SawWindupTimeEntry = new FloatSliderConfigItem(SawWindupTime, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 10,
            });


            var SawFirerateEntry = new FloatInputFieldConfigItem(SawFirerate, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var SawVolumeEntry = new FloatSliderConfigItem(SawVolume, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 2,
            });


            var SawAmmoEntry = new IntInputFieldConfigItem(SawAmmo, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 100000000,
            });

            var SawReloadTimeEntry = new FloatInputFieldConfigItem(SawReloadTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });


            var SawLaunchSpeedEntry = new FloatInputFieldConfigItem(SawLaunchSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var SawRotationSpeedEntry = new FloatInputFieldConfigItem(SawRotationSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 3600,
            });

            LethalConfigManager.AddConfigItem(SawSpawnrateEntry);
            LethalConfigManager.AddConfigItem(SawDmgMultEntry);
            LethalConfigManager.AddConfigItem(SawTargetRangeEntry);
            LethalConfigManager.AddConfigItem(SawWindupTimeEntry);
            LethalConfigManager.AddConfigItem(SawFirerateEntry);
            LethalConfigManager.AddConfigItem(SawAmmoEntry);
            LethalConfigManager.AddConfigItem(SawReloadTimeEntry);
            LethalConfigManager.AddConfigItem(SawLaunchSpeedEntry);
            LethalConfigManager.AddConfigItem(SawRotationSpeedEntry);
            LethalConfigManager.AddConfigItem(SawVolumeEntry);

            var LRADSpawnrateEntry = new FloatInputFieldConfigItem(LRADSpawnrate, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });

            var LRADTargetRangeEntry = new FloatInputFieldConfigItem(LRADTargetRange, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var LRADDmgMultEntry = new FloatSliderConfigItem(LRADDmgMult, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = -5,
                Max = 5,
            });

            var LRADProjectileSpeedEntry = new FloatInputFieldConfigItem(LRADProjectileSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 9999999,
            });

            var LRADChargeTimeEntry = new FloatInputFieldConfigItem(LRADChargeTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 7.2f,
            });

            var LRADReloadTimeEntry = new FloatInputFieldConfigItem(LRADReloadTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 999999,
            });

            var LRADDisorientPeriodEntry = new FloatInputFieldConfigItem(LRADDisorientPeriod, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0.5f,
                Max = 1000000,
            });

            var LRADDizzyMultEntry = new FloatInputFieldConfigItem(LRADDizzyMult, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1000,
            });

            var LRADDrunknessMultEntry = new FloatSliderConfigItem(LRADDrunknessMult, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 5,
            });

            var LRADFXMultEntry = new FloatSliderConfigItem(LRADFXMult, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 10,
            });

            var LRADVolumeEntry = new FloatSliderConfigItem(LRADVolume, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 2f,
            });

            var LRADFXVolumeEntry = new FloatSliderConfigItem(LRADFXVolume, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 2f,
            });

            var LRADRotationSpeedEntry = new FloatInputFieldConfigItem(LRADRotationSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 3600,
            });

            LethalConfigManager.AddConfigItem(LRADSpawnrateEntry);
            LethalConfigManager.AddConfigItem(LRADTargetRangeEntry);
            LethalConfigManager.AddConfigItem(LRADDmgMultEntry);
            LethalConfigManager.AddConfigItem(LRADChargeTimeEntry);
            LethalConfigManager.AddConfigItem(LRADReloadTimeEntry);
            LethalConfigManager.AddConfigItem(LRADProjectileSpeedEntry);
            LethalConfigManager.AddConfigItem(LRADDisorientPeriodEntry);
            LethalConfigManager.AddConfigItem(LRADDizzyMultEntry);
            LethalConfigManager.AddConfigItem(LRADDrunknessMultEntry);
            LethalConfigManager.AddConfigItem(LRADFXMultEntry);
            LethalConfigManager.AddConfigItem(LRADRotationSpeedEntry);
            LethalConfigManager.AddConfigItem(LRADVolumeEntry);
            LethalConfigManager.AddConfigItem(LRADFXVolumeEntry);


            var MouseTrapSpawnrateEntry = new FloatInputFieldConfigItem(MouseTrapSpawnrate, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });
            var MouseTrapBigEntry = new FloatInputFieldConfigItem(BigMTrapChance, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100,
            });
            var MouseTrapGiantEntry = new FloatInputFieldConfigItem(GiantMTrapChance, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100,
            });

            var SmallMTrapDmgEntry = new IntInputFieldConfigItem(SmallMTrapDmg, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = -1000,
                Max = 1000,
            });

            var BigMTrapDmgEntry = new IntInputFieldConfigItem(BigMTrapDmg, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = -1000,
                Max = 1000,
            });

            var GiantMTrapDmgEntry = new IntInputFieldConfigItem(GiantMTrapDmg, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = -1000,
                Max = 1000,
            });

            var SmallMTrapAttachesEntry = new BoolCheckBoxConfigItem(SmallMTrapAttaches, new BoolCheckBoxOptions
            {
                RequiresRestart = false,
            });

            var AttachmentWepRequiredEntry = new BoolCheckBoxConfigItem(AttachmentWepRequired, new BoolCheckBoxOptions
            {
                RequiresRestart = false,
            });

            var MTrapCanBeDisabledEntry = new BoolCheckBoxConfigItem(MTrapCanBeDisabled, new BoolCheckBoxOptions
            {
                RequiresRestart = false,
            });

            var MTrapWhitelistEntry = new TextInputFieldConfigItem(MTrapWhitelist, new TextInputFieldOptions
            {
                RequiresRestart = false,
            });

            var MTrapBaitEntry = new FloatInputFieldConfigItem(MTrapScrapBaitForgiveness, new FloatInputFieldOptions
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(MouseTrapSpawnrateEntry);
            LethalConfigManager.AddConfigItem(MouseTrapBigEntry);
            LethalConfigManager.AddConfigItem(MouseTrapGiantEntry);
            LethalConfigManager.AddConfigItem(SmallMTrapDmgEntry);
            LethalConfigManager.AddConfigItem(BigMTrapDmgEntry);
            LethalConfigManager.AddConfigItem(GiantMTrapDmgEntry);
            LethalConfigManager.AddConfigItem(SmallMTrapAttachesEntry);
            LethalConfigManager.AddConfigItem(AttachmentWepRequiredEntry);
            LethalConfigManager.AddConfigItem(MTrapCanBeDisabledEntry);
            LethalConfigManager.AddConfigItem(MTrapWhitelistEntry);
            LethalConfigManager.AddConfigItem(MTrapBaitEntry);


            var PlasmaSpawnrateEntry = new FloatInputFieldConfigItem(PlasmaSpawnrate, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });

            var PlasmaWindupTimeEntry = new FloatInputFieldConfigItem(PlasmaWindupTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1000,
            });

            var PlasmaReloadTimeEntry = new FloatInputFieldConfigItem(PlasmaReloadTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1000,
            });


            var PlasmaTargetRangeEntry = new FloatInputFieldConfigItem(PlasmaTargetRange, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1000,
            });

            var PlasmaTurretVolumeEntry = new FloatInputFieldConfigItem(PlasmaTurretVolume, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1,
            });

            var PlasmaProjectileSpeedEntry = new FloatInputFieldConfigItem(PlasmaProjectileSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var PlasmaBallsPerBurstEntry = new IntInputFieldConfigItem(PlasmaBallsPerBurst, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 100000000,
            });

            var PlasmaBurstQuantityEntry = new IntInputFieldConfigItem(PlasmaBurstQuantity, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 100000000,
            });

            var PlasmaBurstDelayEntry = new FloatInputFieldConfigItem(PlasmaBurstDelay, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var PlasmaProjDelayEntry = new FloatInputFieldConfigItem(PlasmaProjDelay, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var PlasmaBallLifetimeEntry = new FloatInputFieldConfigItem(PlasmaBallLifetime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var PlasmaRotationSpeedEntry = new FloatInputFieldConfigItem(PlasmaRotationSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 10000,
            });

            LethalConfigManager.AddConfigItem(PlasmaSpawnrateEntry);
            LethalConfigManager.AddConfigItem(PlasmaTargetRangeEntry);
            LethalConfigManager.AddConfigItem(PlasmaWindupTimeEntry);
            LethalConfigManager.AddConfigItem(PlasmaReloadTimeEntry);
            LethalConfigManager.AddConfigItem(PlasmaProjectileSpeedEntry);
            LethalConfigManager.AddConfigItem(PlasmaBallsPerBurstEntry);
            LethalConfigManager.AddConfigItem(PlasmaBurstQuantityEntry);
            LethalConfigManager.AddConfigItem(PlasmaBurstDelayEntry);
            LethalConfigManager.AddConfigItem(PlasmaProjDelayEntry);
            LethalConfigManager.AddConfigItem(PlasmaRotationSpeedEntry);
            LethalConfigManager.AddConfigItem(PlasmaBallLifetimeEntry);
            LethalConfigManager.AddConfigItem(PlasmaTurretVolumeEntry);
        }

        public static void PopulateAssets()
        {
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            TrapBundle = AssetBundle.LoadFromFile(Path.Combine(sAssemblyLocation, "bcs_devioustraps"));

            Debug.Log("DeviousTraps Bundle::: " + TrapBundle);
        }
    }
}