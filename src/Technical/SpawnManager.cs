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
            String levelName = RoundManager.Instance.currentLevel.PlanetName;
            String levelName2 = RoundManager.Instance.currentLevel.name;

            // finding the exact dungeon name as a flow and as defined in lethal level loader
            String dungeonFlowNameToMatch = RoundManager.Instance.dungeonGenerator.Generator.DungeonFlow.name;  // MAP to RM
            String dungeonName = "";
            var extendedFlows = UnityEngine.Object.FindObjectsOfType<ExtendedDungeonFlow>();
            foreach(var flow in extendedFlows)
            {
                if(flow.name.ToLower().Trim().Equals(dungeonFlowNameToMatch.ToLower().Trim()))
                {
                    dungeonName = flow.DungeonName;
                }
            }

            // error case
            if(dungeonName == null || dungeonName.Equals(""))
            {
                UnityEngine.Debug.LogError("Devious Traps Dynamic Spawn Error: Couldn't find a matching dungeon name to a dungeon flow! Dynamic spawning will not work!");
            }

            // all of these are affected by the config
            var targets = new List<String>() { "sawturrettrap","flameturret","lrad","mortarturretprefab","mousetrapspawner","plasmaturret" };

            foreach(String target in targets)
            {
                String[] moonConfigEntries = [];
                String[] interiorConfigEntries = [];
                try
                {
                    switch (target)
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
                            UnityEngine.Debug.LogError("Devious Traps Error: Could not find the turret prefab of target " + target + ". This turret will not have dynamic spawnrates!");
                            break;
                    }
                }
                catch(Exception e)
                {
                    UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error: " + e + " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
                }

                // moon parsing
                foreach(String pair in moonConfigEntries)
                {
                    LethalLevelLoader.
                }

                // interior parsing
                foreach (String pair in interiorConfigEntries)
                {

                }
            }
        }

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

            var SoundTurretMoonpawnratesEntry = new TextInputFieldConfigItem(FlameTurretMoonSpawnrates, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            var SoundTurretInteriorSpawnratesEntry = new TextInputFieldConfigItem(FlameTurretInteriorSpawnrates, new TextInputFieldOptions()
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
            MouseTrapMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Mortar Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the mouse traps. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            MouseTrapInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Mortar Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to mouse traps. " +
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
