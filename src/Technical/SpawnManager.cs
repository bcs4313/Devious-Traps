using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using LethalConfig;
using DeviousTraps;
using BepInEx.Configuration;

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
        public static ConfigEntry<string> GENERALINFO;
        public static ConfigEntry<string> SawTurretMoonSpawnrates;
        public static ConfigEntry<string> SawTurretInteriorSpawnrates;

        // only for creating the Lethal Config widgets for entering custom spawn weights
        public void SetUpSettings(Plugin pluginRef)
        {

            GENERALINFO = pluginRef.Config.Bind("Dynamic Spawnrates", "General Info -IMPORTANT-", "", "This setting doesn't do anything, but it gives some context on how the settings work. These settings are host only and do NOT require a restart. The only spawnrate" +
                "entries that require a restart are the base spawnrates of a turret found in categories separate from this one. Moon and interior weights multiply to the base turret spawnrate if applicable in your configuration." +
                "Just enter simple comma separated lists with colon separators, such as this: Vanilla:0.8,Modded:1.3,Experimentation:2. The values should be treated as multipliers to the base spawnrate, NOT as individual rarity values. (ignoring this will result" +
                "in many, MANY turrets spawning absolutely everywhere)");


            var GENERALINFOEntry = new TextInputFieldConfigItem(GENERALINFO, new TextInputFieldOptions()
            {
                RequiresRestart = false,
            });

            LethalConfigManager.AddConfigItem(GENERALINFOEntry);

            // saw turret section
            SawTurretMoonSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Saw Turret Moon Weights", "", "Moon Spawn Weights that dynamically apply to the Saw turret. " +
                "Enter a comma separated list of pairs, each pair should follow the format of levelName:spawnrateMultiplier. The Selectable Level's name or the moon's name from the console is accepted. Names are not case sensitive or space sensitive" +
                "The spawnrate multiplier is a decimal number that is multiplied with the base turret spawnrate. All, Vanilla, and Modded are accepted keywords. Content Tags are accepted.");

            SawTurretInteriorSpawnrates = pluginRef.Config.Bind("Dynamic Spawnrates", "Saw Turret Interior Weights", "", "Interior Spawn Weights that dynamically apply to the Saw turret. " +
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
        }
    }
}
