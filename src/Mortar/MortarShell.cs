using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace DeviousTraps.src
{
    /// One shell in flight. Spawned by the host only, but every client runs the
    /// same integration locally so the arc looks identical everywhere without a
    /// NetworkTransform streaming 60 position updates a second.
    ///
    /// Authority split:
    ///   * clients  -> simulate position, spin, trail, whistle  (visuals only)
    ///   * host     -> decides WHEN it hits, sends ExplodeClientRpc(pos)
    ///   * everyone -> runs Landmine.SpawnExplosion locally on that RPC
    ///
    /// That last bit is vanilla LC behaviour: SpawnExplosion damages
    /// GameNetworkManager.Instance.localPlayerController if it is in range, so
    /// each client applying it once produces exactly one set of damage per
    /// player. Do NOT also apply damage host-side you'd double-dip.
    public class MortarShell : NetworkBehaviour
    {
        [Header("Refs")]
        public Transform ShellModel;         // spun for tumble; optional
        public AudioSource AudioWhistle;     // the descent tell
        public GameObject TrailRoot;         // particle trail; optional

        [Header("Tuning")]
        public float TumbleSpeed = 220f;
        public float WhistleStartHeight = 25f;   // metres above impact point
        public float MaxLifetime = 20f;          // hard failsafe

        // runtime only
        [HideInInspector] public MortarTurret HostTurret;

        private Vector3 Origin;
        private Vector3 Velocity;
        private Vector3 Impact;

        private float Elapsed = 0f;
        private bool Live = false;
        private bool Detonated = false;
        private bool WhistleStarted = false;

        private Vector3 LastPos;

        /// Called by the turret immediately after Spawn(). Gives every client the
        /// exact launch state so the local integration matches the host's.
        [ClientRpc]
        public void InitializeClientRpc(Vector3 origin, Vector3 velocity, Vector3 impact)
        {
            Origin = origin;
            Velocity = velocity;
            Impact = impact;

            transform.position = origin;
            LastPos = origin;

            Elapsed = 0f;
            Detonated = false;
            WhistleStarted = false;
            Live = true;

            if (TrailRoot) TrailRoot.SetActive(true);
            if (AudioWhistle)
            {
                AudioWhistle.volume = MortarConfig.MortarVolume.Value;
                AudioWhistle.Stop();
            }
        }

        // =====================================================================
        //  FLIGHT
        // =====================================================================
        private void Update()
        {
            if (!Live) return;

            Elapsed += Time.deltaTime;

            // Closed-form position instead of accumulating velocity. Same result
            // on every machine regardless of framerate -- accumulation would
            // drift between a 30fps client and a 200fps host.
            Vector3 a = Physics.gravity * MortarConfig.MortarGravityScale.Value;
            Vector3 pos = Origin + Velocity * Elapsed + 0.5f * a * Elapsed * Elapsed;

            transform.position = pos;

            // point the nose along travel
            Vector3 vel = Velocity + a * Elapsed;
            if (vel.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(vel.normalized);

            if (ShellModel)
                ShellModel.Rotate(Vector3.forward, TumbleSpeed * Time.deltaTime, Space.Self);

            HandleWhistle(pos, vel);

            // ---- host alone decides impact
            if (Detonated) return;

            if (RoundManager.Instance.IsHost)
            {
                if (Elapsed > MaxLifetime) { HostDetonate(pos); return; }

                // Sweep the segment we covered this frame rather than testing a
                // point. At mortar terminal velocity a point test tunnels straight
                // through terrain between frames.
                Vector3 delta = pos - LastPos;
                float dist = delta.magnitude;

                if (dist > 0.0001f &&
                    Physics.Raycast(LastPos, delta / dist, out var hit, dist,
                                    StartOfRound.Instance.collidersAndRoomMask,
                                    QueryTriggerInteraction.Ignore))
                {
                    HostDetonate(hit.point);
                    return;
                }

                // Fallback: we passed the planned impact plane without touching
                // anything (thin geometry, moving platform, bad raycast mask).
                if (Elapsed >= 0.05f && pos.y <= Impact.y && vel.y < 0f)
                    HostDetonate(Impact);
            }

            LastPos = pos;
        }

        private void HandleWhistle(Vector3 pos, Vector3 vel)
        {
            if (WhistleStarted || AudioWhistle == null) return;
            if (vel.y >= 0f) return;                       // only on the way down

            if (pos.y - Impact.y <= WhistleStartHeight)
            {
                WhistleStarted = true;
                AudioWhistle.Play();
            }
        }

        // =====================================================================
        //  DETONATION
        // =====================================================================
        private void HostDetonate(Vector3 pos)
        {
            if (Detonated) return;
            Detonated = true;
            Live = false;

            ExplodeClientRpc(pos);

            // Give the RPC and the local VFX a beat before the object leaves the
            // network. Despawning in the same frame can eat the explosion on
            // high-latency clients.
            StartCoroutine(DespawnAfter(1.5f));
        }

        [ClientRpc]
        public void ExplodeClientRpc(Vector3 pos)
        {
            Live = false;
            Detonated = true;

            if (AudioWhistle) AudioWhistle.Stop();
            if (TrailRoot) TrailRoot.SetActive(false);
            if (ShellModel) ShellModel.gameObject.SetActive(false);

            // Vanilla explosion. Damages the LOCAL player if in range, so running
            // it on every client yields exactly one hit per player.
            Landmine.SpawnExplosion(
                pos + Vector3.up * 0.2f,
                spawnExplosionEffect: true,
                killRange: MortarConfig.MortarKillRange.Value,
                damageRange: MortarConfig.MortarDamageRange.Value,
                nonLethalDamage: MortarConfig.MortarDamage.Value,
                physicsForce: MortarConfig.MortarPhysicsForce.Value
            );

            // The bang is a locator too -- it tells you where the salvo is walking.
            if (RoundManager.Instance != null)
                RoundManager.Instance.PlayAudibleNoise(pos, 32f, 0.9f, 0, false, 0);
        }

        private IEnumerator DespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn();
            else Destroy(gameObject);
        }
    }
}