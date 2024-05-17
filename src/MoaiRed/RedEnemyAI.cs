using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using static MoaiEnemy.Plugin;
using System.Threading.Tasks;
using System.Linq;

namespace MoaiEnemy.src.MoaiNormal
{

    class RedEnemyAI : MOAIAICORE
    {
        // updated once every 15 seconds
        bool preparing = false;

        // blitz vars
        Vector3 blitzTarget = Vector3.zero;
        Vector3 startPosFromTarget = Vector3.zero;
        int playerTargetSteps = 1;

        new enum State
        {
            // defaults
            SearchingForPlayer,
            Guard,
            StickingInFrontOfEnemy,
            StickingInFrontOfPlayer,
            HeadSwingAttackInProgress,
            HeadingToEntrance,
            //define custom below
            Preparing,
            Blitz
        }

        public override void Start()
        {
            baseInit();
        }

        public override void Update()
        {
            base.Update();
            baseUpdate();
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
        public async void explosionChain(int amount, int delay, int delayRandomness)
        {
            for (int i = 0; i < amount; i++)
            {
                Vector3 explosionPos = transform.position + UnityEngine.Vector3.up;
                explosionPos.x += UnityEngine.Random.Range(-7.0f, 7.0f);
                explosionPos.y += UnityEngine.Random.Range(-5.0f, 5.0f);
                explosionPos.z += UnityEngine.Random.Range(-7.0f, 7.0f);
                Landmine.SpawnExplosion(transform.position + UnityEngine.Vector3.up, true, 5.7f, 6.4f);
                await Task.Delay(delay + UnityEngine.Random.Range(0, delayRandomness));
            }
        }

        public Vector3 getRandomPlayerPos()
        {
            PlayerControllerB[] players = [];
            for (int i = 0; i < RoundManager.Instance.playersManager.allPlayerScripts.Length; i++)
            {
                PlayerControllerB player = RoundManager.Instance.playersManager.allPlayerScripts[i];

                if (player != null && player.name != null && player.transform != null)
                {

                    if(!player.isPlayerDead && !player.isInHangarShipRoom)
                    {
                        players.Append(player);
                    }
                }
            }

            if (players.Length > 0) { return players[UnityEngine.Random.RandomRangeInt(0, players.Length)].gameObject.transform.position; }
            
            return Vector3.zero;
        }

    }
}