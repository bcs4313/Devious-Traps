using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using LethalConfig;
using DeviousTraps;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using System.Diagnostics;
using UnityEngine;
using LethalLevelLoader;  // bah humbug

namespace DeviousTraps.src.Technical
{
    /**
     * The Spawn Manager is a system that takes in the names of moons and interiors with their corresponding weights, and then
     * shifts the spawnrate of traps in response to represent the combined interior and moon.
     * 
     * This allows people to have full control over the spawning of all traps in devious traps.
    */
    public class SpawnManager
    {
        // spawn manager will not run if this is false
        public static bool Enabled => Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader");

        // Section for dynamically setting spawn weights
        // This runs once on level load, before traps spawn.
        public static void SetTrapWeights()
        {
            if(!Enabled) { return; }  // this setting only applies with LLL (applies to almost ALL modpacks so...)  

            // we are attempting to match to these!
            String levelName = RoundManager.Instance.currentLevel.PlanetName.ToLower().Trim();
            String levelName2 = RoundManager.Instance.currentLevel.name.ToLower().Trim();

            // finding the exact dungeon name as a flow and as defined in lethal level loader
            String dungeonFlowNameToMatch = RoundManager.Instance.dungeonGenerator.Generator.DungeonFlow.name;  // MAP to RM
            String dungeonName = "";
            var extendedFlows = UnityEngine.Object.FindObjectsOfType<ExtendedDungeonFlow>();
            foreach(var flow in extendedFlows)
            {
                if(flow.name.ToLower().Trim().Equals(dungeonFlowNameToMatch.ToLower().Trim()))
                {
                    dungeonName = flow.DungeonName.ToLower().Trim();
                }
            }

            // error case
            if(dungeonName == null || dungeonName.Equals(""))
            {
                UnityEngine.Debug.LogError("Devious Traps Dynamic Spawn Error: Couldn't find a matching dungeon name to a dungeon flow! Dynamic spawning will not work!");
            }

            // all of these are affected by the config (prefab name)
            var targets = new List<String>() { "sawturrettrap","flameturret","lrad","mortarturretprefab","mousetrapspawner","plasmaturret" };

            foreach(String targetTurret in targets)  // target turret reflects in-game prefab name
            {
                String[] moonConfigEntries = [];
                String[] interiorConfigEntries = [];
                try
                {
                    switch (targetTurret)
                    {
                        case "sawturretrap":
                            moonConfigEntries = SawTurretMoonSpawnrates.Value.Trim().ToLower().Split(",");
                            interiorConfigEntries = SawTurretInteriorSpawnrates.Value.Trim().ToLower().Split(",");
                            break;
                        case "flameturret":
                            moonConfigEntries = FlameTurretMoonSpawnrates.Value.Trim().ToLower().Split(",");
                            interiorConfigEntries = FlameTurretInteriorSpawnrates.Value.Trim().ToLower().Split(",");
                            break;
                        case "lrad":
                            moonConfigEntries = SoundTurretMoonSpawnrates.Value.Trim().ToLower().Split(",");
                            interiorConfigEntries = SoundTurretInteriorSpawnrates.Value.Trim().ToLower().Split(",");
                            break;
                        case "mortarturretprefab":
                            moonConfigEntries = MortarTurretMoonSpawnrates.Value.Trim().ToLower().Split(",");
                            interiorConfigEntries = MortarTurretInteriorSpawnrates.Value.Trim().ToLower().Split(",");
                            break;
                        case "mousetrapspawner":
                            moonConfigEntries = MouseTrapMoonSpawnrates.Value.Trim().ToLower().Split(",");
                            interiorConfigEntries = MouseTrapInteriorSpawnrates.Value.Trim().ToLower().Split(",");
                            break;
                        case "plasmaturret":
                            moonConfigEntries = PlasmaTurretMoonSpawnrates.Value.Trim().ToLower().Split(",");
                            interiorConfigEntries = PlasmaTurretInteriorSpawnrates.Value.Trim().ToLower().Split(",");
                            break;
                        default:
                            UnityEngine.Debug.LogError("Devious Traps Error: Could not find the turret prefab of target " + targetTurret + ". This turret will not have dynamic spawnrates!");
                            break;
                    }
                }
                catch(Exception e)
                {
                    UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error: " + e + " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
                }

                // moon parsing
                foreach (String entry in moonConfigEntries)
                {
                    try
                    {
                        String[] pair = entry.Split(":");
                        String currentMoon = pair[0];
                        float weight = float.Parse(pair[1]);

                        // apply weight if this config entry matches the current level:
                        if (levelName.Contains(currentMoon.ToLower().Trim()) || levelName2.Contains(currentMoon.ToLower().Trim()))
                        {
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }
                    }
                    catch(Exception e)
                    {
                        UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error: " + e + " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
                    }
                }

                // interior parsing
                foreach (String entry in interiorConfigEntries)
                {
                    try
                    {
                        String[] pair = entry.Split(":");
                        String currentInterior = pair[0];
                        float weight = float.Parse(pair[1]);

                        // apply weight if this config entry matches the current level:
                        if (dungeonName.Contains(currentInterior.ToLower().Trim()))
                        {
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error: " + e + " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
                    }
                }
            }
        }

        // dig into the level, find the turret by prefab name,
        // and increase the max keyframe by the target weight
        public static void ApplyWeightToSpawnCurve(String prefabName, float weight)
        {
            try
            {
                var level = RoundManager.Instance.currentLevel;
                foreach(IndoorMapHazard indoorType in level.indoorMapHazards)
                {
                    if (indoorType.hazardType.prefabToSpawn.name.ToLower().Trim().Equals(prefabName))  // finally, we can apply the weight!
                    {
                        // key 1 (assuming index 0 exists) is the target
                        indoorType.numberToSpawn.GetKeys()[1].m_InWeight *= weight;
                    }
                }
            }
            catch(Exception e)
            {
                UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error (ApplyWeightToSpawnCurve): " + e + " - " 
                    + prefabName + " with weight: " + weight +  " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
            }
        }

        // resets the spawn curves of all turrets before multipliers are added
        // matching with the prefab's actual name in game
        public void EstablishStarterWeights(String[] turretTargets)
        {
            IndoorMapHazard[] hazardList = RoundManager.Instance.currentLevel.indoorMapHazards;  // by technicality, all hazards are quote-unquote indoor

            // convert map hazard list to simple prefab name list
            // I don't like the nesting but its not too terrible (for me)
            foreach(IndoorMapHazard hazard in hazardList)
            {
                // null safety
                if(hazard == null || hazard.hazardType == null || hazard.hazardType.prefabToSpawn == null) { continue; }
                String prefabName = hazard.hazardType.prefabToSpawn.name.Trim().ToLower();
                foreach (String targetTurret in turretTargets)
                {
                    if(targetTurret.Equals(prefabName))
                    {
                        // reset to base curve
                        hazard.numberToSpawn = GenerateBaseCurve(prefabName);
                    }
                }
            }
        }   

        public AnimationCurve GenerateBaseCurve(String prefabName)
        {
            var minTurrets = 0;
            var maxTurrets = 4.8 * GetBaseSpawnWeight(prefabName);
            AnimationCurve curve = new AnimationCurve(new Keyframe[]
{
                    new Keyframe(0f, (float)minTurrets, 0.267f, 0.267f, 0f, 0.246f),  // min turret reff from missile turret = 0
                    new Keyframe(1f, (float)maxTurrets, 61f, 61f, 0.015f * (float)maxTurrets, 0f)  // max turret ref from missile turret = 6
            });
            return curve;
        }

        public float GetBaseSpawnWeight(String prefabName)
        {
            switch (prefabName)
            {
                case "sawturretrap":
                    return Plugin.SawSpawnrate.Value;
                case "flameturret":
                    return Plugin.FlameSpawnrate.Value;
                case "lrad":
                    return Plugin.LRADSpawnrate.Value;
                case "mortarturretprefab":
                    return MortarConfig.MortarSpawnrate.Value;
                case "mousetrapspawner":
                    return Plugin.MouseTrapSpawnrate.Value;
                case "plasmaturret":
                    return Plugin.PlasmaSpawnrate.Value;
                default:
                    UnityEngine.Debug.LogError("Devious Traps Error: (GetBaseSpawnWeight) " +
                        "Could not find the turret prefab of target " + prefabName + ". This turret will not have dynamic spawnrates!");
                    break;
            }
            return 1.0f;  // we should never reach this line
        }

        /**
         * 
         * for reference as base weights
         * 
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

        They all work off a 4.8 * Plugin.<turret>Spawnrate.Value system. Curves are overidden by the spawn manager
         * */

        /*
        public struct MoonWeightRelationship
        {
            // matching to:
            public String MoonName = "?";
            public String MoonConsoleName = "?";
            public string

            // weight multiplier to spawnrate
            public float WeightMultplier = 1.0f;

            public MoonWeightRelationship() { }
        }

        public struct DungeonWeightRelationship
        {
            // matching to:
            public String DungeonName = "?";

            // weight multiplier to spawnrate
            public float WeightMultplier = 1.0f;

            public DungeonWeightRelationship() { }
        }

        public static MoonWeightRelationship getMoonRelationship(String levelName1, String levelName2)
        {
            var moons = UnityEngine.Object.FindObjectsOfType<SelectableLevel>();

            foreach(var moon in moons)
            {
                if(levelName1 == moon.PlanetName || levelName2)
                var rel = new MoonWeightRelationship();
                rel.MoonName = moon.name;
                rel.MoonConsoleName = moon.PlanetName;
                moonRels.Add(rel);
            }
            return moonRels;
        }
        */

        // Lethal Config Section
        public static ConfigEntry<string> GENERALINFO;
        
        public static ConfigEntry<string> SawTurretMoonSpawnrates;
        public static ConfigEntry<string> SawTurretInteriorSpawnrates;

        public static ConfigEntry<string> FlameTurretMoonSpawnrates;
        public static ConfigEntry<string> FlameTurretInteriorSpawnrates;

        public static ConfigEntry<string> SoundTurretMoonSpawnrates;
        public static ConfigEntry<string> SoundTurretInteriorSpawnrates;

        public static ConfigEntry<string> PlasmaTurretMoonSpawnrates;
        public static ConfigEntry<string> PlasmaTurretInteriorSpawnrates;

        public static ConfigEntry<string> MortarTurretMoonSpawnrates;
        public static ConfigEntry<string> MortarTurretInteriorSpawnrates;

        public static ConfigEntry<string> MouseTrapMoonSpawnrates;
        public static ConfigEntry<string> MouseTrapInteriorSpawnrates;

        // only for creating the Lethal Config widgets for entering custom spawn weights for moons AND interiors
        public static void SetUpSettings(Plugin pluginRef)
        {

            GENERALINFO = pluginRef.Config.Bind("Dynamic Spawnrates", "General Info -IMPORTANT-", "", "This setting doesn't do anything, but it gives some context on how the settings work. These settings are host only and do NOT require a restart. You need LethalLevelLoader installed for it to work." +
                "The only spawnrate entries that require a restart are the base spawnrates of a turret found in categories separate from this one. Moon and interior weights multiply to the base turret spawnrate if applicable in your configuration." +
                "Just enter simple comma separated lists with colon separators, such as this: Vanilla:0.8,Modded:1.3,Experimentation:2. The values should be treated as multipliers to the base spawnrate, NOT as individual rarity values. (ignoring this will result" +
                "in many, MANY turrets spawning absolutely everywhere)");


            var GENERALINFOEntry = new TextInputFieldConfigItem(GENERALINFO, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(GENERALINFOEntry);

            // saw turret section
            SawTurretMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Saw Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the saw turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            SawTurretInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Saw Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to the saw turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader." +
                "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            var SawTurretMoonpawnratesEntry = new TextInputFieldConfigItem(SawTurretMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var SawTurretInteriorSpawnratesEntry = new TextInputFieldConfigItem(SawTurretInteriorSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(SawTurretMoonpawnratesEntry);
            LethalConfigManager.AddConfigItem(SawTurretInteriorSpawnratesEntry);

            // flame turret section
            FlameTurretMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Flame Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the flame turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            FlameTurretInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Flame Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to the flame turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader." +
                "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            var FlameTurretMoonpawnratesEntry = new TextInputFieldConfigItem(FlameTurretMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var FlameTurretInteriorSpawnratesEntry = new TextInputFieldConfigItem(FlameTurretInteriorSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(FlameTurretMoonpawnratesEntry);
            LethalConfigManager.AddConfigItem(FlameTurretInteriorSpawnratesEntry);


            // Sound turret section
            SoundTurretMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Sound Cannon (LRAD) Moon Weights", "", "Moon Spawn Weights that dynamically apply to the sound Cannon. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            SoundTurretInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Sound Cannon (LRAD) Interior Weights", "", "Interior Spawn Weights that dynamically apply to the sound Cannon. " +
                "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader." +
                "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            var SoundTurretMoonpawnratesEntry = new TextInputFieldConfigItem(SoundTurretMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var SoundTurretInteriorSpawnratesEntry = new TextInputFieldConfigItem(SoundTurretInteriorSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(SoundTurretMoonpawnratesEntry);
            LethalConfigManager.AddConfigItem(SoundTurretInteriorSpawnratesEntry);

            // Plasma turret section
            PlasmaTurretMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Plasma Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the plasma turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            PlasmaTurretInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Plasma Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to the plasma turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader." +
                "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            var PlasmaTurretMoonpawnratesEntry = new TextInputFieldConfigItem(PlasmaTurretMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var PlasmaTurretInteriorSpawnratesEntry = new TextInputFieldConfigItem(PlasmaTurretInteriorSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(PlasmaTurretMoonpawnratesEntry);
            LethalConfigManager.AddConfigItem(PlasmaTurretInteriorSpawnratesEntry);

            // Mortar turret section
            MortarTurretMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Mortar Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the mortar turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            MortarTurretInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Mortar Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to the mortar turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader." +
                "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            var MortarTurretMoonpawnratesEntry = new TextInputFieldConfigItem(MortarTurretMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var MortarTurretInteriorSpawnratesEntry = new TextInputFieldConfigItem(MortarTurretInteriorSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(MortarTurretMoonpawnratesEntry);
            LethalConfigManager.AddConfigItem(MortarTurretInteriorSpawnratesEntry);

            // Mouse Trap section
            MouseTrapMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Mouse Trap Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the mouse traps. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            MouseTrapInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Mouse Trap Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to mouse traps. " +
                "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader." +
                "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            var MouseTrapMoonpawnratesEntry = new TextInputFieldConfigItem(MouseTrapMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var MouseTrapInteriorSpawnratesEntry = new TextInputFieldConfigItem(MouseTrapInteriorSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(MouseTrapMoonpawnratesEntry);
            LethalConfigManager.AddConfigItem(MouseTrapInteriorSpawnratesEntry);
        }
    }
}
