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
        private static void Log(String message)
        {
            if (debugMode.Value == true) { UnityEngine.Debug.Log("[DeviousTraps SpawnManager] " + message); }
        }

        private static void LogWarn(String message)
        {
            if (debugMode.Value == true) { UnityEngine.Debug.LogWarning("[DeviousTraps SpawnManager] " + message); }
        }

        // spawn manager will not run if this is false
        public static bool Enabled => Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader");

        // Section for dynamically setting spawn weights
        // This runs once on level load, before traps spawn.
        public static void SetTrapWeights()
        {
            // all of these are affected by the config (prefab name)
            var targets = new List<String>() { "sawturrettrap", "flameturret", "lrad", "mortarturretprefab", "mousetrapspawner", "plasmaturret" };
            EstablishStarterWeights(targets);
            
            if (!Enabled) { Log("LethalLevelLoader not present, skipping dynamic spawn weights."); return; }  // this setting only applies with LLL (applies to almost ALL modpacks so...)  

            Log($"Starting SetTrapWeights() for devioustraps:");

            // we are attempting to match to these!
            String levelName = RoundManager.Instance.currentLevel.PlanetName.ToLower().Trim();
            String levelName2 = RoundManager.Instance.currentLevel.name.ToLower().Trim();
            Log($"Matching against level names: '{levelName}' / '{levelName2}'");

            List<String> moonTags = GetContentTagStringsOfLevel(levelName);

            // finding the exact dungeon name as a flow and as defined in lethal level loader
            String dungeonFlowNameToMatch = RoundManager.Instance.dungeonGenerator.Generator.DungeonFlow.name;  // MAP to RM
            String dungeonName = "";
            var extendedFlows = UnityEngine.Object.FindObjectsOfType<ExtendedDungeonFlow>();
            foreach (var flow in extendedFlows)
            {
                Log($"Found ExtendedDungeonFlow: '{flow.name}' -> DungeonName '{flow.DungeonName}'" + " -> " + flow.DungeonFlow.name);
                if (flow.name.ToLower().Trim().Equals(dungeonFlowNameToMatch.ToLower().Trim()) ||
                    flow.DungeonFlow.name.ToLower().Trim().Equals(dungeonFlowNameToMatch.ToLower().Trim()))
                {
                    dungeonName = flow.DungeonName.ToLower().Trim();
                }
            }
            Log($"Resolved dungeon flow '{dungeonFlowNameToMatch}' -> dungeon name '{dungeonName}'");

            List<String> dungeonTags = GetContentTagStringsOfDungeon(dungeonName);

            // error case
            if (dungeonName == null || dungeonName.Equals(""))
            {
                UnityEngine.Debug.LogError("Devious Traps Dynamic Spawn Error: Couldn't find a matching dungeon name to a dungeon flow! Dynamic spawning will not work!");
            }

            foreach (String targetTurret in targets)  // target turret reflects in-game prefab name
            {
                String[] moonConfigEntries = [];
                String[] interiorConfigEntries = [];
                try
                {
                    switch (targetTurret)
                    {
                        case "sawturrettrap":
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
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error: " + e + " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
                }

                // moon parsing
                for (int i = 0; i < moonConfigEntries.Length; i++)
                {
                    String entry = moonConfigEntries[i];
                    Log("Looking at entry -> " + entry);
                    try
                    {
                        String[] pair = entry.Split(":");
                        String currentMoon = pair[0].ToLower().Trim();
                        float weight = float.Parse(pair[1]);

                        // apply weight if this config entry matches the current level:
                        if (levelName.Contains(currentMoon.ToLower().Trim()) || levelName2.Contains(currentMoon.ToLower().Trim()))
                        {
                            Log($"[{targetTurret}] moon entry '{entry}' matched -- applying weight {weight}");
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }

                        // apply weight if this config entry matches a corresponding moon tag
                        if (moonTags != null && moonTags.Contains(currentMoon))
                        {
                            Log($"[{targetTurret}] moon entry '{entry}' matched (content tag) -- applying weight {weight}");
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }

                        // apply weight if this config entry is "modded" and the moon is not vanilla
                        if (currentMoon.Contains("modded"))
                        {
                            if(moonIsModded(levelName)) 
                            {
                                Log($"Applying weight for {targetTurret}, tag:modded weight {weight}");
                                ApplyWeightToSpawnCurve(targetTurret, weight); 
                            }
                        }

                        // apply weight if this config entry is "vanilla" and the moon is vanilla
                        if (currentMoon.Contains("vanilla"))
                        {
                            if (!moonIsModded(levelName)) 
                            {
                                Log($"Applying weight for {targetTurret}, tag:vanilla weight {weight}");
                                ApplyWeightToSpawnCurve(targetTurret, weight); 
                            }
                        }

                        // apply weight if this config entry is "all" and the moon is vanilla
                        if (currentMoon.Contains("all"))
                        {
                            Log($"Applying weight for {targetTurret}, tag:all weight {weight}");
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }
                    }
                    catch (Exception e)
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
                        String currentInterior = pair[0].ToLower().Trim();
                        float weight = float.Parse(pair[1]);

                        // apply weight if this config entry matches the current level:
                        if (dungeonName.Contains(currentInterior.ToLower().Trim()))
                        {
                            Log($"[{targetTurret}] interior entry '{entry}' matched -- applying weight {weight}");
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }

                        // apply weight if this config entry is "all" and the interior is vanilla
                        if (currentInterior.Contains("all"))
                        {
                            Log($"Applying weight for {targetTurret}, tag:all weight {weight}");
                            ApplyWeightToSpawnCurve(targetTurret, weight);
                        }

                        // apply weight if this config entry matches a corresponding moon tag
                        if (dungeonTags != null && dungeonTags.Contains(currentInterior))
                        {
                            Log($"[{targetTurret}] dungeon entry '{entry}' matched (content tag) -- applying weight {weight}");
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

        public static bool moonIsModded(String PlanetName)
        {
            var ext_levels = UnityEngine.Object.FindObjectsOfType<ExtendedLevel>();
            foreach(var ext in ext_levels)
            {
                if(ext.SelectableLevel.PlanetName.ToLower().Trim() != PlanetName.ToLower().Trim()) { return true; }
            }
            return false;
        }


        public static List<String> GetContentTagStringsOfDungeon(String dungeonName)
        {
            List<ContentTag> tags = null;
            foreach (ExtendedDungeonFlow ext in UnityEngine.Object.FindObjectsOfType<ExtendedDungeonFlow>())
            {
                if (ext.DungeonName == dungeonName)
                {
                    tags = ext.ContentTags;
                    break;
                }
            }

            if (tags == null)
            {
                return null;
            }

            Log("Devious Traps: Parsing content tags...");
            List<String> tagStrings = new List<String>();
            foreach (ContentTag tag in tags)
            {
                tagStrings.Add(tag.contentTagName.ToLower().Trim());
            }

            Log("Devious Traps: Tags found for dungeon: " + tagStrings.ToString());
            return tagStrings;
        }

        public static List<String> GetContentTagStringsOfLevel(String planetName)
        {
            List<ContentTag> tags = null;
            foreach(ExtendedLevel ext in UnityEngine.Object.FindObjectsOfType<ExtendedLevel>())
            {
                if(ext.SelectableLevel.PlanetName == planetName)
                {
                    tags = ext.ContentTags;
                    break;
                }
            }

            if(tags == null)
            {
                return null;
            }

            Log("Devious Traps: Parsing content tags...");
            List<String> tagStrings = new List<String>();
            foreach(ContentTag tag in tags)
            {
                tagStrings.Add(tag.contentTagName.ToLower().Trim());
            }

            Log("Devious Traps: Tags found for dungeon: " + tagStrings.ToString());
            return tagStrings;
        }

        // dig into the level, find the turret by prefab name,
        // and increase the max keyframe by the target weight
        public static void ApplyWeightToSpawnCurve(String prefabName, float weight)
        {
            try
            {
                var level = RoundManager.Instance.currentLevel;
                bool foundMatch = false;
                foreach (IndoorMapHazard indoorType in level.indoorMapHazards)
                {
                    if (indoorType.hazardType.prefabToSpawn.name.ToLower().Trim().Equals(prefabName))  // finally, we can apply the weight!
                    {
                        foundMatch = true;
                        // key 1 (assuming index 0 exists) is the target
                        Keyframe[] keys = indoorType.numberToSpawn.GetKeys(); // real local array, you own it now
                        keys[1].value *= weight;                         // mutating index 1 of YOUR array — this sticks
                        indoorType.numberToSpawn.SetKeys(keys);                 // write the whole array back into the curve
                    }
                }
                if (!foundMatch) { LogWarn($"ApplyWeightToSpawnCurve: no indoorMapHazard found for prefab '{prefabName}' on this level."); }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Devious Traps Dynamic Spawnrate Error (ApplyWeightToSpawnCurve): " + e + " - "
                    + prefabName + " with weight: " + weight + " -- Please check your Dynamic Spawnrate config for typos / human error. If not applicable you may report it.");
            }
        }

        // resets the spawn curves of all turrets before multipliers are added
        // matching with the prefab's actual name in game
        public static void EstablishStarterWeights(List<String> turretTargets)
        {
            IndoorMapHazard[] hazardList = RoundManager.Instance.currentLevel.indoorMapHazards;  // by technicality, all hazards are quote-unquote indoor

            // convert map hazard list to simple prefab name list
            // I don't like the nesting but its not too terrible (for me)
            foreach (IndoorMapHazard hazard in hazardList)
            {
                // null safety
                if (hazard == null || hazard.hazardType == null || hazard.hazardType.prefabToSpawn == null) { continue; }
                String prefabName = hazard.hazardType.prefabToSpawn.name.Trim().ToLower();
                foreach (String targetTurret in turretTargets)
                {
                    if (targetTurret.Equals(prefabName))
                    {
                        // reset to base curve
                        Log($"Resetting '{prefabName}' to its base spawn curve.");
                        hazard.numberToSpawn = GenerateBaseCurve(prefabName);
                    }
                }
            }
        }

        public static AnimationCurve GenerateBaseCurve(String prefabName)
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

        public static float GetBaseSpawnWeight(String prefabName)
        {
            switch (prefabName)
            {
                case "sawturrettrap":
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

        // Tooltip templates -- {0} gets replaced with the turret's display name (e.g. "saw turret").
        // The original had this paragraph pasted six times with only the turret name edited each time;
        // this is the same text, just written once.
        private const String MoonTooltipTemplate =
            "Moon Spawn Weights that dynamically apply to the {0}. " +
            "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
            "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. LLL Moon Tags are accepted here.";

        private const String InteriorTooltipTemplate =
            "Interior Spawn Weights that dynamically apply to the {0}. " +
            "Enter a comma separated list of pairs, each pair should follow the format of interiorName:spawnrateMultiplier. You can partially enter an interior's name and it can be accepted. Based on the Dungeon Name value inside LethalLeverLoader. " +
            "Example 1: entering 'circus' for 'Circus Facility' is valid. Example 2: entering 'House' for 'liminal house' is valid. Example 3: entering 'Castle Grounds' for the 'Peachs Castle' interior is NOT valid. Values are not case sensitive or space sensitive. " +
            "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. Only the keyword All and LLL Dungeon Tags are accepted.";

        // Binds the moon + interior spawnrate config entries for one turret, wires up its
        // LethalConfig text widgets, and hands the bound entries back via out params so the
        // caller can assign them to that turret's usual static fields. Replaces what used to
        // be six copy-pasted ~25-line blocks (one per turret) with one call per turret.
        private static void BindTurretSpawnrateSettings(Plugin pluginRef, String displayName,
            out ConfigEntry<string> moonEntry, out ConfigEntry<string> interiorEntry)
        {
            moonEntry = pluginRef.Config.Bind("Dynamic Spawnrates", $"{displayName} Moon Weights", "All:1",
                String.Format(MoonTooltipTemplate, displayName.ToLower()));

            interiorEntry = pluginRef.Config.Bind("Dynamic Spawnrates", $"{displayName} Interior Weights", "All:1",
                String.Format(InteriorTooltipTemplate, displayName.ToLower()));

            var moonWidget = new TextInputFieldConfigItem(moonEntry, new TextInputFieldOptions() { RequiresRestart = false });
            var interiorWidget = new TextInputFieldConfigItem(interiorEntry, new TextInputFieldOptions() { RequiresRestart = false });

            LethalConfigManager.AddConfigItem(moonWidget);
            LethalConfigManager.AddConfigItem(interiorWidget);
        }


        public static ConfigEntry<bool> debugMode;
        // only for creating the Lethal Config widgets for entering custom spawn weights for moons AND interiors
        public static void SetUpSettings(Plugin pluginRef)
        {

            GENERALINFO = pluginRef.Config.Bind("Dynamic Spawnrates", "General Info -IMPORTANT-", "", "This setting doesn't do anything, but it gives some context on how the settings work. These settings are host only and do NOT require a restart. You need LethalLevelLoader installed for it to work." +
                "The base spawnrates of a turret found in categories separate from this one (doesn't require a restart). Moon and interior weights multiply to the base turret spawnrate if applicable in your configuration." +
                "Just enter simple comma separated lists with colon separators, such as this: Vanilla:0.8,Modded:1.3,Experimentation:2. The values should be treated as multipliers to the base spawnrate, NOT as individual rarity values. (ignoring this will result" +
                "in many, MANY turrets spawning absolutely everywhere)");


            var GENERALINFOEntry = new TextInputFieldConfigItem(GENERALINFO, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(GENERALINFOEntry);

            debugMode = pluginRef.Config.Bind("Dynamic Spawnrates", "Debug Mode", false, "When true, Devious Traps will write a ton of debug information about the identified moon names, interiors," +
                "and what multipliers were applied to traps as a result. If things aren't working as expected this will certainly assist you!");


            var debugModeEntry = new TextInputFieldConfigItem(GENERALINFO, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(GENERALINFOEntry);
            LethalConfigManager.AddConfigItem(debugModeEntry);
            BindTurretSpawnrateSettings(pluginRef, "Saw Turret", out SawTurretMoonSpawnrates, out SawTurretInteriorSpawnrates);
            BindTurretSpawnrateSettings(pluginRef, "Flame Turret", out FlameTurretMoonSpawnrates, out FlameTurretInteriorSpawnrates);
            BindTurretSpawnrateSettings(pluginRef, "Sound Cannon (LRAD)", out SoundTurretMoonSpawnrates, out SoundTurretInteriorSpawnrates);
            BindTurretSpawnrateSettings(pluginRef, "Plasma Turret", out PlasmaTurretMoonSpawnrates, out PlasmaTurretInteriorSpawnrates);
            BindTurretSpawnrateSettings(pluginRef, "Mortar Turret", out MortarTurretMoonSpawnrates, out MortarTurretInteriorSpawnrates);
            BindTurretSpawnrateSettings(pluginRef, "Mouse Trap Turret", out MouseTrapMoonSpawnrates, out MouseTrapInteriorSpawnrates);
        }
    }
}