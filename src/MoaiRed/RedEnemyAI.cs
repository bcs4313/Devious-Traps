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
using static MoaiEnemy.src.MoaiNormal.MoaiNormalNet;

namespace MoaiEnemy.src.MoaiNormal
{

    class RedEnemyAI : MOAIAICORE
    {
        // updated once every 15 seconds
        bool preparing = false;
        float anger = 0; 

        // blitz vars
        Vector3 blitzTarget = Vector3.zero;
        Vector3 startPosFromTarget = Vector3.zero;
        int playerTargetSteps = 0;
        int tempHp = 0;

        // extra audio sources
        public AudioSource creaturePrepare;
        public AudioSource creatureBlitz;
        public GameObject flameEffect;
        public GameObject swirlEffect;

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
            Blitz,
            Staring
        }

        public override void Start()
        {
            baseInit();
            creatureBlitz.volume = moaiGlobalMusicVol.Value;
            creaturePrepare.volume = moaiGlobalMusicVol.Value;
            flameEffect.SetActive(false);
        }

        public override void Update()
        {
            base.Update();
            baseUpdate();
            switch (currentBehaviourStateIndex)
            {
                case (int)State.Preparing:
                    if (!flameEffect.activeInHierarchy) { flameEffect.SetActive(true); }
                    break;
                case (int)State.Blitz:
                    if (!flameEffect.activeInHierarchy) { flameEffect.SetActive(true); }
                    break;
                default:
                    if (flameEffect.activeInHierarchy) { flameEffect.SetActive(false); }
                    break;
             }
        }

        public override void playSoundId(String id)
        {
            switch (id)
            {
                case "creatureBlitz":
                    stopAllSound();
                    creatureBlitz.Play();
                    break;
                case "creaturePrepare":
                    stopAllSound();
                    creaturePrepare.Play();
                    break;
            }
        }

        public bool playerIsAlone(PlayerControllerB player)
        {
            RoundManager m = RoundManager.Instance;
            var team = RoundManager.Instance.playersManager.allPlayerScripts;
            for (int i = 0; i < team.Length; i++)
            {
                var p = team[i];
                if(p.playerClientId != player.playerClientId)
                {
                    // test distance
                    if(Vector3.Distance(p.transform.position, player.transform.position) < 30)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool playerOnRock(PlayerControllerB player)
        {
            var slidingSurface = "None";
            var interactRay = new Ray(player.transform.position + Vector3.up, -Vector3.up);
            RaycastHit castHit;
            if (Physics.Raycast(interactRay, out castHit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore))
            {
                for (int i = 0; i < StartOfRound.Instance.footstepSurfaces.Length; i++)
                {
                    // go through all surfaces
                    if (castHit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[i].surfaceTag))
                    {
                        slidingSurface = StartOfRound.Instance.footstepSurfaces[i].surfaceTag;
                    }
                }
            }
            switch (slidingSurface)
            {
                default:
                    return false;
                case "Rock":
                    return true;
                case "Concrete":
                    return true;
            }
        }

        public bool playerIsDefenseless(PlayerControllerB player)
        {
            return false;
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
                    Debug.Log("SearchingForPlayer");
                    if (FoundClosestPlayerInRange(40f, true))  // sets targetPlayer when true
                    {
                        // stare state transfer
                        if (anger < 100)
                        {
                            agent.speed = 0;
                            if(playerOnRock(targetPlayer))
                            {
                                anger = 100;
                            }
                            else
                            {   // anger builds up faster the closer you are
                                anger += 37 / Vector3.Distance(transform.position, targetPlayer.transform.position);
                            }
                            return;
                        }

                        // kidnap state transfer
                        if (anger >= 100 && playerIsAlone(targetPlayer) && !playerOnRock(targetPlayer))
                        {
                            Debug.Log("Swithing to Kidnap State");
                            StopSearch(currentSearch);
                            tempHp = enemyHP;
                            SwitchToBehaviourClientRpc((int)State.Preparing);
                            anger = 0;
                            return;
                        }

                        if (anger >= 100)
                        {
                            // blitz state transfer
                            Debug.Log("Swithing to Preparing State");
                            StopSearch(currentSearch);
                            tempHp = enemyHP;
                            SwitchToBehaviourClientRpc((int)State.Preparing);
                            anger = 0;
                            return;
                        }
                    }
                    else
                    {
                        if(anger > 0)
                        {
                            anger -= 1;  // 20 seconds from 100 anger to completely deaggro
                        }
                    }
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
                case (int)State.Preparing:
                    agent.speed = 0;

                    // Transition to Blitz if sound is no longer playing
                    if (creaturePrepare.time > 3.7)
                    {
                        LogIfDebugBuild("MOAIRED: Blitz Activated");
                        preparing = false;
                        playerTargetSteps = 0;
                        impatience = 0;
                        SwitchToBehaviourClientRpc((int)State.Blitz);
                        return;
                    }

                    // sound switch 
                    if (!preparing)
                    {
                        Plugin.networkHandler.s_moaiSoundPlay.SendAllClients(new moaiSoundPkg(NetworkObject.NetworkObjectId, "creaturePrepare"));
                        preparing = true;
                    }
                    break;
                case (int)State.Blitz:
                    agent.speed = 35f * moaiGlobalSpeed.Value;
                    agent.acceleration *= 10;
                    agent.angularSpeed *= 10;
                    enemyHP = 500;

                    // in blitz, the target resets if blitzTarget is Vector3.zero
                    if (blitzTarget == Vector3.zero)
                    {
                        impatience = 0;
                        var player = GetClosestPlayer(false, true, false);
                        NavMeshHit hit;
                        if (!player)
                        {
                            StartSearch(transform.position);
                            blitzReset();
                            return;
                        }
                        Vector3 playerPos = player.gameObject.transform.position;
                        Vector3 rayDisposition = transform.position - playerPos;
                        Vector3 scaledRayDisposition = Vector3.Normalize(rayDisposition) * Math.Min(Vector3.Distance(playerPos, transform.position), UnityEngine.Random.Range(7f, 9.0f));
                        var valid = UnityEngine.AI.NavMesh.SamplePosition(playerPos - scaledRayDisposition, out hit, 10f, NavMesh.AllAreas);

                        if(!valid)
                        {
                            Debug.Log("MOAI RED: Position Sample Failure -> " + playerPos + " -- " + rayDisposition);
                            blitzReset();
                            return;
                        }
                        else
                        {
                            Debug.Log("MOAI RED: Position Sample Success -> " + hit.position);
                        }

                        blitzTarget = hit.position;
                        Plugin.networkHandler.s_moaiSoundPlay.SendAllClients(new moaiSoundPkg(NetworkObject.NetworkObjectId, "creatureBlitz"));
                        Landmine.SpawnExplosion(transform.position + UnityEngine.Vector3.up, true, 5.7f, 6.4f);
                        startPosFromTarget = this.transform.position;

                        if (playerTargetSteps >= 3)
                        {
                            Debug.Log("red: target end");
                            blitzReset();
                            return;
                        }
                        else
                        {
                            playerTargetSteps++;
                            Debug.Log("red: target player");
                        }
                    }

                    targetPlayer = null;
                    SetDestinationToPosition(blitzTarget);

                    // blitz reset
                    if (Vector3.Distance(transform.position, blitzTarget) < (transform.localScale.magnitude + transform.localScale.magnitude + impatience))
                    {
                        blitzTarget = Vector3.zero;
                    }
                    else
                    {
                        impatience += 0.1f;
                    }

                    // The explosion chains start when a moai is 2/3th of completion to position
                    if (Vector3.Distance(transform.position, blitzTarget) < (Vector3.Distance(startPosFromTarget, blitzTarget) / 3))
                    {
                    }
                    break;

                default:
                    LogIfDebugBuild("This Behavior State doesn't exist!");
                    break;
            }
        }

        public void blitzReset()
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.SearchingForPlayer);
            stamina = 0;
            enemyHP = tempHp;
            blitzTarget = Vector3.zero;
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