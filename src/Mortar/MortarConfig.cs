using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeviousTraps.src
{
    // there are too many things to config so I thought keeping this in a sep file would be cleaner
    internal class MortarConfig
    {
        // core
        public static ConfigEntry<float> MortarSpawnrate;
        public static ConfigEntry<float> MortarVolume;
        public static ConfigEntry<float> MortarWarningVolume;
        public static ConfigEntry<float> MortarReloadTime;
        public static ConfigEntry<float> MortarChargeTime;

        // ranges
        public static ConfigEntry<float> MortarMinRange;
        public static ConfigEntry<float> MortarMaxRange;
        public static ConfigEntry<float> MortarPreferredStandoff;
        public static ConfigEntry<float> MortarRequiredCeiling;

        // salvo
        public static ConfigEntry<int> MortarMinShells;
        public static ConfigEntry<int> MortarMaxShells;
        public static ConfigEntry<float> MortarScatterRadius;
        public static ConfigEntry<float> MortarLeadFactor;
        public static ConfigEntry<float> MortarShellInterval;

        // ballistics
        public static ConfigEntry<float> MortarShellSpeed;
        public static ConfigEntry<float> MortarMinFlightTime;
        public static ConfigEntry<float> MortarMaxFlightTime;
        public static ConfigEntry<float> MortarGravityScale;

        // rotation
        public static ConfigEntry<float> MortarRotationSpeed;
        public static ConfigEntry<float> MortarElevationSpeed;
        public static ConfigEntry<float> MortarMinElevation;
        public static ConfigEntry<float> MortarMaxElevation;

        // damage
        public static ConfigEntry<float> MortarKillRange;
        public static ConfigEntry<float> MortarDamageRange;
        public static ConfigEntry<int> MortarDamage;
        public static ConfigEntry<float> MortarPhysicsForce;

        // param is named Config so the body reads the same as bindVars() in Plugin.cs
        // call site: MortarConfig.BindMortarConfig(Config);
        public static void BindMortarConfig(Plugin pluginRef)
        {
            MortarSpawnrate = pluginRef.Config.Bind("Mortar Turret", "Spawnrate", 0.7f, "How often do these turrets spawn? Note that mortars are outdoor-only. (default 0.7)");
            MortarVolume = pluginRef.Config.Bind("Mortar Turret", "Turret Volume", 1f, "How loud are all sounds from this turret and its shells? (default 1)");
            MortarWarningVolume = pluginRef.Config.Bind("Mortar Turret", "Turret Warning Volume Multiplier", 0.65f, "How loud is the pre-firing (warning) sound from the turret? Set to a lower value if you find the sound annoying or want to surprise players more often with the shells. (default 0.65)");

            MortarReloadTime = pluginRef.Config.Bind("Mortar Turret", "Time Between Salvos", 34f, "Length of time before the mortar can fire another salvo, in seconds. (default 34)");
            MortarChargeTime = pluginRef.Config.Bind("Mortar Turret", "Charge Up Time", 3.5f, "Time the mortar spends loudly spinning up before the first shell leaves the tube, in seconds. (default 3.5)");

            MortarMinRange = pluginRef.Config.Bind("Mortar Turret", "Minimum Range", 18f, "Dead zone. Inside this distance the barrel can't depress far enough to hit you, so sprinting AT the mortar is the counterplay. Setting this to 0 removes the only way to beat it. (default 18)");
            MortarMaxRange = pluginRef.Config.Bind("Mortar Turret", "Range", 110f, "How far away a mortar can target you. Unlike the other turrets it does NOT need a line of sight to fire. (default 110)");
            MortarPreferredStandoff = pluginRef.Config.Bind("Mortar Turret", "Spawn Distance from Entrance", 65f, "Placement bias: how many meters from an entrance the mortar will spawn at. (default 65)");
            MortarRequiredCeiling = pluginRef.Config.Bind("Mortar Turret", "Required Sky Clearance", 30f, "Placement rejects any spot with geometry within this many meters overhead. Set to 0 if you want goofy placements. (default 30)");

            MortarMinShells = pluginRef.Config.Bind("Mortar Turret", "Minimum Shells Per Salvo", 5, "Fewest shells fired in one salvo. (default 5)");
            MortarMaxShells = pluginRef.Config.Bind("Mortar Turret", "Maximum Shells Per Salvo", 10, "Most shells fired in one salvo. (default 10)");
            MortarScatterRadius = pluginRef.Config.Bind("Mortar Turret", "Scatter Radius", 9f, "Shells land randomly inside a circle of this radius around where you were predicted to be. Bigger = more area denial but a larger spread of explosions. (default 9)");
            MortarLeadFactor = pluginRef.Config.Bind("Mortar Turret", "Targeting with Player Velocity", 2f, "Seconds of your current velocity used to lead the salvo. 0 means it just aims exactly where the player is, more than 0 means it attempts to account for player velocity in its shots. (default 2)");
            MortarShellInterval = pluginRef.Config.Bind("Mortar Turret", "Delay Per Shell", 0.3f, "Delay between each shell in the ripple, in seconds. Also the window the barrel gets to micro-rotate onto the next impact point. (default 0.3)");

            MortarShellSpeed = pluginRef.Config.Bind("Mortar Turret", "Projectile Speed", 34f, "Meters per second used to work out the shell's flight time from the target player. Affects how arcs look when they travel. (default 34)");
            MortarMinFlightTime = pluginRef.Config.Bind("Mortar Turret", "Minimum Flight Time", 2.2f, "Floor on how long a shell stays in the air, in seconds. The default guarantees you always get a moment to look up, even on close shots. (default 2.2)");
            MortarMaxFlightTime = pluginRef.Config.Bind("Mortar Turret", "Maximum Flight Time", 5.5f, "Ceiling on how long a shell stays in the air, so long shots don't hang forever. (default 5.5)");
            MortarGravityScale = pluginRef.Config.Bind("Mortar Turret", "Gravity Multiplier", 1f, "Multiplier on gravity for shells only. Raise it for a snappier, steeper arc. (default 1)");

            MortarRotationSpeed = pluginRef.Config.Bind("Mortar Turret", "Rotation Speed", 75f, "How quickly the carriage rotates to face its target (degrees per second). The lower the value, the easier it is to outmaneuver. (default 75)");
            MortarElevationSpeed = pluginRef.Config.Bind("Mortar Turret", "Elevation Speed", 45f, "How quickly the barrel pitches up and down (degrees per second). (default 45)");
            MortarMinElevation = pluginRef.Config.Bind("Mortar Turret", "Minimum Elevation", 35f, "Lowest the barrel can point, in degrees above horizontal. This is what physically creates the dead zone, so keep it consistent with Minimum Range. (default 35)");
            MortarMaxElevation = pluginRef.Config.Bind("Mortar Turret", "Maximum Elevation", 80f, "Highest the barrel can point, in degrees above horizontal. (default 80)");

            MortarKillRange = pluginRef.Config.Bind("Mortar Turret", "Kill Range", 2.2f, "Instant death radius per shell, in meters. A vanilla landmine is 5.7 - this is deliberately much smaller because you eat 5 to 10 of them. (default 2.2)");
            MortarDamageRange = pluginRef.Config.Bind("Mortar Turret", "Damage Range", 6f, "Radius per shell where you take damage instead of dying, in meters. (default 6)");
            MortarDamage = pluginRef.Config.Bind("Mortar Turret", "Damage", 35, "Damage per shell inside the damage range. You can make them heal with negative values too. At 35, three glancing hits from one salvo will drop a full health player. (default 35)");
            MortarPhysicsForce = pluginRef.Config.Bind("Mortar Turret", "Physics Force", 20f, "How hard each detonation shoves players and objects around. (default 20)");

            var MortarSpawnrateEntry = new FloatInputFieldConfigItem(MortarSpawnrate, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });

            var MortarVolumeEntry = new FloatSliderConfigItem(MortarVolume, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 2f,
            });


            var MortarWarningVolumeEntry = new FloatSliderConfigItem(MortarWarningVolume, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 2f,
            });

            var MortarReloadTimeEntry = new FloatInputFieldConfigItem(MortarReloadTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 999999,
            });

            var MortarChargeTimeEntry = new FloatInputFieldConfigItem(MortarChargeTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 999999,
            });

            var MortarMinRangeEntry = new FloatInputFieldConfigItem(MortarMinRange, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 1000,
            });

            var MortarMaxRangeEntry = new FloatInputFieldConfigItem(MortarMaxRange, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var MortarPreferredStandoffEntry = new FloatInputFieldConfigItem(MortarPreferredStandoff, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 100000000,
            });

            var MortarRequiredCeilingEntry = new FloatInputFieldConfigItem(MortarRequiredCeiling, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 1000,
            });

            var MortarMinShellsEntry = new IntInputFieldConfigItem(MortarMinShells, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 100000000,
            });

            var MortarMaxShellsEntry = new IntInputFieldConfigItem(MortarMaxShells, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 100000000,
            });

            var MortarScatterRadiusEntry = new FloatSliderConfigItem(MortarScatterRadius, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 40,
            });

            var MortarLeadFactorEntry = new FloatSliderConfigItem(MortarLeadFactor, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 3,
            });

            var MortarShellIntervalEntry = new FloatInputFieldConfigItem(MortarShellInterval, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            var MortarShellSpeedEntry = new FloatInputFieldConfigItem(MortarShellSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 100000000,
            });

            var MortarMinFlightTimeEntry = new FloatInputFieldConfigItem(MortarMinFlightTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0.1f,
                Max = 60,
            });

            var MortarMaxFlightTimeEntry = new FloatInputFieldConfigItem(MortarMaxFlightTime, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0.1f,
                Max = 60,
            });

            var MortarGravityScaleEntry = new FloatSliderConfigItem(MortarGravityScale, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0.1f,
                Max = 5,
            });

            var MortarRotationSpeedEntry = new FloatInputFieldConfigItem(MortarRotationSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 3600,
            });

            var MortarElevationSpeedEntry = new FloatInputFieldConfigItem(MortarElevationSpeed, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 3600,
            });

            var MortarMinElevationEntry = new FloatSliderConfigItem(MortarMinElevation, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 89,
            });

            var MortarMaxElevationEntry = new FloatSliderConfigItem(MortarMaxElevation, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 89,
            });

            var MortarKillRangeEntry = new FloatSliderConfigItem(MortarKillRange, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 30,
            });

            var MortarDamageRangeEntry = new FloatSliderConfigItem(MortarDamageRange, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 30,
            });

            var MortarDamageEntry = new IntInputFieldConfigItem(MortarDamage, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = -1000,
                Max = 1000,
            });

            var MortarPhysicsForceEntry = new FloatInputFieldConfigItem(MortarPhysicsForce, new FloatInputFieldOptions
            {
                RequiresRestart = false,
                Min = 0,
                Max = 100000000,
            });

            LethalConfigManager.AddConfigItem(MortarSpawnrateEntry);
            LethalConfigManager.AddConfigItem(MortarVolumeEntry);
            LethalConfigManager.AddConfigItem(MortarWarningVolumeEntry);
            LethalConfigManager.AddConfigItem(MortarReloadTimeEntry);
            LethalConfigManager.AddConfigItem(MortarChargeTimeEntry);
            LethalConfigManager.AddConfigItem(MortarMinRangeEntry);
            LethalConfigManager.AddConfigItem(MortarMaxRangeEntry);
            LethalConfigManager.AddConfigItem(MortarPreferredStandoffEntry);
            LethalConfigManager.AddConfigItem(MortarRequiredCeilingEntry);
            LethalConfigManager.AddConfigItem(MortarMinShellsEntry);
            LethalConfigManager.AddConfigItem(MortarMaxShellsEntry);
            LethalConfigManager.AddConfigItem(MortarScatterRadiusEntry);
            LethalConfigManager.AddConfigItem(MortarLeadFactorEntry);
            LethalConfigManager.AddConfigItem(MortarShellIntervalEntry);
            LethalConfigManager.AddConfigItem(MortarShellSpeedEntry);
            LethalConfigManager.AddConfigItem(MortarMinFlightTimeEntry);
            LethalConfigManager.AddConfigItem(MortarMaxFlightTimeEntry);
            LethalConfigManager.AddConfigItem(MortarGravityScaleEntry);
            LethalConfigManager.AddConfigItem(MortarRotationSpeedEntry);
            LethalConfigManager.AddConfigItem(MortarElevationSpeedEntry);
            LethalConfigManager.AddConfigItem(MortarMinElevationEntry);
            LethalConfigManager.AddConfigItem(MortarMaxElevationEntry);
            LethalConfigManager.AddConfigItem(MortarKillRangeEntry);
            LethalConfigManager.AddConfigItem(MortarDamageRangeEntry);
            LethalConfigManager.AddConfigItem(MortarDamageEntry);
            LethalConfigManager.AddConfigItem(MortarPhysicsForceEntry);
        }
    }
}