
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace DeviousTraps.src
{
    public class SawProjectileV2 : NetworkBehaviour
    {
        internal Vector3 LaunchForce = Vector3.zero;

        // linked via unity
        public Rigidbody RigidBody;
        public Collider Collider;

        float LastHitPlayerCooldown = 0;  // 200 ms cooldown per hit
        PlayerControllerB LastHitPlayer;

        float LastHitEnemyCooldown = 0;  // 200 ms cooldown per hit
        EnemyAI LastHitEnemy;

        float HitCooldownReset = 0.2f;
        float Lifetime = 60f;

        public Material BloodMat;
        public MeshRenderer Renderer;

        public AudioSource AudioBlood1;
        public AudioSource AudioBlood2;
        public AudioSource AudioBlood3;
        public AudioSource AudioBounce;
        public GameObject BloodParticles;
        public GameObject SparkParticles;
        public float BloodLifetime = 0f;
        public void Start()
        {
            if (RoundManager.Instance.IsHost)
            {
                RigidBody.AddForce(LaunchForce);
            }
            AudioBlood1.volume = Plugin.SawVolume.Value;
            AudioBlood2.volume = Plugin.SawVolume.Value;
            AudioBlood3.volume = Plugin.SawVolume.Value;
            AudioBounce.volume = Plugin.SawVolume.Value;
        }

        public void SetScale(float mult)
        {
            this.transform.localScale *= mult;
        }

        //float PositionUpdateRate = 0.05f;
        //float PositionUpdateTimer = 0;
        public static float BounceSoundThreshold = 5;
        Vector3 PreviousVelocity = Vector3.zero;
        public void Update()
        {
            LastHitPlayerCooldown -= Time.deltaTime;
            LastHitEnemyCooldown -= Time.deltaTime;
            Lifetime -= Time.deltaTime;
            BloodLifetime -= Time.deltaTime;
            //PositionUpdateTimer -= Time.deltaTime;

            if(BloodLifetime < 0 && BloodParticles.activeInHierarchy)
            {
                ParticleSetClientRpc(false);
            }

            var cur_v = RigidBody.velocity;
            var prev_v = PreviousVelocity;
            PreviousVelocity = cur_v;
            if((cur_v - prev_v).magnitude > BounceSoundThreshold)
            {
                PlayBounceClientRpc();
            }

            if (Lifetime < 0 && RoundManager.Instance.IsHost) { Destroy(this.gameObject); }

            /*
            if(RoundManager.Instance.IsHost && PositionUpdateTimer < 0)
            {
                PositionUpdateTimer = PositionUpdateRate;
                SetPosClientRpc(transform.position);
            }
            */
        }

        /*
        /// <summary>
        ///  this is dumb.
        /// </summary>
        [ClientRpc]
        public void SetPosClientRpc(Vector3 pos)
        {
            if(!RoundManager.Instance.IsHost)
            {
                transform.position = pos;
            }
        }
        */

        [ClientRpc]
        public void DmgPlayerClientRpc(ulong netid, int amount)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach(var ply in players)
            {
                if(ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.DamagePlayer(amount);
                    BloodLifetime = 0.8f;
                    Renderer.material = BloodMat;
                }
            }
        }


        [ClientRpc]
        public void KillPlayerClientRpc(ulong netid, Vector3 launchVelocity)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var ply in players)
            {
                if (ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.KillPlayer(launchVelocity, true, CauseOfDeath.Snipping, 7);
                    BloodLifetime = 0.8f;
                    Renderer.material = BloodMat;
                }
            }
        }

        [ClientRpc]
        public void ParticleSetClientRpc(bool active)
        {
            BloodParticles.SetActive(active);
        }

        [ClientRpc]
        public void PlayBounceClientRpc()
        {
            AudioBounce.Play();
        }

        [ClientRpc]
        public void SparkSetClientRpc(bool active)
        {
            SparkParticles.SetActive(active);
        }

        [ClientRpc]
        public void PlayShredClientRpc(int id)
        {
            switch(id)
            {
                case 1:
                    AudioBlood1.Play();
                    break;
                case 2:
                    AudioBlood2.Play();
                    break;
                case 3:
                    AudioBlood3.Play();
                    break;
                default:
                    AudioBlood1.Play();
                    break;
            }
        }

        // the higher this multiplier is, the easier it is to push around saws.
        public static float sawPushMultiplier = 1;
        public void OnTriggerEnter(Collider other)
        {
            //Debug.Log("Saw hit: " + other.gameObject);
            if (RoundManager.Instance.IsHost)
            {
                var go = other.gameObject;
                PlayerControllerB ply = go.GetComponent<PlayerControllerB>();
                EnemyAI ey = go.GetComponent<EnemyAI>();

                if (ply && (LastHitPlayerCooldown <= 0 || ply != LastHitPlayer))
                {
                    var rnd = new System.Random();
                    int dmg = (int)(Math.Max(ply.playerRigidbody.velocity.magnitude, RigidBody.velocity.magnitude) * Plugin.SawDmgMult.Value);
                    if (dmg > 10)
                    {
                        PlayShredClientRpc(rnd.Next(3) + 1);
                        ParticleSetClientRpc(true);
                    }
                    if (dmg >= ply.health)
                    {
                        KillPlayerClientRpc(ply.NetworkObject.NetworkObjectId, RigidBody.velocity);
                    }
                    else if(dmg >= 5)
                    {
                        DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, dmg);
                    }
                    else
                    {
                        // do nothing
                    }

                    // deflect blade away from player on contact
                    Vector3 pushDir = (transform.position - ply.transform.position).normalized;
                    RigidBody.AddForce(pushDir * sawPushMultiplier, ForceMode.Impulse);
                    LastHitPlayerCooldown = HitCooldownReset;
                    LastHitPlayer = ply;

                    LastHitPlayerCooldown = HitCooldownReset;
                    LastHitPlayer = ply;
                }
                if (ey && (LastHitEnemyCooldown <= 0 || ey != LastHitEnemy))
                {
                    ey.HitEnemyClientRpc(3, -1, true);
                    LastHitEnemyCooldown = HitCooldownReset;
                    LastHitEnemy = ey;
                }
            }
        }

    }
}
