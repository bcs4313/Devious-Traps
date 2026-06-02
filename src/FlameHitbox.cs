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

        public void Start()
        {
            WorldMask = BuildWorldMask();
        }

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

        private LayerMask WorldMask;        // environment-only mask
        // Build a sane environment-only mask based on layer names
        // === Replace BuildWorldMask() with this ===
        private LayerMask BuildWorldMask()
        {
            // Layers considered "static world". Add/remove to match your project.
            string[] include = new string[]
            {
                "Default",              // keep only if your static level uses Default
                "Room",
                "Colliders",
                "MiscLevelGeometry",
                "Terrain",
                "Railing",
                "DecalStickableSurface"
            };
            // Layers to force-exclude (players, enemies, triggers, ragdolls, etc.)
            string[] exclude = new string[]
            {
                "Player",
                "Enemy",
                "Enemies",
                "Players",
                "Ragdoll",
                "Trigger",
                "Ignore Raycast",
                "UI",
            };
            int mask = 0;
            foreach (var n in include)
            {
                int li = LayerMask.NameToLayer(n);
                if (li >= 0) mask |= 1 << li;
                else Debug.LogWarning($"[WorldMask] Include layer \"{n}\" not found.");
            }
            foreach (var n in exclude)
            {
                int li = LayerMask.NameToLayer(n);
                if (li >= 0) mask &= ~(1 << li);
            }
            // Also exclude our own current layer to avoid self-hits.
            mask &= ~(1 << gameObject.layer);
            if (mask == 0)
            {
                Debug.LogWarning("[WorldMask] Mask resolved to 0; falling back to Physics.DefaultRaycastLayers.");
                mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
            }
            //Debug.Log($"[WorldMask] Built mask={mask} (self layer excluded: {LayerMask.LayerToName(gameObject.layer)})");
            return mask;
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
                    // requirement for the parent turret to "see" the player its damaging.
                    // this prevents the hitbox from hitting through walls
                    if (Physics.Linecast(gameObject.transform.parent.transform.position, ply.transform.position, out RaycastHit hit, WorldMask))
                    {
                        if (!hit.collider.transform.IsChildOf(ply.transform))
                            return; // something blocks view, do not damage player
                    }

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
