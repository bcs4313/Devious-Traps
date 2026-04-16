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
                transform.position = this.transform.position + (Velocity * Time.deltaTime);
                if(lifetime <= 0)
                {
                    Destroy(this.gameObject);
                }
            }
        }

        public void LocalProximityShake()
        {
            var localPly = RoundManager.Instance.playersManager.localPlayerController;
            var loc = localPly.gameObject.transform.position;
            var pos = this.transform.position;
            if(Vector3.Distance(loc, pos) < (14 * (Plugin.LRADTargetRange.Value / 50)))
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            }
            else if(Vector3.Distance(loc, pos) < (30 * (Plugin.LRADTargetRange.Value / 50)))
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
            else if (Vector3.Distance(loc, pos) < (42 * (Plugin.LRADTargetRange.Value/50)))
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
        }

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
                    int dmg = 20;
                    if (dmg >= ply.health)
                    {
                        KillPlayerClientRpc(ply.NetworkObject.NetworkObjectId);
                    }
                    else
                    {
                        DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, dmg);
                        var impact = Instantiate<GameObject>(Plugin.LRADImpactPrefab, this.transform.position, this.transform.rotation);
                        ImpactFXState IState = impact.GetComponent<ImpactFXState>();
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
