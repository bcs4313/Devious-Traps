using GameNetcodeStuff;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DeviousTraps.src
{
    public class FlameTurret : NetworkBehaviour
    {
        public AudioSource AudioFlameTurretOn;
        public AudioSource AudioFireFlames;
        public AudioSource AudioReload;
        public AudioSource AudioSmokeRelease;
        public AudioSource AudioPowerDown;
        // new activation group consists of many components
        //ACTIVATION GROUP
        //public GameObject ActivationGroup;
        bool Activated = false;
        public ParticleSystem Fire1;
        public ParticleSystem Fire2;
        public ParticleSystem Fire3;
        public Light Light1;
        public Light Light2;
        public Light Light3;
        public FlameHitbox H1;
        public FlameHitbox H2;
        public FlameHitbox H3;
        //ACTIVATION GROUP

        public GameObject pillar; // pillar that moves up/down
        public GameObject SmokeSystem;

        internal Animator animator;

        bool On = false;          // current active state
        bool lastOn = false;      // last state on host (for change detection)

        [Tooltip("How high the pillar moves up from its starting local Y (world units).")]
        public float ElevationAmount = 2.2f;

        float t_SmokeReleaseTotal = 0f;

        // runtime state
        float t_SpinningTimeTotal = 0f;
        float t_StandbyTimeTotal = 0f;
        float t_ElevationState = 0f;   // 0..1, 0 = lowered, 1 = raised

        bool InStandby = false;
        float pillarBaseY;   // starting local Y of pillar

        public void Start()
        {
            Activated = false;
            animator = GetComponent<Animator>();

            pillarBaseY = pillar.transform.localPosition.y;

            AudioFlameTurretOn.volume = Plugin.FlameVolume.Value;
            AudioFireFlames.volume = Plugin.FlameVolume.Value;
            AudioReload.volume = Plugin.FlameVolume.Value;
            AudioSmokeRelease.volume = Plugin.FlameVolume.Value / 9f;
            AudioPowerDown.volume = Plugin.FlameVolume.Value;
        }

        public void Update()
        {
            // Only host decides if turret should be on
            if (IsServer)
            {
                OnOffConditional();

                // if state changed, push it to clients
                if (On != lastOn)
                {
                    SetOnStateClientRpc(On);
                    // handle on/off sound once per change
                    ToggleTurretOnClientRpc(On);
                    lastOn = On;
                }
            }

            if (RoundManager.Instance.IsHost)
            {
                t_SmokeReleaseTotal -= Time.deltaTime;
                if (t_SmokeReleaseTotal < 0f)
                {
                    t_SmokeReleaseTotal = Plugin.SmokeCooldown.Value;
                    PlaySmokeReleaseClientRpc();
                }
            }
            TimeUntilEnabled -= Time.deltaTime;

            if (Inactive && TimeUntilEnabled < 0f)
            {
                TerminalToggleClientRpc(true);
            }

            // Everyone (host + clients) runs the animation/cycle locally
            AITick();
        }

        public float TimeUntilEnabled = -1f;
        public bool Inactive = false;
        [ClientRpc]
        public void TerminalToggleClientRpc(bool turnOn)
        {
            if (turnOn)
            {
                TimeUntilEnabled = -1f;
                Inactive = false;
            }
            else
            {
                TimeUntilEnabled = 7f;
                Inactive = true;
                AudioPowerDown.Play();
            }
        }

        public void OnOffConditional()
        {
            float closestDist = Plugin.FlameTargetRange.Value;
            PlayerControllerB best = null;

            var players = RoundManager.Instance.playersManager.allPlayerScripts;

            foreach (var ply in players)
            {
                if (ply == null) continue;
                if (ply.isPlayerDead) continue;

                Vector3 origin = transform.position + Vector3.up * 1.2f;
                Vector3 target = ply.gameplayCamera.transform.position;

                float dist = Vector3.Distance(origin, target);
                if (dist > Plugin.FlameTargetRange.Value) continue;

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

            On = (best != null) && !Inactive;
        }

        void SetPillarHeight(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            t_ElevationState = normalized;

            // ElevationAmount is in world units; cancel parent scaling
            float scaleY = transform.lossyScale.y;
            if (scaleY <= 0f) scaleY = 1f;

            float localHeightDelta = ElevationAmount / scaleY;

            var pos = pillar.transform.localPosition;
            pos.y = pillarBaseY + localHeightDelta * normalized;
            pillar.transform.localPosition = pos;
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

        public void AITick()
        {
            if (Inactive)
            {
                if (Activated && IsServer)
                {
                    SetGroupActiveClientRpc(false);
                }

                if (AudioFlameTurretOn.isPlaying)
                    StopFireClientRpc();

                return;
            }

            if (!InStandby)
            {
                // rising phase
                if (t_ElevationState < 1f)
                {
                    if (!AudioFlameTurretOn.isPlaying)
                        PlayFireClientRpc();
                    float delta = Time.deltaTime / Plugin.FlameRisingTime.Value;
                    SetPillarHeight(t_ElevationState + delta);
                    return;
                }

                // spinning phase
                if (t_SpinningTimeTotal < Plugin.FlameSpinningTime.Value)
                {
                    if (IsServer && !Activated)
                    {
                        SetGroupActiveClientRpc(true); // flames on
                    }

                    t_SpinningTimeTotal += Time.deltaTime;
                    pillar.transform.Rotate(0f, Plugin.FlameRotationSpeed.Value * Time.deltaTime, 0f, Space.Self);
                    return;
                }
                else
                {
                    InStandby = true;
                    return;
                }
            }
            else
            {
                // flames off in standby
                if (IsServer && Activated)
                {
                    if (AudioFlameTurretOn.isPlaying)
                        StopFireClientRpc();
                    SetGroupActiveClientRpc(false);
                }

                // lowering phase
                if (t_ElevationState > 0f)
                {
                    if (!AudioReload.isPlaying)
                    {
                        PlayReloadClientRpc();
                    }

                    float delta = Time.deltaTime / Plugin.FlameRisingTime.Value;
                    SetPillarHeight(t_ElevationState - delta);
                    return;
                }

                // standby before rising again
                if (t_StandbyTimeTotal < Plugin.FlameRestingTime.Value)
                {
                    t_StandbyTimeTotal += Time.deltaTime;
                    return;
                }
                else if (On)
                {
                    // reset, now attacking
                    InStandby = false;
                    t_StandbyTimeTotal = 0f;
                    t_SpinningTimeTotal = 0f;
                    // t_ElevationState is 0, next tick we rise again
                }
            }
        }

        [ClientRpc]
        public void PlaySmokeReleaseClientRpc()
        {
            StartCoroutine(PlaySmokeCoroutine());
        }

        public IEnumerator PlaySmokeCoroutine()
        {
            SmokeSystem.SetActive(true);
            yield return new WaitForSeconds(4.5f);
            SmokeSystem.SetActive(false);
        }

        // === RPCs ===

        [ClientRpc]
        void SetOnStateClientRpc(bool value)
        {
            On = value;
        }

        [ClientRpc]
        public void SetGroupActiveClientRpc(bool value)
        {
            Activated = value;

            if (value)
            {
                AudioFireFlames.Play();
                Fire1.Play();
                Fire2.Play();
                Fire3.Play();
                Light1.enabled = true;
                Light2.enabled = true;
                Light3.enabled = true;
                H1.enabled = true;
                H2.enabled = true;
                H3.enabled = true;
            }
            else
            {
                AudioFireFlames.Stop();
                Fire1.Stop();
                Fire2.Stop();
                Fire3.Stop();
                Light1.enabled = false;
                Light2.enabled = false;
                Light3.enabled = false;
                H1.enabled = false;
                H2.enabled = false;
                H3.enabled = false;
                H1.ResetDmgRamp();
                H2.ResetDmgRamp();
                H3.ResetDmgRamp();
            }
        }

        [ClientRpc]
        public void PlayFireClientRpc()
        {
            AudioFlameTurretOn.Play();
        }

        [ClientRpc]
        public void StopFireClientRpc()
        {
            AudioFlameTurretOn.Stop();
        }


        [ClientRpc]
        public void PlayReloadClientRpc()
        {
            AudioReload.Play();
        }

        [ClientRpc]
        public void StopReloadClientRpc()
        {
            AudioReload.Stop();
        }


        [ClientRpc]
        public void ToggleTurretOnClientRpc(bool on)
        {
            if (on)
            {
                if (AudioReload.isPlaying)
                {
                    AudioReload.Stop();
                }
            }
        }
    }
}
