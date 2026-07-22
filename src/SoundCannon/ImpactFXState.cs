using Unity.Netcode;

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;
using UnityEngine;
using GameNetcodeStuff;
using System.Collections;
using System.Security.Cryptography;
using UnityEngine.PlayerLoop;

namespace DeviousTraps.src.SoundCannon
{
    public class ImpactFXState : NetworkBehaviour
    {
        // 4 visual effects that are UI based, each with an animator
        public UnityEngine.UI.Image InitialHitEffect;
        //public Animator InitialHitAnimator;
        public UnityEngine.UI.Image PainAuraEffect;
        public Animator PainAuraAnimator;
        public UnityEngine.UI.Image StaticEffect;
        public Animator StaticEffectAnimator;
        public UnityEngine.UI.Image GlitchEffect;
        public Animator GlitchAnimator;

        // 1 = max power, 0 = state is done, destroy self
        public float Power = 1f;
        public float PowerStart = -1;
        float TimeToEnd = 20f;
        public float max_TimeToEnd = 20f;

        public System.Random rnd = new System.Random();

        // Audio
        public AudioSource SoundImpact;
        public AudioSource SoundGlitch;
        public AudioSource SoundNoise;

        public static List<ImpactFXState> Instances;

        public DeafnessListenerFilter filter;

        // state representation
        // 180-120f full impairment, full deafness, drunkness, walking instability
        // 120f and down, fading impairment, waning deafness, low walk instability to none
        // indirect hits from far away might have weaker debuffs maybe?

        private ulong pid = 999999999;

        public void Start()
        {
            if (SoundImpact) { SoundImpact.volume = Plugin.LRADFXVolume.Value * Power; }
            if (SoundNoise) { SoundNoise.volume = Plugin.LRADFXVolume.Value * Power; }
            //TimeToEnd = Plugin.LRADDisorientPeriod.Value;
            max_TimeToEnd = Plugin.LRADDisorientPeriod.Value;
        }

        // begin the madness
        public void AttachToPlayer(ulong uid, float PStart)
        {
            if (Instances == null) { Instances = new List<ImpactFXState>(); }
            pid = uid;  
            Debug.Log("Devious Traps: LRAD Impact State attached to the player with uid: " + uid + " Power Start is: " + PStart + " TimeToEnd: " + TimeToEnd);

            PowerStart = PStart;

            Instances.Add(this);
        }

        public void AStep()
        {
            Debug.Log("Devious Traps: LRAD Astep--");
            if (Instances == null) { Instances = new List<ImpactFXState>(); }

            Power = PowerStart;
            if (PowerStart > 0.15)
            {
                TimeToEnd = Plugin.LRADDisorientPeriod.Value / (1 / PowerStart);
            }
            else
            {
                TimeToEnd = 0f;
            }
            if (SoundImpact) { SoundImpact.volume = Plugin.LRADFXVolume.Value * PowerStart; }
            if (SoundNoise) { SoundNoise.volume = Plugin.LRADFXVolume.Value * PowerStart; }

            DestroyStep();

            Instances.Add(this);
        }

        public void DestroyStep()
        {
            GameObject objToDestroy = null;

            if(Instances == null) { Instances = new List<ImpactFXState>(); }
            if(!RoundManager.Instance.IsHost) { return; }

            // destroy an fx state if it already has the pid, using this one instead
            foreach (ImpactFXState fx in Instances)
            {
                if (fx && fx.gameObject && fx.pid == this.pid && fx != this)
                {
                    objToDestroy = fx.gameObject;
                    Debug.Log("Devious Traps: Detached LRAD Impact State to replace with current instance");
                }
            }

            if (objToDestroy) { Destroy(objToDestroy); }
        }

        bool spawned = false;
        public override void OnNetworkSpawn()
        {
            if (pid != 999999999 && RoundManager.Instance.IsHost)
            {
                NotifyUsersOfEffectClientRpc(pid, PowerStart);
            }
        }

        [ClientRpc]
        public void NotifyUsersOfEffectClientRpc(ulong NewPid, float pStart)
        {
            //Debug.Log("NotifyUsersOfEffectClientRpc: -> " + pStart);
            pid = NewPid;
            PowerStart = pStart;
            spawned = true;
            PlayInitialHit(pid);
        }


        public static float GlitchChancePerStep = 0.05f;  // 5% chance per step
        public static float GlitchCheckDelay = 0.25f;  // check 4 times a second
        public static float max_GlitchCheckDelay = 0.25f; // check 4 times a second
        public static float TotalDeafness = 300;

        public static float DrunkPushChancePerStepBase = 1f;  // 25% chance per step
        public static float DrunkPushMaxForce = 3f;  // check 4 times a second
        bool isInitialized = false;


        [ServerRpc (RequireOwnership = false)]
        public void RequestPowerInitServerRpc()
        {
            //Debug.Log("RequestPowerInitServerRPC CALL: ");
            NotifyUsersOfEffectClientRpc(pid, PowerStart);
        }

        float requestInitCooldown = 0.05f;
        public void Update()
        {
            // backup spawn block for laggy players
            if(!spawned) 
            {
                if(NetworkObject.IsSpawned && requestInitCooldown <= 0)
                {
                    RequestPowerInitServerRpc();
                    requestInitCooldown = 0.05f;
                }
                requestInitCooldown -= Time.deltaTime;
                return; 
            }

            /*
            if (PowerStart == -1)
            {
                Debug.LogError("Impact FX LRAD Error: Power Start uninitialized. waiting for initialization");
                return;
            }
            */

            if (Power > PowerStart) { Power = PowerStart-0.01f; } 

            if (!LocalCheck(pid)) { return; }  // must be local player for effect
            // below here is local player only


            TimeToEnd -= Time.deltaTime;
            GlitchCheckDelay -= Time.deltaTime;
            Power = TimeToEnd / max_TimeToEnd;

            if (!SoundGlitch.isPlaying) 
            {
                SoundGlitch.Play();
                SoundGlitch.volume = 0;
            }

            // for now static is a simple fade q
            StaticEffect.color = new Color(1f, 1f, 1f, (Power/2) * Plugin.LRADFXMult.Value);

            var ply = RoundManager.Instance.playersManager.localPlayerController;
            ply.drunkness = Plugin.LRADDrunknessMult.Value * Power;
            if (filter)
            {
                filter.Deafness = Math.Clamp(Power * 3, 0, 1);
            }

            if (ply.isPlayerDead) { Destroy(this.gameObject); }

            if (Power < 0 && LocalCheck(pid)) { Destroy(gameObject); }

            if (GlitchCheckDelay < 0)
            {
                DestroyStep();
                if (rnd.NextDouble() < GlitchChancePerStep)
                {
                    // we shouldn't need a client rpc for glitches, commented out code is there just in case
                    StartCoroutine(PlayGlitchCoroutine());
                }

                if (rnd.NextDouble() < (DrunkPushChancePerStepBase * Power))
                {
                    var x = (float)(rnd.NextDouble() * DrunkPushMaxForce * Math.Sign(rnd.NextDouble() - 0.5));
                    var y = (float)(rnd.NextDouble() * DrunkPushMaxForce * Math.Sign(rnd.NextDouble() - 0.5));
                    var z = (float)(rnd.NextDouble() * DrunkPushMaxForce * Math.Sign(rnd.NextDouble() - 0.5));

                    // this SHOULD work, if it doesn't we have commented out code for server level pushing
                    ply.externalForceAutoFade = new Vector3(x, y, z) + ply.externalForceAutoFade * Plugin.LRADDizzyMult.Value;
                }

                GlitchCheckDelay = max_GlitchCheckDelay;
            }
        }

        public PlayerControllerB GetPlayer(ulong netid)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var player in players)
            { 
                if(player.NetworkObjectId == netid)
                {
                    return player;
                }
            }
            return null;
        }

        public bool LocalCheck(ulong uid)
        {
            if(RoundManager.Instance.playersManager.localPlayerController.NetworkObjectId == uid)
            {
                return true;
            }
            return false;
        }

        public void PlayInitialHit(ulong uid)
        {
            AStep();
            if (!LocalCheck(uid)) { return; }  // must be local player for effect
            StartCoroutine(PlayInitialHitCoroutine());
            spawned = true;
        }

        public static float DrunkInitial = 1.3f;
        // duration: 0.5s
        public IEnumerator PlayInitialHitCoroutine()
        {
            Debug.Log("IMPACTFXHIT COR: " + Plugin.LRADDisorientPeriod.Value + ": wall penalty multiplier = " + PowerStart);
            if (SoundImpact) { SoundImpact.volume = Plugin.LRADFXVolume.Value * Power; }
            if (SoundNoise) { SoundNoise.volume = Plugin.LRADFXVolume.Value * Power; }
            TimeToEnd = Plugin.LRADDisorientPeriod.Value * PowerStart;
            max_TimeToEnd = Plugin.LRADDisorientPeriod.Value;
            if (!SoundImpact.isPlaying) { SoundImpact.Play(); }

            var ply = RoundManager.Instance.playersManager.localPlayerController;
            try
            {
                ply.drunkness = DrunkInitial * Plugin.LRADDrunknessMult.Value;

                // add the deafness filter
                AudioListener listener =
                    ply.gameplayCamera.GetComponentInChildren<AudioListener>();

                // add a filter to control if it doesn't exist
                if (!listener.TryGetComponent<DeafnessListenerFilter>(out _))
                {
                    filter = listener.gameObject.AddComponent<DeafnessListenerFilter>();
                }
                else
                {
                    DeafnessListenerFilter prev;
                    listener.TryGetComponent<DeafnessListenerFilter>(out prev);
                    filter = prev;
                }
            }
            catch (Exception e) { Debug.LogError(e); }
            for (float i = 1f; i > 0f; i -= 0.05f)
            {
                InitialHitEffect.color = new Color(1f, 1f, 1f, i * Plugin.LRADFXMult.Value);
                yield return new WaitForSeconds(0.025f);
            }

            InitialHitEffect.color = new Color(1f, 1f, 1f, 0);
        }

        public void OnDestroy()
        {
            try
            {
                if (!LocalCheck(pid)) { return; }  // must be local player for effect
                if (filter) { filter.Deafness = 0; }
                var ply = RoundManager.Instance.playersManager.localPlayerController;
                ply.drunkness = 0;

                AudioListener listener = ply.gameplayCamera.GetComponentInChildren<AudioListener>();

                // stop existing filter deafness if applicable
                if (listener.TryGetComponent<DeafnessListenerFilter>(out _))
                {
                    listener.gameObject.GetComponent<DeafnessListenerFilter>().Deafness = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        /*
        [ServerRpc]
        public void PlayDrunkStepServerRpc(ulong uid, float x, float y, float z)
        {
            PlayDrunkStepClientRpc(uid, x, y, z);
        }

        [ClientRpc]
        public void PlayDrunkStepClientRpc(ulong uid, float x, float y, float z)
        {
            if (!LocalCheck(uid)) { return; }  // must be local player for effect
            var ply = RoundManager.Instance.playersManager.localPlayerController;
            ply.externalForceAutoFade = new Vector3(x, y, z) + ply.externalForceAutoFade;
        }
        */

        /*
        [ServerRpc]
        public void PlayGlitchServerRpc(ulong uid)
        {
            PlayGlitchClientRpc(uid);
        }

        [ClientRpc]
        public void PlayGlitchClientRpc(ulong uid)
        {
            if (!LocalCheck(uid)) { return; }  // must be local player for effect
            StartCoroutine(PlayGlitchCoroutine());
        }
        */


        public static float GlitchDurationModifier = 0.005f;
        public static float AvgGlitchStrengthModifier = 2.5f;
        public static float AvgGlitchVolumeModifier = 1f;
        // duration: 0.5s
        public IEnumerator PlayGlitchCoroutine()
        {
            float GlitchStartOpacity = ((float)(rnd.NextDouble() * 0.05f + 0.05) * Power) * AvgGlitchStrengthModifier;
            float GlitchStartPitch = ((float)(rnd.NextDouble() * 2f));
            float GlitchVolume = Math.Min(GlitchStartOpacity / 0.1f * AvgGlitchVolumeModifier, 1f);

            if (SoundGlitch)
            {
                SoundGlitch.volume = GlitchVolume * Plugin.LRADFXVolume.Value;
                SoundGlitch.pitch = GlitchStartPitch;
            }

            for (float i = GlitchStartOpacity; i > 0f; i -= GlitchDurationModifier)
            {
                GlitchEffect.color = new Color(1f, 1f, 1f, i  * Plugin.LRADFXMult.Value);

                yield return new WaitForSeconds(0.025f);
            }

            GlitchEffect.color = new Color(1f, 1f, 1f, 0);
            SoundGlitch.volume = 0f;
        }
    }
}
