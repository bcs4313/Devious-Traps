using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using GameNetcodeStuff;

namespace DeviousTraps.src
{
    /// Outdoor indirect-fire trap. Unlike the LRAD it does NOT need line of sight --
    /// it lobs a ripple of shells over cover into a scatter circle around the target.
    ///
    /// Contract with the player:
    ///   * loud, long charge-up  -> you get told it is coming
    ///   * shells arc visibly    -> you can watch them and read where they land
    ///   * min range dead zone   -> sprinting AT the mortar is the counterplay
    ///   * lever hook            -> temporary disable (SetDisabledForSeconds)
    ///
    /// Authority model matches LRAD: host makes every decision, clients are told
    /// what to present. Rotation + audio run everywhere so the visuals stay in sync.
    public class MortarTurret : NetworkBehaviour
    {
        // ---------------------------------------------------------------- refs
        [Header("Transforms")]
        public Transform RotationPoint;      // yaw only (the carriage / gear ring)
        public Transform ElevationPoint;     // pitch only (the barrel)
        public Transform MuzzlePoint;        // shell spawn origin, at the bore

        [Header("Audio")]
        public AudioSource AudioChargeUp;    // long, loud, unmistakable tell
        public AudioSource AudioFire;        // per-shell thump
        public AudioSource AudioReload;
        public AudioSource AudioDoneReloading;
        public AudioSource AudioPowerDown;
        public AudioSource AudioServoLoop;   // plays during micro-rotations

        [Header("Visuals")]
        public GameObject ActivationGroup;   // muzzle flash / heat glow root

        // ------------------------------------------------------------- runtime
        public PlayerControllerB TargetPlayer;

        public static System.Random rnd = new System.Random();

        private bool Reloading = false;
        private int CurrentAmmo = 0;
        private float TimeUntilDoneReloading = 0f;
        private float TimeUntilEnabled = -1f;   // lever / terminal disable
        private bool FireMissionActive = false;

        // aim state -- driven by the fire mission, read by LateUpdate
        private Vector3 CurrentAimPoint = Vector3.zero;
        private bool HasAimPoint = false;

        // host only: this salvo's scatter offsets, kept so re-aiming preserves the
        // spread instead of collapsing every shell onto the player
        private Vector3[] SalvoOffsets;

        private float ChargeTimer = 0f;

        // =====================================================================
        //  LIFECYCLE
        // =====================================================================
        public void Start()
        {
            TimeUntilDoneReloading = MortarConfig.MortarReloadTime.Value;
            if (ActivationGroup) ActivationGroup.SetActive(false);

            float v = MortarConfig.MortarVolume.Value;
            foreach (var a in new[] { AudioChargeUp, AudioFire, AudioReload,
                                      AudioDoneReloading, AudioPowerDown, AudioServoLoop })
                if (a) a.volume = v;

            if (RoundManager.Instance.IsHost)
                ProjectToRandomOutsideLocation();
        }

        // Same outside-node placement approach as the LRAD, but biased AWAY from
        // the entrance. A mortar wants standoff distance, not doorstep camping.
        public static int PlacementSamples = 8;
        public void ProjectToRandomOutsideLocation()
        {
            Vector3 best = transform.position;
            float bestScore = -99999f;

            var nodes = RoundManager.Instance.outsideAINodes;
            for (int i = 0; i < PlacementSamples; i++)
            {
                var node = nodes[rnd.Next(0, nodes.Length)];
                var jitter = node.transform.position
                             + new Vector3(rnd.Next(-6, 6), 0f, rnd.Next(-6, 6));

                Vector3 pos = NavMesh.SamplePosition(jitter, out var hit, 20f, NavMesh.AllAreas)
                              ? hit.position
                              : node.transform.position;

                // want: far enough from the entrance to be a standoff threat,
                // but not so far it can never reach anyone.
                float d = DistanceToEntrance(pos);
                float score = -Mathf.Abs(d - MortarConfig.MortarPreferredStandoff.Value);

                // reject spots without sky above. this trap needs to arc to the sky
                if (!HasSkyClearance(pos)) score -= 1000f;

                if (score > bestScore) { bestScore = score; best = pos; }
            }

            ForwardPositionClientRpc(best);
        }

        private bool HasSkyClearance(Vector3 pos)
        {
            return !Physics.Raycast(pos + Vector3.up * 2f, Vector3.up,
                                    MortarConfig.MortarRequiredCeiling.Value,
                                    StartOfRound.Instance.collidersAndRoomMask,
                                    QueryTriggerInteraction.Ignore);
        }

        [ClientRpc]
        public void ForwardPositionClientRpc(Vector3 pos) => transform.position = pos;

        public float DistanceToEntrance(Vector3 pos)
        {
            float best = 9999f;
            foreach (var e in FindObjectsOfType<EntranceTeleport>())
                best = Mathf.Min(best, Vector3.Distance(pos, e.transform.position));
            return best;
        }

        //  LEVER / TERMINAL DISABLE (not hooked yet lol)
        public void SetDisabledForSeconds(float seconds)
        {
            if (RoundManager.Instance.IsHost) DisableClientRpc(seconds);
            else DisableServerRpc(seconds);
        }

        [ServerRpc(RequireOwnership = false)]
        public void DisableServerRpc(float seconds) => DisableClientRpc(seconds);

        [ClientRpc]
        public void DisableClientRpc(float seconds)
        {
            TimeUntilEnabled = seconds;
            if (AudioPowerDown) AudioPowerDown.Play();
            if (AudioChargeUp) AudioChargeUp.Stop();
            if (ActivationGroup) ActivationGroup.SetActive(false);
            ChargeTimer = 0f;
        }

        //  MAIN LOOP
        public void Update()
        {
            TimeUntilDoneReloading -= Time.deltaTime;
            TimeUntilEnabled -= Time.deltaTime;

            if (RoundManager.Instance.IsHost)
            {
                HandleReload();
                if (!FireMissionActive) HostAcquireAndCharge();
            }

            DriveRotation();
        }

        private void HandleReload()
        {
            if (Reloading && TimeUntilDoneReloading < 0f)
            {
                Reloading = false;
                CurrentAmmo = 1;
                PlayFinishReloadingClientRpc();
            }
            else if (!Reloading && CurrentAmmo <= 0)
            {
                Reloading = true;
                TimeUntilDoneReloading = MortarConfig.MortarReloadTime.Value;
                PlayReloadingClientRpc();
            }
        }

        [ClientRpc] public void PlayReloadingClientRpc() 
        {
            Reloading = true;
            if (AudioReload) AudioReload.Play(); 
        }
        [ClientRpc]
        public void PlayFinishReloadingClientRpc()
        {
            if (AudioDoneReloading) AudioDoneReloading.Play();
            if (AudioReload) AudioReload.Stop();
            Reloading = false;
        }

        /// <summary>
        /// Host-only. Picks a victim, charges up, then commits a fire mission.
        /// NO line-of-sight test on purpose -- cover does not save you here.
        /// </summary>
        private void HostAcquireAndCharge()
        {
            PlayerControllerB best = FindTarget();

            bool canFire = best != null && CurrentAmmo > 0 && TimeUntilEnabled <= 0f;

            if (!canFire)
            {
                if (ChargeTimer > 0f) SetChargingClientRpc(false);
                ChargeTimer = 0f;
                return;
            }

            if (ChargeTimer <= 0f) SetChargingClientRpc(true);
            ChargeTimer += Time.deltaTime;

            // track the victim while spinning up so the barrel visibly follows
            SetTargetPlayerClientRpc(best.NetworkObject.NetworkObjectId);

            if (ChargeTimer >= MortarConfig.MortarChargeTime.Value)
            {
                ChargeTimer = 0f;
                CurrentAmmo--;
                BeginFireMission(best);
            }
        }

        private PlayerControllerB FindTarget()
        {
            PlayerControllerB best = null;
            float bestDist = float.MaxValue;

            foreach (var ply in RoundManager.Instance.playersManager.allPlayerScripts)
            {
                if (ply == null || ply.isPlayerDead || !ply.isPlayerControlled && !ply.isInHangarShipRoom) continue;

                // outdoor trap: ignore anyone inside the facility
                if (ply.isInsideFactory) continue;

                float dist = Vector3.Distance(transform.position, ply.transform.position);

                // THE DEAD ZONE. Too close = under the arc = safe.
                if (dist < MortarConfig.MortarMinRange.Value) continue;
                if (dist > MortarConfig.MortarMaxRange.Value) continue;

                if (dist < bestDist) { bestDist = dist; best = ply; }
            }
            return best;
        }

        [ClientRpc]
        public void SetTargetPlayerClientRpc(ulong netid)
        {
            TargetPlayer = null;
            if (netid == ulong.MaxValue) return;
            foreach (var ply in RoundManager.Instance.playersManager.allPlayerScripts)
                if (ply != null && ply.NetworkObject.NetworkObjectId == netid)
                    TargetPlayer = ply;
        }

        [ClientRpc]
        public void SetChargingClientRpc(bool charging)
        {
            if (charging)
            {
                if (AudioChargeUp && !AudioChargeUp.isPlaying) AudioChargeUp.Play();
                if (ActivationGroup) ActivationGroup.SetActive(true);
            }
            else
            {
                if (AudioChargeUp) AudioChargeUp.Stop();
                if (ActivationGroup) ActivationGroup.SetActive(false);
            }
        }

        //  FIRE MISSION  --  the actual mortar behaviour
        /// Host builds the whole salvo up front: N impact points scattered in a
        /// circle around the target, then broadcasts them. Every client walks the
        /// same list so rotation and audio match; only the host spawns shells.
        private void BeginFireMission(PlayerControllerB victim)
        {
            int count = rnd.Next(MortarConfig.MortarMinShells.Value,
                                 MortarConfig.MortarMaxShells.Value + 1);

            Vector3 center = victim.transform.position;

            // slight lead so standing still is punished but running is rewarded
            center += victim.thisController.velocity * MortarConfig.MortarLeadFactor.Value;

            var impacts = new Vector3[count];
            SalvoOffsets = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 c = UnityEngine.Random.insideUnitCircle * MortarConfig.MortarScatterRadius.Value;
                SalvoOffsets[i] = new Vector3(c.x, 0f, c.y);
                Vector3 p = center + SalvoOffsets[i];
                impacts[i] = GroundAt(p, center.y);
            }

            FireMissionClientRpc(impacts);
        }

        /// Blend this shell's original aim point toward where the victim is right
        /// now. 0 = the old locked salvo, 1 = full chase. The shell keeps its own
        /// scatter offset so the spread pattern survives the re-aim.
        private Vector3 TrackedImpact(Vector3 original, int i)
        {
            float k = 1;  // we will keep k as 1 for now... for tracking
            if (k <= 0f || TargetPlayer == null || TargetPlayer.isPlayerDead) return original;

            Vector3 live = TargetPlayer.transform.position
                         + TargetPlayer.thisController.velocity * MortarConfig.MortarLeadFactor.Value;

            if (SalvoOffsets != null && i < SalvoOffsets.Length) live += SalvoOffsets[i];

            return GroundAt(Vector3.Lerp(original, live, k), live.y);
        }

        /// Host re-aimed mid-salvo; everyone swings the barrel to match.
        /// Presentation only -- the host still owns the real impact point.
        [ClientRpc]
        public void UpdateAimClientRpc(Vector3 point)
        {
            CurrentAimPoint = point;
            HasAimPoint = true;
        }

        /// <summary>Drop the impact point onto terrain so shells don't detonate midair.</summary>
        private Vector3 GroundAt(Vector3 p, float fallbackY)
        {
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 120f,
                                StartOfRound.Instance.collidersAndRoomMask,
                                QueryTriggerInteraction.Ignore))
                return hit.point;

            p.y = fallbackY;
            return p;
        }

        [ClientRpc]
        public void FireMissionClientRpc(Vector3[] impacts)
        {
            StartCoroutine(FireMissionRoutine(impacts));
        }

        private IEnumerator FireMissionRoutine(Vector3[] impacts)
        {
            FireMissionActive = true;
            if (AudioServoLoop) AudioServoLoop.Play();

            float interval = MortarConfig.MortarShellInterval.Value;

            for (int i = 0; i < impacts.Length; i++)
            {
                // micro-rotation: swing onto THIS shell's impact point.
                // Ease over the interval so the barrel visibly walks the salvo.
                CurrentAimPoint = impacts[i];
                HasAimPoint = true;

                // Host re-aims this shell at where the victim is NOW, then tells
                // everyone, so the barrel walks toward the tracked point during the
                // interval instead of easing onto a stale one.
                if (RoundManager.Instance.IsHost)
                {
                    impacts[i] = TrackedImpact(impacts[i], i);
                    UpdateAimClientRpc(impacts[i]);
                }

                yield return new WaitForSeconds(interval);

                if (RoundManager.Instance.IsHost)
                    LaunchShell(impacts[i]);

                if (AudioFire) AudioFire.Play();
            }

            if (AudioServoLoop) AudioServoLoop.Stop();
            HasAimPoint = false;
            FireMissionActive = false;

            if (RoundManager.Instance.IsHost)
                SetChargingClientRpc(false);
        }

        /// Fixed-flight-time solve. Given where we are and where we want to land,
        /// return the launch velocity. Always has a solution (unlike solving for
        /// angle, which fails past max range) and makes the airtime -- the thing
        /// the player is reading -- predictable and tunable.
        ///
        ///   p(t) = p0 + v*t + 0.5*a*t^2   =>   v = (dp - 0.5*a*t^2) / t
        public static Vector3 SolveBallisticVelocity(Vector3 origin, Vector3 target, float flightTime)
        {
            Vector3 dp = target - origin;
            Vector3 a = Physics.gravity * MortarConfig.MortarGravityScale.Value;
            return (dp - 0.5f * a * flightTime * flightTime) / flightTime;
        }

        public static float FlightTimeFor(float horizontalDist)
        {
            return Mathf.Clamp(horizontalDist / MortarConfig.MortarShellSpeed.Value,
                               MortarConfig.MortarMinFlightTime.Value,
                               MortarConfig.MortarMaxFlightTime.Value);
        }

        private void LaunchShell(Vector3 impact)
        {
            Vector3 origin = MuzzlePoint ? MuzzlePoint.position : transform.position + Vector3.up * 2f;

            float flat = Vector3.Distance(new Vector3(origin.x, 0, origin.z),
                                          new Vector3(impact.x, 0, impact.z));
            float t = FlightTimeFor(flat);

            Vector3 vel = SolveBallisticVelocity(origin, impact, t);

            GameObject go = Instantiate(Plugin.MortarShellPrefab, origin,
                                        Quaternion.LookRotation(vel.normalized));

            var shell = go.GetComponent<MortarShell>();
            shell.HostTurret = this;

            go.GetComponent<NetworkObject>().Spawn();

            // tell every client the exact launch state so the arc matches everywhere
            shell.InitializeClientRpc(origin, vel, impact);
        }

        //  ROTATION  --  yaw on the carriage, pitch on the barrel
        private void DriveRotation()
        {
            Vector3 aim;

            if (HasAimPoint) aim = CurrentAimPoint;
            else if (TargetPlayer) aim = TargetPlayer.transform.position;
            else return;

            // ---- yaw
            if (FireMissionActive || !Reloading)
            {
                Vector3 flat = aim - RotationPoint.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.001f && RotationPoint)
                {
                    float want = Quaternion.LookRotation(flat).eulerAngles.y;
                    float now = Mathf.MoveTowardsAngle(RotationPoint.rotation.eulerAngles.y, want,
                                                       MortarConfig.MortarRotationSpeed.Value * Time.deltaTime);
                    RotationPoint.rotation = Quaternion.Euler(0f, now, 0f);
                }
            }

            // ---- pitch, derived from the ballistic solution so the barrel
            //      actually points along the arc it is about to throw
            if (ElevationPoint && (FireMissionActive || !Reloading))
            {
                Vector3 origin = MuzzlePoint ? MuzzlePoint.position : transform.position;
                float dist = Vector3.Distance(new Vector3(origin.x, 0, origin.z),
                                              new Vector3(aim.x, 0, aim.z));
                Vector3 v = SolveBallisticVelocity(origin, aim, FlightTimeFor(dist));

                float pitch = -Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -MortarConfig.MortarMaxElevation.Value,
                                            -MortarConfig.MortarMinElevation.Value);

                float cur = ElevationPoint.localEulerAngles.x;
                float next = Mathf.MoveTowardsAngle(cur, pitch,
                                                    MortarConfig.MortarElevationSpeed.Value * Time.deltaTime);
                ElevationPoint.localRotation = Quaternion.Euler(next, 0f, 0f);
            }
            else  // reset to minimum pitch if not in a fire mission
            {
                float pitch = 0;

                float cur = ElevationPoint.localEulerAngles.x;
                float next = Mathf.MoveTowardsAngle(cur, pitch,
                                                    MortarConfig.MortarElevationSpeed.Value * Time.deltaTime);
                ElevationPoint.localRotation = Quaternion.Euler(next, 0f, 0f);
            }
        }
    }
}