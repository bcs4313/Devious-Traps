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
using MoaiEnemy.src.MoaiNormal;
using System.Collections.Generic;

namespace MoaiEnemy
{
    [BepInDependency("LethalNetworkAPI")]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        public static Harmony _harmony; 
        public static EnemyType ExampleEnemy;
        public static new ManualLogSource Logger;

        public static MoaiNormalNet networkHandler = new MoaiNormalNet();

        public void LogIfDebugBuild(string text)
        {
        #if DEBUG
            Plugin.Logger.LogInfo(text);
        #endif
        }

        private void Awake() {
            Logger = base.Logger;
            Assets.PopulateAssets();
            bindVars();

            // asset loading phase
            var MoaiEnemy = Assets.MainAssetBundle.LoadAsset<EnemyType>("MoaiEnemy");
            var tlTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("MoaiEnemyTN");
            var tlTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("MoaiEnemyTK");

            var MoaiBlue = Assets.MainAssetBundle.LoadAsset<EnemyType>("MoaiBlue");
            var MoaiBlueTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("MoaiBlueTN");
            var MoaiBlueTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("MoaiBlueTK");

            var MoaiRed = Assets.MainAssetBundle.LoadAsset<EnemyType>("MoaiRed");
            var MoaiRedTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("MoaiRedTN");
            var MoaiRedTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("MoaiRedTK");

            // debug phase
            Debug.Log("MOAI ENEMY BUNDLE: " + Assets.MainAssetBundle.ToString());
            Debug.Log("MOAINORM ENEMY: " + MoaiEnemy);
            Debug.Log("MOAINORM TK: " + tlTerminalKeyword);
            Debug.Log("MOAINORM TN: " + tlTerminalNode);
            Debug.Log("MOAIBLUE ENEMY: " + MoaiBlue);
            Debug.Log("MOAIBLUE TK: " + MoaiBlueTerminalNode);
            Debug.Log("MOAIBLUE TN: " + MoaiBlueTerminalKeyword);
            Debug.Log("RED ENEMY: " + MoaiRed);
            Debug.Log("RED TK: " + MoaiRedTerminalNode);
            Debug.Log("RED TN: " + MoaiRedTerminalKeyword);

            UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);

            // register phase 
            NetworkPrefabs.RegisterNetworkPrefab(MoaiEnemy.enemyPrefab);
            NetworkPrefabs.RegisterNetworkPrefab(MoaiBlue.enemyPrefab);
            NetworkPrefabs.RegisterNetworkPrefab(MoaiRed.enemyPrefab);

            // rarity range is 0-100 normally
            RegisterEnemy(MoaiEnemy, (int)(120 / moaiGlobalRarity.Value), LevelTypes.All, SpawnType.Daytime, tlTerminalNode, tlTerminalKeyword);
            RegisterEnemy(MoaiBlue, (int)(14 / moaiGlobalRarity.Value), LevelTypes.All, SpawnType.Outside, MoaiBlueTerminalNode, MoaiBlueTerminalKeyword);
            RegisterEnemy(MoaiRed, (int)(700 / moaiGlobalRarity.Value), LevelTypes.All, SpawnType.Daytime, MoaiRedTerminalNode, MoaiRedTerminalKeyword); 
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // Required by https://github.com/EvaisaDev/UnityNetcodePatcher maybe?
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
            Debug.Log("MOAI: Registering Moai Net Messages");
            networkHandler.setup();

            On.RoundManager.LoadNewLevel += (On.RoundManager.orig_LoadNewLevel orig, global::RoundManager self, int randomSeed, global::SelectableLevel newLevel) =>
            {
                setSpawnProbabilities();
                orig.Invoke(self, randomSeed, newLevel);
            };
        }

        

        static void setSpawnProbabilities()
        {
            float r = UnityEngine.Random.RandomRange(0.0f, 1.0f + (moaiGlobalRarity.Value  - 1));

            if (r < 0.06)
            {
                MoaiEnemyAI.rawSpawnProbability = 1.0f;
            } 
            else if (r < 0.12)
            {
                MoaiEnemyAI.rawSpawnProbability = 0.5f;
            }
            else if (r < 0.20)
            {
                MoaiEnemyAI.rawSpawnProbability = 0.25f;
            }
            else
            {
                MoaiEnemyAI.rawSpawnProbability = 0f;
            }

            Debug.Log("MOAI rawspawn multiplier: " + MoaiEnemyAI.rawSpawnProbability);
        }

        // SETTINGS SECTION
        // consider these multipliers for existing values
        public static ConfigEntry<float> moaiGlobalSize;
        public static ConfigEntry<float> moaiGlobalSizeVar;
        public static ConfigEntry<float> moaiGlobalMusicVol;
        public static ConfigEntry<float> moaiGlobalRarity;
        public static ConfigEntry<float> moaiGlobalSpeed;

        public void bindVars()
        {
            moaiGlobalMusicVol = Config.Bind("Global", "Enemy Sound Volume", 0.6f, "Changes the volume of all moai sounds. May make moai more sneaky as well.");
            moaiGlobalSizeVar = Config.Bind("Global", "Size Variant Chance", 0.2f, "The chance of a moai to spawn in a randomly scaled size. Affects their pitch too.");
            moaiGlobalSize = Config.Bind("Global", "Size Multiplier", 1f, "Changes the size of all moai models. Scales pretty violently. Affects SFX pitch.");
            moaiGlobalRarity = Config.Bind("Global", "Enemy Rarity Multiplier", 1f, "How rare are moai? A 2x multiplier makes them 2x more rare, and a 0.25x multiplier would make them 4x more common.");
            moaiGlobalSpeed = Config.Bind("Global", "Enemy Speed Multiplier", 1f, "Changes the speed of all moai. 4x would mean they are 4 times faster, 0.5x would be 2 times slower.");

            var sizeSlider = new FloatSliderConfigItem(moaiGlobalSize, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0.05f,
                Max = 5f
            });

            var sizeVarSlider = new FloatSliderConfigItem(moaiGlobalSizeVar, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 1f
            });


            var volumeSlider = new FloatSliderConfigItem(moaiGlobalMusicVol, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0.0f,
                Max = 1f
            });

            var raritySlider = new FloatSliderConfigItem(moaiGlobalRarity, new FloatSliderOptions
            {
                RequiresRestart = true,
                Min = 0.05f,
                Max = 10f
            });

            var speedSlider = new FloatSliderConfigItem(moaiGlobalSpeed, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0.0f,
                Max = 5f,
            });
            
            LethalConfigManager.AddConfigItem(volumeSlider);
            LethalConfigManager.AddConfigItem(sizeSlider);
            LethalConfigManager.AddConfigItem(sizeVarSlider);
            LethalConfigManager.AddConfigItem(raritySlider);
            LethalConfigManager.AddConfigItem(speedSlider);
        }
    }

    public static class Assets {
        public static AssetBundle MainAssetBundle = null;
        public static void PopulateAssets() {
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(sAssemblyLocation, "moaibundle"));
            if (MainAssetBundle == null) {
                Plugin.Logger.LogError("Failed to load custom assets.");
                return;
            }
        }
    }
}