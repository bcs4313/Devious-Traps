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

namespace EasterIsland
{
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    [BepInPlugin("LegendOfTheMoai", "", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Harmony _harmony;
        public static EnemyType ExampleEnemy;
        public static new ManualLogSource Logger;

        // easter island net prefabs
        public static AssetBundle easterislandBundle;
        public static GameObject EruptionController;

        public static float rawSpawnMultiplier = 0f;


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

            EruptionController = easterislandBundle.LoadAsset<GameObject>("EruptionController");

            // debug phase
            Debug.Log("EASTER ISLAND ERUPTION CONTROLLER::: " + EruptionController);

            UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);

            // register phase 
            NetworkPrefabs.RegisterNetworkPrefab(EruptionController);

            Logger.LogInfo($"Plugin LegendOfTheMoai is loaded!");


            // Required by https://github.com/EvaisaDev/UnityNetcodePatcher
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
        }

        // SETTINGS SECTION
        // consider these multipliers for existing values
        public void bindVars()
        {
        }

        public static void PopulateAssets()
        {
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            easterislandBundle = AssetBundle.LoadFromFile(Path.Combine(sAssemblyLocation, "eastermoonnetobjects"));

            Debug.Log("Easter Bundle::: " + easterislandBundle);
        }
    }
}