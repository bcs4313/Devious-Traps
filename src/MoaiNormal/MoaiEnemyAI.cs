using System;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static MoaiEnemy.Plugin;
using static MoaiEnemy.src.MoaiNormal.MoaiNormalNet;
using System.Collections.Generic;
using System.Reflection;
using static UnityEngine.UIElements.UIR.Implementation.UIRStylePainter;
using MoaiEnemy;

namespace MoaiEnemy.src.MoaiNormal
{

    // MoaiEnemyAI Inherits from MOAIAICORE, which controls all of its basic functions.
    // The red variant will also inherit MOAIAICORE to keep default behavior, and then 'inject' its own behaviors in AI Interval.

    class MoaiEnemyAI : MOAIAICORE
    {
        public override void Start()
        {
            baseInit();
        }

        public override void Update()
        {
            base.Update();
            baseUpdate();

            switch (currentBehaviourStateIndex)
            {
                case (int)State.SearchingForPlayer:
                    thunderReset();
                    break;

                case (int)State.StickingInFrontOfPlayer:
                    thunderTick();
                    break;
            };
        }

        public override void DoAIInterval()
        {
            if (isEnemyDead)
            {
                return;
            };
            base.DoAIInterval();
            baseAIInterval();

            switch (currentBehaviourStateIndex)
            {
                case (int)State.SearchingForPlayer:
                    baseSearchingForPlayer();
                    break;
                case (int)State.HeadingToEntrance:
                    baseHeadingToEntrance();
                    break;
                case (int)State.Guard:
                    baseGuard();
                    break;
                case (int)State.StickingInFrontOfEnemy:
                    baseStickingInFrontOfEnemy();
                    break;
                case (int)State.StickingInFrontOfPlayer:
                    baseStickingInFrontOfPlayer();
                    break;

                case (int)State.HeadSwingAttackInProgress:
                    baseHeadSwingAttackInProgress();
                    break;

                default:
                    LogIfDebugBuild("This Behavior State doesn't exist!");
                    break;
            }
        }

        public void thunderReset()
        {
            RoundManager m = RoundManager.Instance;

            if (!gameObject.name.Contains("Blue"))
            {
                return;
            }

            if (targetPlayer == null || ticksTillThunder > 0)
            {
                return;
            }

            //LogIfDebugBuild("MOAI: spawning LBolt");
            ticksTillThunder = 2 + Math.Min((float)Math.Pow(Vector3.Distance(transform.position, targetPlayer.transform.position), 1.75), 180);
            Vector3 position = serverPosition;
            position.y += (float)(enemyRandom.NextDouble() * ticksTillThunder * 0.2 + 4 * this.gameObject.transform.localScale.x) * Math.Sign(enemyRandom.Next(-100, 100));
            position.x += (float)(enemyRandom.NextDouble() * ticksTillThunder * 0.2 + 4 * this.gameObject.transform.localScale.x) * Math.Sign(enemyRandom.Next(-100, 100));

            GameObject weather = GameObject.Find("TimeAndWeather");

            // find "Stormy" in weather
            GameObject striker = null;
            for (int i = 0; i < weather.transform.GetChildCount(); i++)
            {
                GameObject g = weather.transform.GetChild(i).gameObject;
                if (g.name.Equals("Stormy"))
                {
                    //Debug.Log("Lethal Chaos: Found Stormy!");
                    striker = g;
                }
            }
            if (striker != null)
            {
                // change to include warning

                if(!striker.activeSelf)
                {
                    Plugin.networkHandler.s_moaiEnableStriker.SendAllClients(true);
                }
                m.LightningStrikeServerRpc(position);
                //m.ShowStaticElectricityWarningClientRpc
            }
            else
            {
                Debug.LogError("Lethal Chaos: Failed to find Stormy Weather container (LBolt)!");
            }
        }

        public void thunderTick()
        {
            ticksTillThunder -= 1;
            if (ticksTillThunder <= 0)
            {
                thunderReset();
            }
        }

    }
}