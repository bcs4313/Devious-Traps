using GameNetcodeStuff;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace DeviousTraps.src.SoundCannon
{
    public class SoundProjectile : NetworkBehaviour
    {
        public LRADTurret HostObject = null;
        public float LocalShakeCooldown = 0.2f;
        public float lifetime = 4f;  // lasts for 4 seconds

        // --- Wall penetration config ---
        [Tooltip("Layers considered solid walls/props the round can punch through (and lose damage doing so).")]
        public LayerMask WallMask; // assign in inspector, e.g. Default | Room | InteractableObject
        [Tooltip("Layers considered valid hit targets (players/enemies) for the sweep check.")]
        public LayerMask HitMask; // assign in inspector, e.g. Player | Enemies
        [Tooltip("Physical radius of the round for sweep tests, keep small but non-zero.")]
        public float ProjectileRadius = 0.05f;
        [Tooltip("Total meters of wall thickness at which damage falloff bottoms out at MinDamageMultiplier.")]
        public float PenetrationFalloffDistance = 3f;
        [Tooltip("Damage multiplier once accumulated wall thickness reaches PenetrationFalloffDistance.")]
        public float MinDamageMultiplier = 0.25f;
        private float TotalWallThicknessPenetrated = 0f;

        public void Start()
        {
            // shake camera depending on proximity to blast
            LocalProximityShake();
        }

        Vector3 Velocity = Vector3.zero;
        public void SetVelocity(Vector3 _velocity)
        {
            Velocity = _velocity * Plugin.LRADProjectileSpeed.Value;
        }

        public void Update()
        {
            lifetime -= Time.deltaTime;
            LocalShakeCooldown -= Time.deltaTime;
            if (LocalShakeCooldown < 0)
            {
                LocalShakeCooldown = 0.2f;
                LocalProximityShake();
            }

            if (RoundManager.Instance.IsHost)
            {
                Vector3 prevPos = transform.position;
                Vector3 delta = Velocity * Time.deltaTime;

                // Movement is untouched — the round always keeps flying at full speed/trajectory.
                // We only use this cast to detect walls AND targets it passed through.
                CheckWallsPassedThrough(prevPos, delta);
                transform.position = prevPos + delta;

                if (lifetime <= 0)
                {
                    Destroy(this.gameObject);
                }
            }
        }

        /// <summary>
        /// Sweeps this frame's travel path once for players/enemies (HitMask) and once for
        /// walls (WallMask). Targets found get routed into HIT(); wall hits only tally
        /// thickness for damage falloff. Purely detection — never affects movement.
        /// </summary>
        private void CheckWallsPassedThrough(Vector3 prevPos, Vector3 delta)
        {
            float dist = delta.magnitude;
            if (dist <= 0f) return;
            Vector3 dir = delta / dist;

            // players/enemies along the swept path — this is what needs to reach HIT(),
            // NOT the wall collider found below.
            RaycastHit[] targetHits = Physics.SphereCastAll(prevPos, ProjectileRadius, dir, dist);
            foreach (var t in targetHits)
            {
                HIT(t.collider);
            }

            if (!Physics.SphereCast(prevPos, ProjectileRadius, dir, out RaycastHit entryHit, dist, WallMask, QueryTriggerInteraction.Ignore))
                return;

            Collider wall = entryHit.collider;

            float maxThicknessGuess = wall.bounds.size.magnitude + 0.1f;
            Vector3 farPoint = entryHit.point + dir * maxThicknessGuess;

            float thickness = 0.2f; // fallback estimate if no clean exit face found (e.g. solid/concave geo)
            if (wall.Raycast(new Ray(farPoint, -dir), out RaycastHit exitHit, maxThicknessGuess))
            {
                thickness = Vector3.Distance(entryHit.point, exitHit.point);
            }

            TotalWallThicknessPenetrated += thickness;
        }


        public void LocalProximityShake()
        {
            var localPly = RoundManager.Instance.playersManager.localPlayerController;
            var loc = localPly.gameObject.transform.position;
            var pos = this.transform.position;
            if (Vector3.Distance(loc, pos) < (14 * (Plugin.LRADTargetRange.Value / 50)))
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            }
            else if (Vector3.Distance(loc, pos) < (30 * (Plugin.LRADTargetRange.Value / 50)))
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
            else if (Vector3.Distance(loc, pos) < (42 * (Plugin.LRADTargetRange.Value / 50)))
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
        }

        public float thicknessMult = 1f;
        public List<ulong> victims = new List<ulong>();  // sound wave can only hit a player ONCE
        public void HIT(Collider other)
        {
            if (RoundManager.Instance.IsHost)
            {
                var go = other.gameObject;
                PlayerControllerB ply = playerParentWalk(go);
                EnemyAI ey = enemyParentWalk(go);

                //Debug.Log("HIT -> " + other + " walk -> " + ply);
                if (ply)
                {
                    thicknessMult = 1 + TotalWallThicknessPenetrated;
                    int dmg = Mathf.RoundToInt(28 / (1 + (thicknessMult * Plugin.LRADDmgPenaltyMult.Value)));
                    Debug.Log("LRAD Wave Dmg Player: " + " wall thickness penalty: " + thicknessMult + " total base dmg (without wall -> 28) = " + dmg);

                    // rounding, no adjustment over base dmg, for consistency
                    if (dmg > 23)
                    {
                        dmg = 28;
                    }

                    if (victims != null && !victims.Contains(ply.NetworkObject.NetworkObjectId))
                    {
                        victims.Add(ply.NetworkObject.NetworkObjectId);
                        if (dmg >= ply.health)
                        {
                            KillPlayerClientRpc(ply.NetworkObject.NetworkObjectId);
                        }
                        else
                        {
                            DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, dmg);
                            var impact = Instantiate<GameObject>(Plugin.LRADImpactPrefab, this.transform.position, this.transform.rotation);

                            // impact now receives a penalty to its power if dmg is 15 or lower
                            ImpactFXState IState = impact.GetComponent<ImpactFXState>();
                            var power = IState.Power * Mathf.Min(1f, 1 * ((dmg + 0.01f) / 20));
                            IState.AttachToPlayer(ply.NetworkObjectId, power);
                            impact.GetComponent<NetworkObject>().Spawn();
                        }
                    }
                }
                if (ey)
                {
                    if (victims != null && !victims.Contains(ey.NetworkObject.NetworkObjectId))
                    {
                        victims.Add(ey.NetworkObject.NetworkObjectId);
                        ey.HitEnemyClientRpc(6, -1, true);
                    }
                }
            }
        }


        // goes up the parent tree until it finds player or null
        public PlayerControllerB playerParentWalk(GameObject leaf)
        {
            while (leaf != null && leaf.GetComponent<PlayerControllerB>() == null)
            {
                if (leaf.transform.parent && leaf.transform.parent.gameObject)
                {
                    leaf = leaf.transform.parent.gameObject;
                }
                else
                {
                    leaf = null;
                }
            }

            if (leaf && leaf.GetComponent<PlayerControllerB>())
            {
                return leaf.GetComponent<PlayerControllerB>();
            }

            return null;
        }

        // goes up the enemy tree until it finds player or null
        public EnemyAI enemyParentWalk(GameObject leaf)
        {
            while (leaf != null && leaf.GetComponent<EnemyAI>() == null)
            {
                if (leaf.transform.parent && leaf.transform.parent.gameObject)
                {
                    leaf = leaf.transform.parent.gameObject;
                }
                else
                {
                    leaf = null;
                }
            }

            if (leaf && leaf.GetComponent<EnemyAI>())
            {
                return leaf.GetComponent<EnemyAI>();
            }

            return null;
        }


        [ClientRpc]
        public void DmgPlayerClientRpc(ulong netid, int amount)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var ply in players)
            {
                if (ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.DamagePlayer((int)(amount * Plugin.LRADDmgMult.Value));
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
                    ply.KillPlayer(ply.playerRigidbody.velocity * 2, true, CauseOfDeath.Blast);
                }
            }
        }
    }
}