using System;
using System.Collections.Generic;
using System.Text;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using LethalConfig;
using BepInEx.Configuration;
using BepInEx;
using BepInEx.Configuration;

// stores public vars for the plugin that relate to
// config files.
namespace EasterIsland
{
    internal class ConfigModel
    {
        // consider these multipliers for existing values
        // consider these multipliers for existing values
        public static ConfigEntry<float> moaiGlobalSize;
        public static ConfigEntry<float> moaiGlobalMusicVol;
        public static ConfigEntry<float> moaiGlobalRarity;
        public static ConfigEntry<float> moaiGlobalSpeed;

        public static void setupConfig()
        {

            var sizeSlider = new FloatSliderConfigItem(moaiGlobalSize, new FloatSliderOptions
            {
                Min = 0.05f,
                Max = 5f
            });

            var voluimeSlider = new FloatSliderConfigItem(moaiGlobalMusicVol, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 2f
            });

            var raritySlider = new FloatSliderConfigItem(moaiGlobalRarity, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 10f
            });

            var speedSlider = new FloatSliderConfigItem(moaiGlobalSpeed, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 5f
            });
        }
    }
}
