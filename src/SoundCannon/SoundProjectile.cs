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
                // We only use this cast to detect walls it passed through, purely to tally
                // damage falloff. It never stops, slows, or redirects the projectile.
                CheckWallsPassedThrough(prevPos, delta);
                transform.position = prevPos + delta;

                if (lifetime <= 0)
                {
                    Destroy(this.gameObject);
                }
            }
        }

        /// <summary>
        /// Detects any wall the round crossed this frame and adds its thickness to
        /// TotalWallThicknessPenetrated, which lowers CurrentDamageMultiplier. Purely
        /// a damage-falloff tally — does not affect movement, speed, or trajectory at all.
        /// </summary>
        private void CheckWallsPassedThrough(Vector3 prevPos, Vector3 delta)
        {
            float dist = delta.magnitude;
            if (dist <= 0f) return;
            Vector3 dir = delta / dist;

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

            SpawnWallImpactFX(entryHit.point, entryHit.normal);
            TotalWallThicknessPenetrated += thickness;
        }

        private void SpawnWallImpactFX(Vector3 point, Vector3 normal)
        {
            if (Plugin.LRADImpactPrefab == null) return;
            var impact = Instantiate(Plugin.LRADImpactPrefab, point, UnityEngine.Quaternion.LookRotation(normal));
            var netObj = impact.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();
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
        public static float thicknessPenaltyMultiplier = 0.05f;
        public void OnTriggerEnter(Collider other)
        {
            //Debug.Log("Flame hit: " + other.gameObject);
            if (RoundManager.Instance.IsHost)
            {
                var go = other.gameObject;
                PlayerControllerB ply = go.GetComponent<PlayerControllerB>();
                EnemyAI ey = go.GetComponent<EnemyAI>();
                if (ply)
                {
                    thicknessMult = 1 + TotalWallThicknessPenetrated;
                    int dmg = Mathf.RoundToInt(25 / (1 + (thicknessMult * thicknessPenaltyMultiplier)));
                    Debug.Log("LRAD Wave Dmg Player: " + " wall thickness penalty: " + thicknessMult + " total base dmg (without wall -> 25) = " + dmg);

                    // rounding, no adjustment over 15 base dmg, for consistency
                    if(dmg > 20)
                    {
                        dmg = 25;
                    }

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
                        IState.Power *= Mathf.Min(1f, 1 * ((dmg + 0.01f) / 20));
                        IState.AttachToPlayer(ply.NetworkObjectId);
                        impact.GetComponent<NetworkObject>().Spawn();
                    }
                }
                if (ey)
                {
                    ey.HitEnemyClientRpc(6, -1, true);
                }
            }
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