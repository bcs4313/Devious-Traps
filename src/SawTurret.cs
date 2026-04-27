using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using GameNetcodeStuff;
using static UnityEngine.UIElements.StylePropertyAnimationSystem;
using UnityEngine.AI;

namespace DeviousTraps.src
{
    internal class SawTurret : NetworkBehaviour
    {
        public AudioSource AudioSawTurretOn;
        public AudioSource AudioLaunchSaw;
        public AudioSource AudioPowerDown;
        public GameObject ActivationGroup;
        public PlayerControllerB TargetPlayer;
        public Transform SawSpawnPoint;

        // Ammo Related
        bool Reloading = false;
        public GameObject VisibleSawBlade;
        public AudioSource AudioReload;
        public AudioSource AudioDoneReloading;
        private float TimeUntilDoneReloading = 0;
        private int CurrentAmmo = 0;

        internal Animator animator;

        bool On = false;

        public void Start()
        {
            ActivationGroup.SetActive(false);
            VisibleSawBlade.SetActive(true);
            animator = GetComponent<Animator>();
            AudioSawTurretOn.volume = Plugin.SawVolume.Value / 1.35f;
            AudioLaunchSaw.volume = Plugin.SawVolume.Value / 1.35f;
            AudioReload.volume = Plugin.SawVolume.Value / 1.35f;
            AudioDoneReloading.volume = Plugin.SawVolume.Value / 1.35f;
            AudioPowerDown.volume = Plugin.SawVolume.Value / 1.2f;
            if (RoundManager.Instance.IsHost)
            {
                PositionShiftForFiring();
            }
        }

        float WindUpVolumeMultiplier = 0f;

        public BoxCollider ShiftColliderArea; 
        // Handles placing the saw turret so its much less likely to fire into a wall
        // if possible given the samples
        public void PositionShiftForFiring()
        {
            // read turret box shape
            Collider col = ShiftColliderArea;
            if (col == null)
                return;

            Vector3 halfExtents = col.bounds.extents;
            LayerMask mask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;

            // candidate search directions
            Vector3[] directions =
            {
                transform.forward,
                -transform.forward,
                transform.right,
                -transform.right
            };

            // push outward up to ~1.5m
            const float maxShift = 3f;
            const float step = 0.2f;

            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 dir = directions[i];

                for (float d = step; d <= maxShift; d += step)
                {
                    Vector3 candidate = transform.position + dir * d;

                    // must sit on navmesh
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.35f, NavMesh.AllAreas))
                        continue;

                    // ensure collider box does not overlap world geometry
                    bool intersect = Physics.CheckBox(
                        hit.position + Vector3.up * 0.5f,
                        halfExtents,
                        Quaternion.identity,
                        mask,
                        QueryTriggerInteraction.Ignore
                    );

                    if (!intersect)
                    {
                        transform.position = hit.position;
                        return;
                    }
                }
            }
        }

        public void TerminalDisableTurretMethod()
        {
            if (RoundManager.Instance.IsHost)
            {
                TerminalToggleClientRpc(false);
            }
            else
            {
                TerminalToggleServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TerminalToggleServerRpc()
        {
            TerminalToggleClientRpc(false);
        } 

        public float TimeUntilEnabled = -1f;
        public bool AnimationToggle = false;
        public bool Inactive = false;
        [ClientRpc]
        public void TerminalToggleClientRpc(bool turnOn)
        {
            if(turnOn)
            {
                TimeUntilEnabled = -1f;
                AnimationToggle = false;
                Inactive = false;
            }
            else
            {
                TimeUntilEnabled = 7f;
                AnimationToggle = true;
                Inactive = true;
                AudioPowerDown.Play();
            }
        }


        public void Update()
        {
            // conditional for turning turret on and off
            if (RoundManager.Instance.IsHost) { OnOffConditional();  };

            AudioSawTurretOn.volume = Plugin.SawVolume.Value * WindUpVolumeMultiplier;
            if (On)
            {
                if (RoundManager.Instance.IsHost && !AudioSawTurretOn.isPlaying)
                {
                    ToggleTurretOnClientRpc(true);
                }

                if (WindUpVolumeMultiplier < 1)
                {
                    WindUpVolumeMultiplier += Plugin.SawWindupTime.Value * Time.deltaTime;
                }
                AITick();
            }
            else
            {
                if(RoundManager.Instance.IsHost && AudioSawTurretOn.isPlaying)
                {
                    ToggleTurretOnClientRpc(false);
                }

                if (WindUpVolumeMultiplier > 0.25f)
                {
                    WindUpVolumeMultiplier -= Plugin.SawWindupTime.Value * Time.deltaTime;
                }
            }

            if (RoundManager.Instance.IsHost)
            {
                if (TimeUntilDoneReloading < 0 && Reloading == true)
                {
                    Reloading = false;
                    PlayFinishReloadingClientRpc();
                    CurrentAmmo = Plugin.SawAmmo.Value;
                }
                else if (Reloading == false)
                {
                    if (CurrentAmmo <= 0)
                    {
                        Reloading = true;
                        TimeUntilDoneReloading = Plugin.SawReloadTime.Value;
                        PlayReloadingClientRpc();
                    }
                }
            }

            // Turn on animation activation
            if(TimeUntilEnabled < 0f && AnimationToggle)
            {
                // trigger animation
                AnimationToggle = false;  
                if (animator.GetCurrentAnimatorStateInfo(1).IsName("ToEnabled"))
                {
                    PlayTurnOnClientRpc();
                }
            }

            // turn on condition
            if (Inactive && TimeUntilEnabled < 0f && !animator.GetCurrentAnimatorStateInfo(1).IsName("ToEnabled"))
            {
                TerminalToggleClientRpc(true);
            }

            TimeUntilDoneReloading -= Time.deltaTime;
            WindTime += Time.deltaTime;
            CooldownTime -= Time.deltaTime;
            TimeUntilEnabled -= Time.deltaTime;
        }

        [ClientRpc]
        public void PlayTurnOnClientRpc()
        {
            animator.Play("ToEnabled");
        }


        [ClientRpc]
        public void PlayReloadingClientRpc()
        {
            AudioReload.Play();
            VisibleSawBlade.SetActive(false);
        }

        [ClientRpc]
        public void PlayFinishReloadingClientRpc()
        {
            AudioDoneReloading.Play();
            AudioReload.Stop();
            VisibleSawBlade.SetActive(true);
        }

        public void OnOffConditional()
        {
            float closestDist = Plugin.SawTargetRange.Value;
            PlayerControllerB best = null;

            var players = RoundManager.Instance.playersManager.allPlayerScripts;

            foreach (var ply in players)
            {
                if (ply == null) continue;
                if (ply.isPlayerDead) continue;

                // origin/target similar to vanilla turret
                Vector3 origin = transform.position + Vector3.up * 1.2f; // tweak as needed
                Vector3 target = ply.gameplayCamera.transform.position;

                float dist = Vector3.Distance(origin, target);
                if (dist > Plugin.SawTargetRange.Value) continue;

                bool blocked = Physics.Linecast(
                    origin,
                    target,
                    StartOfRound.Instance.collidersAndRoomMask,
                    QueryTriggerInteraction.Ignore
                );

                if (!blocked && dist < closestDist)
                {
                    closestDist = dist;
                    best = ply;
                }
            }

            if ((best != null && CurrentAmmo > 0) && !Inactive)
            {
                if(TargetPlayer != best) { }

                SetTargetPlayerClientRpc(best.NetworkObject.NetworkObjectId);
                TurnOnClientRpc(true);
            }
            else
            {
                SetTargetPlayerClientRpc(9999999);
                TurnOnClientRpc(false);
                WindTime = 0f;
                CooldownTime = 0f;
            }
        }

        [ClientRpc]
        public void TurnOnClientRpc(bool value)
        {
            On = value;
        }

        [ClientRpc]
        public void SetTargetPlayerClientRpc(ulong netid)
        {
            if(netid == 9999999) { TargetPlayer = null; }
            else
            {
                var players = RoundManager.Instance.playersManager.allPlayerScripts;

                foreach (var ply in players)
                {
                    if (ply == null) continue;
                    if (ply.isPlayerDead) continue;
                    if(ply.NetworkObject.NetworkObjectId == netid)
                    {
                        TargetPlayer = ply;
                    }
                }
            }
        }

        // point and fire variables
        float WindTime = 0f;
        float CooldownTime = 0f;
        public void AITick()
        {
            facePosition(TargetPlayer.transform.position);

            if(!RoundManager.Instance.IsHost) { return; }
            if(WindTime > Plugin.SawWindupTime.Value && CooldownTime < 0f)
            {
                CooldownTime = Plugin.SawFirerate.Value;
                Fire();
            }
        }

        public static float ycorrect = 90f;
        public static float zcorrect = 90f;
        //public static float SawLaunchForce = 3000f; default
        public void Fire()
        {
            // direction fired is toward player if within cone, otherwise turret orientation
            Vector3 toPlayer = (TargetPlayer.transform.position - SawSpawnPoint.position).normalized;
            float angle = Vector3.Angle(transform.forward, toPlayer);
            Vector3 dir = angle <= 30f ? toPlayer : transform.forward;

            // Spawn with rotation matching the direction
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            // FIXED AXIS OFFSET — adjust ONCE based on prefab orientation
            // Common cases:
            //   +90 X  -> mesh was Y-forward
            //   +90 Y  -> mesh was X-forward
            Quaternion axisFix = Quaternion.Euler(0f, ycorrect, zcorrect);
            Quaternion finalRot = rot * axisFix;

            GameObject blade = Instantiate(
                Plugin.SawPrefab,
                SawSpawnPoint.position,
                finalRot
            );

            // Apply launch force (server-side)
            blade.GetComponent<SawProjectileV2>().LaunchForce = dir * Plugin.SawLaunchSpeed.Value;
            // Scale based on turret scale
            blade.GetComponent<SawProjectileV2>().SetScale(transform.localScale.y);
            // Spawn over network
            blade.GetComponent<NetworkObject>().Spawn();
            PlayLaunchSoundClientRpc();
            CurrentAmmo -= 1;
        }

        [ClientRpc]
        public void PlayLaunchSoundClientRpc()
        {
            AudioLaunchSaw.Play();
        }

        [ClientRpc]
        public void ToggleTurretOnClientRpc(bool on)
        {
            if(on)
            {
                AudioSawTurretOn.Play();
                ActivationGroup.SetActive(true);
                animator.Play("On");
            }
            else
            {
                AudioSawTurretOn.Stop();
                ActivationGroup.SetActive(false);
                animator.Play("Off");
            }
        }

        // facePosition uses easing to prevent the turret from "snapping" to players, making encounters more fair
        public void facePosition(Vector3 pos)
        {
            Vector3 directionToTarget = pos - transform.position;
            directionToTarget.y = 0f; // Ignore vertical difference
            if (directionToTarget != Vector3.zero)
            {
                // use Lerp angle adjustment to achieve target rotation
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                // rotate at a specific speed, using DeltaTime to prevent fps specific speed differences 
                float EulerYTarget = Mathf.MoveTowardsAngle(transform.rotation.eulerAngles.y, targetRotation.eulerAngles.y, Time.deltaTime * Plugin.SawRotationSpeed.Value);

                transform.rotation = Quaternion.Euler(0f, EulerYTarget, 0f);
            }
        }
    }
}
