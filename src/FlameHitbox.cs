using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace DeviousTraps.src
{
    public class FlameHitbox : NetworkBehaviour
    {
        internal Vector3 LaunchForce = Vector3.zero;

        // linked via unity
        public Collider Collider;

        float HitCooldown = 0;  // 200 ms cooldown per hit
        float HitCooldownReset = 0.2f;

        float LastHitEnemyCooldown = 0;  // 200 ms cooldown per hit

        public static float BaseDmg = 10f;
        public float dmgRamp = 0f;  // damage from fire hitbox takes a little bit of time to increase to 1x
        public static float dmgRampTime = 0.45f;  // 0.45s to go to full dps

        public void Update()
        {
            HitCooldown -= Time.deltaTime;
            LastHitEnemyCooldown -= Time.deltaTime;
            if (dmgRamp < 1)
            {
                dmgRamp += (Time.deltaTime / dmgRampTime);
                if (dmgRamp > 1f) dmgRamp = 1f;
            }
        }

        // ramp resets when the flames stop firing and start firing
        public void ResetDmgRamp()
        {
            dmgRamp = 0;
        }

        [ClientRpc]
        public void DmgPlayerClientRpc(ulong netid, int amount)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var ply in players)
            {
                if (ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.DamagePlayer(amount);
                }
            }
        }

        [ClientRpc]
        public void KillPlayerClientRpc(ulong netid)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var ply in players)
            {
                if (ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.KillPlayer(ply.playerRigidbody.velocity, true, CauseOfDeath.Burning);
                }
            }
        }


        public void OnTriggerStay(Collider other)
        {
            //Debug.Log("Flame hit: " + other.gameObject);
            if (RoundManager.Instance.IsHost)
            {
                if (dmgRamp <= 0f) { return; }

                var go = other.gameObject;
                PlayerControllerB ply = go.GetComponent<PlayerControllerB>();
                EnemyAI ey = go.GetComponent<EnemyAI>();

                if (ply && (HitCooldown <= 0))
                {
                    int dmg = (int)(dmgRamp * BaseDmg * Plugin.FlameDmgMult.Value);
                    if (dmg >= ply.health)
                    {
                        KillPlayerClientRpc(ply.NetworkObject.NetworkObjectId);
                    }
                    else
                    {
                        DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, dmg);
                    }

                    HitCooldown = HitCooldownReset;
                }
                if (ey && (LastHitEnemyCooldown <= 0))
                {
                    ey.HitEnemyClientRpc(3, -1, true);
                    LastHitEnemyCooldown = HitCooldownReset;
                }
            }
        }
    }
}
