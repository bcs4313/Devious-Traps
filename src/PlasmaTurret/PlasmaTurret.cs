using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using GameNetcodeStuff;
using static UnityEngine.UIElements.StylePropertyAnimationSystem;
using UnityEngine.AI;
using Unity.Burst.CompilerServices;

namespace DeviousTraps.src
{
    internal class PlasmaTurret : NetworkBehaviour
    {
        public AudioSource AudioPlasmaTurretOnClicking;
        public AudioSource AudioPlasmaTurretOn;
        public AudioSource AudioPlasmaQuickCharge;
        public AudioSource AudioPlasmaOnSwitch;
        public AudioSource AudioFireBall;
        public AudioSource AudioPowerDown;
        public GameObject ActivationGroup;
        public PlayerControllerB TargetPlayer;
        public Transform PlasmaSpawnPoint;

        public Transform TurretSwivel;

        // Ammo Related
        bool Reloading = false;
        public AudioSource AudioReload;
        public AudioSource AudioDoneReloading;
        private float TimeUntilDoneReloading = 0;
        private float TimeUntilBurstReady = 0;
        private int CurrentAmmo = 0;
        private int CurrentBursts = 0;

        internal Animator animator;

        bool On = false;

        public void Start()
        {
            ActivationGroup.SetActive(false);
            animator = GetComponent<Animator>();
            AudioPlasmaTurretOn.volume = Plugin.PlasmaTurretVolume.Value;
            AudioFireBall.volume = Plugin.PlasmaTurretVolume.Value;
            AudioReload.volume = Plugin.PlasmaTurretVolume.Value;
            AudioDoneReloading.volume = Plugin.PlasmaTurretVolume.Value;
            AudioPowerDown.volume = Plugin.PlasmaTurretVolume.Value;
            
            if (RoundManager.Instance.IsHost)
            {
                try
                {
                    PositionShiftForFiring();
                }
                catch (Exception e) { Debug.LogError(e); }
            }
        }

        public override void OnNetworkSpawn()
        {
            SyncTransformClientRpc(transform.position, transform.rotation);
        }

        [ClientRpc]
        public void SyncTransformClientRpc(Vector3 pos, Quaternion rot)
        {
            if(RoundManager.Instance.IsHost) { return; }

            transform.position = pos;
            transform.rotation = rot;
        }

        float WindUpVolumeMultiplier = 0f;

        public BoxCollider ShiftColliderArea;


        public static Vector3 shiftVector = new Vector3(0, 1f, 0);
        bool IsCeilingMounted = false;
        // Handles placing the plasma turret so it may appear on the roof
        // if possible given the samples
        public void PositionShiftForFiring()
        {
            // read turret box shape
            Collider col = ShiftColliderArea;
            if (col == null)
                return;

            Vector3 halfExtents = col.bounds.extents;
            LayerMask mask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;

            // push upward up to ~15m
            float maxShift = 15f;
            float step = 2f;

            // shift phase 1 -> attempt to stick to roof
            for (float d = step; d <= maxShift; d += step)
            {
                Vector3 candidate = transform.position + transform.up * d;

                // shift phase 1 -> attempt to stick to roof
                var rayResult = Physics.Raycast(candidate, shiftVector, out RaycastHit rayHit, maxShift, mask);

                // basic raycast hit condition
                if (rayResult)
                {
                    Debug.Log("Devious Traps: Attached plasma turret to roof. Starting Position: " + transform.position + " End Position: " + rayHit.point);
                    transform.position = rayHit.point;
                    IsCeilingMounted = true;
                    // flip the root transform upside down so the turret hangs from the ceiling
                    transform.rotation = Quaternion.Euler(180f, transform.eulerAngles.y, transform.eulerAngles.z);
                    return;
                }
            }
            Debug.Log("Devious Traps: Plasma Turret Roof attachment failure. This doesn't break the turret, it just means it will shoot on the floor instead.");
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
            if (RoundManager.Instance.IsHost) { OnOffConditional(); };

            AudioPlasmaTurretOn.volume = Plugin.PlasmaTurretVolume.Value * WindUpVolumeMultiplier;
            if (On)
            {
                if (RoundManager.Instance.IsHost && !AudioPlasmaTurretOn.isPlaying)
                {
                    ToggleTurretOnClientRpc(true);
                }

                if (WindUpVolumeMultiplier < 1)
                {
                    WindUpVolumeMultiplier += Plugin.PlasmaWindupTime.Value * Time.deltaTime;
                }
                AITick();
            }
            else
            {
                if(RoundManager.Instance.IsHost && AudioPlasmaTurretOn.isPlaying)
                {
                    ToggleTurretOnClientRpc(false);
                }

                if (WindUpVolumeMultiplier > 0.25f)
                {
                    WindUpVolumeMultiplier -= Plugin.PlasmaWindupTime.Value * Time.deltaTime;
                }
            }

            if (RoundManager.Instance.IsHost)
            {
                if (TimeUntilDoneReloading < 0 && Reloading == true)
                {
                    Reloading = false;
                    PlayFinishReloadingClientRpc();
                    CurrentAmmo = Plugin.PlasmaBallsPerBurst.Value;
                    CurrentBursts = Plugin.PlasmaBurstQuantity.Value-1;
                }
                else if (Reloading == false)
                {
                    if (CurrentAmmo <= 0 && CurrentBursts <= 0)
                    {
                        Reloading = true;
                        TimeUntilDoneReloading = Plugin.PlasmaReloadTime.Value;
                        PlayReloadingClientRpc();
                    }
                    else if(CurrentAmmo <= 0)
                    {
                        BurstCooldownTime = Plugin.PlasmaBurstDelay.Value;
                        CurrentAmmo = Plugin.PlasmaBallsPerBurst.Value;
                        CurrentBursts -= 1;
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
            BurstCooldownTime -= Time.deltaTime;
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
        }

        [ClientRpc]
        public void PlayFinishReloadingClientRpc()
        {
            AudioDoneReloading.Play();
            AudioReload.Stop();
        }

        bool MidBurst = false;

        public void OnOffConditional()
        {
            float closestDist = Plugin.PlasmaTargetRange.Value;
            PlayerControllerB best = null;

            var players = RoundManager.Instance.playersManager.allPlayerScripts;

            foreach (var ply in players)
            {
                if (ply == null) continue;
                if (ply.isPlayerDead) continue;

                // origin/target similar to vanilla turret
                Vector3 origin = transform.position + (IsCeilingMounted ? -transform.up : transform.up) * 1.2f;
                Vector3 target = ply.gameplayCamera.transform.position;

                float dist = Vector3.Distance(origin, target);
                if (dist > Plugin.PlasmaTargetRange.Value) continue;

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
                if (TargetPlayer != best) { }

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
        float BurstCooldownTime = 0f;
        public void AITick()
        {
            if(!RoundManager.Instance.IsHost) { return; }

            facePosition(TargetPlayer.transform.position);

            if (BurstCooldownTime < 0f)
            {
                if (WindTime > Plugin.PlasmaWindupTime.Value && CooldownTime < 0f)
                {
                    CooldownTime = Plugin.PlasmaProjDelay.Value;
                    Fire();
                }
            }
        }

        public static float ycorrect = 90f;
        public static float zcorrect = 90f;

        public static Quaternion FireRotationOffset = Quaternion.Euler(0f, 0, 0f);

        public void Fire()
        {
            // direction fired is toward player if within cone, otherwise turret orientation
            Vector3 toPlayer = (TargetPlayer.transform.position - PlasmaSpawnPoint.position).normalized;
            float angle = Vector3.Angle(TurretSwivel.forward, toPlayer);
            Vector3 dir = angle <= 30f ? toPlayer : TurretSwivel.forward;

            // Spawn with rotation matching the direction
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            // FIXED AXIS OFFSET — adjust ONCE based on prefab orientation
            // Common cases:
            //   +90 X  -> mesh was Y-forward
            //   +90 Y  -> mesh was X-forward
            Quaternion finalRot = rot * FireRotationOffset;

            GameObject plasmaBall = Instantiate(
                Plugin.PlasmaBallPrefab,
                PlasmaSpawnPoint.position,
                finalRot
            );

            var comp = plasmaBall.GetComponent<PlasmaBall>();

            // Apply launch force (server-side)
            comp.speed = Plugin.PlasmaProjectileSpeed.Value;

            // Scale based on turret scale
            comp.SetScale(transform.localScale.y);

            // Spawn over network
            comp.GetComponent<NetworkObject>().Spawn();
            PlayLaunchSoundClientRpc();
            CurrentAmmo -= 1;
        }



        [ClientRpc]
        public void PlayLaunchSoundClientRpc()
        {
            AudioFireBall.Play();
        }

        [ClientRpc]
        public void ToggleTurretOnClientRpc(bool on)
        {
            if(on)
            {
                AudioPlasmaTurretOn.Play();
                AudioPlasmaOnSwitch.Play();
                AudioPlasmaTurretOnClicking.Play();
                ActivationGroup.SetActive(true);
                animator.Play("On");
            }
            else
            {
                AudioPlasmaTurretOn.Stop();
                AudioPlasmaTurretOnClicking.Stop();
                ActivationGroup.SetActive(false);
                animator.Play("Off");
            }
        }

        // facePosition uses easing to prevent the turret from "snapping" to players, making encounters more fair
        public void facePosition(Vector3 pos)
        {
            Vector3 directionToTarget = pos - TurretSwivel.position;
            if (directionToTarget == Vector3.zero) return;

            // when ceiling mounted the root is flipped 180, so transform direction into local space
            if (IsCeilingMounted)
                directionToTarget = transform.InverseTransformDirection(directionToTarget);

            // Desired rotation that points directly at the target on all axes
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

            // read current in local space to match what we write back
            Vector3 currentEuler = TurretSwivel.localRotation.eulerAngles;
            Vector3 targetEuler = targetRotation.eulerAngles;

            float speed = Time.deltaTime * Plugin.PlasmaRotationSpeed.Value;

            // X = pitch (vertical aim), Y = yaw (horizontal aim)
            // Use MoveTowardsAngle so wrap-around at 360 is handled correctly.
            float pitch = Mathf.MoveTowardsAngle(currentEuler.x, targetEuler.x, speed);
            float yaw = Mathf.MoveTowardsAngle(currentEuler.y, targetEuler.y, speed);

            // Keep roll (Z) at 0 so the turret doesn't tilt sideways.
            TurretSwivel.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

    }
}
