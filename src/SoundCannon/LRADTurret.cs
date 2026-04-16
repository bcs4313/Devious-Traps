using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using GameNetcodeStuff;
using static UnityEngine.UIElements.StylePropertyAnimationSystem;
using DeviousTraps.src.SoundCannon;
using UnityEngine.AI;
using System.Collections;
using System.Threading.Tasks;

namespace DeviousTraps.src
{
    public class LRADTurret : NetworkBehaviour
    {
        public AudioSource AudioLRADOn;
        public AudioSource AudioFire;
        public AudioSource AudioPowerDown;
        public GameObject ActivationGroup;
        public PlayerControllerB TargetPlayer;

        public Transform RotationPoint;

        // Sound Wave Spawn Points
        public Transform WaveSpawnPoint1;
        public Transform WaveSpawnPoint2;

        // Ammo Related
        bool Reloading = false;
        public AudioSource AudioReload;
        public AudioSource AudioDoneReloading;
        private float TimeUntilDoneReloading = 0;
        private int CurrentAmmo = 0;

        // Indicator for the trap being off
        public Transform SpeakerTransform;

        //internal Animator animator;

        public static System.Random rnd = new System.Random();

        bool On = false;

        public void Start()
        {
            TimeUntilDoneReloading = Plugin.LRADReloadTime.Value;
            ActivationGroup.SetActive(false);
            //animator = GetComponent<Animator>();
            AudioLRADOn.volume = Plugin.LRADVolume.Value;
            AudioFire.volume = Plugin.LRADVolume.Value;
            AudioReload.volume = Plugin.LRADVolume.Value;
            AudioDoneReloading.volume = Plugin.LRADVolume.Value;
            AudioPowerDown.volume = Plugin.LRADVolume.Value;
            upTransitionSound.volume = Plugin.LRADVolume.Value;
            downTransitionSound.volume = Plugin.LRADVolume.Value;
            // lower pitch of sample with longer charge time
            AudioLRADOn.pitch = 5.53f / Plugin.LRADChargeTime.Value;

            if (RoundManager.Instance.IsHost)
            {
                ProjectToRandomOutsideLocation();
            } 
        }

        public static int EntranceTendency = 6;
        public void ProjectToRandomOutsideLocation()
        {
            Vector3 BestProjection = transform.position;
            float bestDist = 99999f;

            for (int i = 0; i < EntranceTendency; i++)
            {
                // move to outside node
                var SpawnTargets = RoundManager.Instance.outsideAINodes;
                var NodeTarget = SpawnTargets[rnd.Next(0, SpawnTargets.Length)];
                var Disposition = NodeTarget.transform.position + new Vector3(rnd.Next(-5, 5), rnd.Next(-5, 5), rnd.Next(-5, 5));

                NavMeshHit hit;
                var result = NavMesh.SamplePosition(Disposition, out hit, 20f, NavMesh.AllAreas);

                Vector3 curPos;
                if (result)
                {
                    curPos = hit.position;
                }
                else
                {
                    curPos = NodeTarget.transform.position;
                }

                var dist = DistanceToEntrance(curPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    BestProjection = curPos;
                }
            }

            ForwardPositionClientRpc(BestProjection);
        }

        [ClientRpc]
        public void ForwardPositionClientRpc(Vector3 pos)
        {
            transform.position = pos;
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
        [ClientRpc]
        public void TerminalToggleClientRpc(bool turnOn)
        {
            if (turnOn)
            {
                TimeUntilEnabled = -1f;
            }
            else
            {
                AudioPowerDown.Play();
                TimeUntilEnabled = 7f;
            }
        }


        public float DistanceToEntrance(Vector3 pos)
        {
            float bestDist = 9999f;
            var Entrances = FindObjectsOfType<EntranceTeleport>();
            foreach(var e in Entrances)
            {
                var dist = Vector3.Distance(pos, e.transform.position);
                if(dist < bestDist)
                {
                    bestDist = dist;
                }
            }
            return bestDist;
        }

        public void Update()
        {
            //AudioLRADOn.pitch = 5.53f / Plugin.LRADChargeTime.Value;
            // conditional for turning turret on and off
            if (RoundManager.Instance.IsHost) { OnOffConditional();  };

            if(AudioLRADOn.pitch != 1f) { AudioLRADOn.pitch = 1f; }  // turret is unfair if the charge is pitched

            if (On)
            {
                if (RoundManager.Instance.IsHost && !AudioLRADOn.isPlaying)
                {
                    ToggleTurretOnClientRpc(true);
                }

                AITick();
            }
            else
            {
                if(RoundManager.Instance.IsHost && AudioLRADOn.isPlaying)
                {
                    ToggleTurretOnClientRpc(false);
                }
            }

            if (RoundManager.Instance.IsHost)
            {
                if (TimeUntilDoneReloading < 0 && Reloading == true)
                {
                    Reloading = false;
                    PlayFinishReloadingClientRpc();
                    CurrentAmmo = 1;
                }
                else if (Reloading == false)
                {
                    if (CurrentAmmo <= 0)
                    {
                        Reloading = true;
                        TimeUntilDoneReloading = Plugin.LRADReloadTime.Value;
                        PlayReloadingClientRpc();
                    }
                }
            }

            TimeUntilDoneReloading -= Time.deltaTime;
            WindTime += Time.deltaTime;
            CooldownTime -= Time.deltaTime;
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

        public void OnOffConditional()
        {
            float closestDist = Plugin.LRADTargetRange.Value;
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
                if (dist > Plugin.LRADTargetRange.Value) continue;

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

            // logic for animating the turret rotating down to show its turning back on
            if (CurrentAmmo <= 0 && TimeUntilDoneReloading <= faceTransitionTime && !executingCoroutine)
            {
                Debug.Log("notifyStatusChange DOWN: currentAmmo: " + CurrentAmmo + 
                    " TimeUntilDoneReloading: " + TimeUntilDoneReloading + " faceTransitionTime: " + faceTransitionTime);

                executingCoroutine = true;
                notifyStatusChange(false);
            }

            if(CurrentAmmo <= 0 && TimeUntilDoneReloading >= (Plugin.LRADReloadTime.Value-1) && !executingCoroutine)
            {
                Debug.Log("notifyStatusChange UP: currentAmmo: " + CurrentAmmo +
                " TimeUntilDoneReloading: " + TimeUntilDoneReloading + " LRADCOUNTER: " + (Plugin.LRADReloadTime.Value - 1));
                executingCoroutine = true;
                notifyStatusChange(true);
            }


            if ((best != null && CurrentAmmo > 0) && TimeUntilEnabled <= 0f)  // actually reloaded 
            {
                if(TargetPlayer != best) { }

                SetTargetPlayerClientRpc(best.NetworkObject.NetworkObjectId);
                TurnOnClientRpc(true);
            }
            else if((AudioLRADOn.time > Plugin.LRADChargeTime.Value / 2.6) && CurrentAmmo > 0 && TimeUntilEnabled <= 0f)
            {

                // point of no return logic
                // (Turret fires even if it doesn't see a target)
                SetTargetPlayerClientRpc(9999999);
                TurnOnClientRpc(true);
            }
            else  // turned off
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
        Vector3 LastKnownPosition = Vector3.zero;
        public void AITick()
        {
            if (TargetPlayer)
            {
                facePosition(TargetPlayer.transform.position);
                LastKnownPosition = TargetPlayer.transform.position;
            }

            if(!RoundManager.Instance.IsHost) { return; }
            if(AudioLRADOn.time > Plugin.LRADChargeTime.Value && CooldownTime < 0f)
            {
                CooldownTime = 0.1f;
                Fire();
            }
        }

        public static float SoundWaveSpeed = 50f;
        public static float ycorrect = 90f;
        public static float zcorrect = 90f;
        public void Fire()
        {
            Vector3 dir;
            Vector3 targetPos = TargetPlayer ? TargetPlayer.transform.position : LastKnownPosition;

            // Horizontal direction from turret facing, vertical from target
            Vector3 horizontal = new Vector3(RotationPoint.forward.x, 0f, RotationPoint.forward.z).normalized;
            float verticalAngle = (targetPos.y - WaveSpawnPoint1.position.y) / Vector3.Distance(transform.position, targetPos);
            dir = (horizontal + Vector3.up * verticalAngle).normalized;

            // Base rotation that points Z+ toward target
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);

            // FIXED AXIS OFFSET — adjust ONCE based on prefab orientation
            // Common cases:
            //   +90 X  -> mesh was Y-forward
            //   +90 Y  -> mesh was X-forward
            Quaternion axisFix = Quaternion.Euler(0f, ycorrect, zcorrect);

            Quaternion finalRot = lookRot * axisFix;

            SpawnWave(finalRot, WaveSpawnPoint1.position);
            SpawnWave(finalRot, WaveSpawnPoint2.position);

            PlayLaunchSoundClientRpc();
            CurrentAmmo--;

            hasFiredOnce = true;
        }

        private void SpawnWave(Quaternion rot, Vector3 pos)
        {
            GameObject wave = Instantiate(
                Plugin.LRADBlastPrefab,
                pos,
                rot
            );

            var proj = wave.GetComponent<SoundProjectile>();
            proj.HostObject = this;

            // Direction is derived from rotation
            proj.SetVelocity(wave.transform.up * SoundWaveSpeed);

            wave.GetComponent<NetworkObject>().Spawn();
        }

        [ClientRpc]
        public void PlayLaunchSoundClientRpc()
        {
            AudioFire.Play();
        }

        [ClientRpc]
        public void ToggleTurretOnClientRpc(bool on)
        {
            if(on)
            {
                AudioLRADOn.Play();
                ActivationGroup.SetActive(true);
                //animator.Play("On");
            }
            else
            {
                AudioLRADOn.Stop();
                ActivationGroup.SetActive(false);
                //animator.Play("Off");
            }
        }

        bool executingCoroutine = false;

        [ClientRpc]
        public void upAnimationClientRpc()
        {
            StartCoroutine(FaceUpCoroutine());
        }

        [ClientRpc]
        public void downAnimationClientRpc()
        {
            StartCoroutine(FaceDownCoroutine());
        }


        float faceTransitionTime = 3.2f;
        public AudioSource upTransitionSound;
        public AudioSource downTransitionSound;
        bool hasFiredOnce = false;
        public IEnumerator FaceUpCoroutine()
        {
            Debug.Log("Devious Traps LRAD: Face up coroutine");
            yield return new WaitForSeconds(1);
            upTransitionSound.Play();

            float start = 0f;
            float end = -60f;

            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                float angle = Mathf.Lerp(start, end, t);

                SpeakerTransform.localRotation = Quaternion.Euler(angle, 0f, 0f);

                yield return new WaitForSeconds(faceTransitionTime / 60f);
            }
            Debug.Log("Devious Traps LRAD: Face up coroutine execution done");
            executingCoroutine = false;
            yield return new WaitForSeconds(0.1f);
        }

        public async void notifyStatusChange(bool isUp)
        {
            Debug.Log("NotifyStatusChange");
            if (isUp)
            {
                // reload time needs to be at least faceTransitionTime * 2  + 1
                if (Plugin.LRADReloadTime.Value > (faceTransitionTime * 2 + 1))
                {
                    upAnimationClientRpc();
                    //else { Debug.Log("NotifyStatusChange Reject: in notify anim"); }
                }
            }
            else
            {
                // reload time needs to be at least faceTransitionTime * 2  + 1
                if (Plugin.LRADReloadTime.Value > (faceTransitionTime * 2 + 1))
                {
                    downAnimationClientRpc();
                    //else { Debug.Log("NotifyStatusChange Reject: in notify anim"); }
                }
            }
        }

        public IEnumerator FaceDownCoroutine()
        {
            Debug.Log("Devious Traps LRAD: Face down coroutine");
            downTransitionSound.Play();

            float start = -60f;
            float end = 0;

            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                float angle = Mathf.Lerp(start, end, t);

                SpeakerTransform.localRotation = Quaternion.Euler(angle, 0f, 0f);

                yield return new WaitForSeconds(faceTransitionTime / 60f);
            }

            Debug.Log("Devious Traps LRAD: Face down coroutine execution done");
            executingCoroutine = false;
            yield return new WaitForSeconds(0.1f);
        }


        // facePosition uses easing to prevent the turret from "snapping" to players, making encounters more fair
        public void facePosition(Vector3 pos)
        {
            Vector3 directionToTarget = pos - transform.position;
            directionToTarget.y = 0f;
            if (directionToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                float EulerYTarget = Mathf.MoveTowardsAngle(
                    RotationPoint.rotation.eulerAngles.y,
                    targetRotation.eulerAngles.y,
                    Plugin.LRADRotationSpeed.Value * Time.deltaTime
                );
                RotationPoint.rotation = Quaternion.Euler(0f, EulerYTarget, 0f);
            }
        }
    }
}
