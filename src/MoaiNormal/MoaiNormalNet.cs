using GameNetcodeStuff;
using System;
using UnityEngine.AI;
using UnityEngine;
using LethalNetworkAPI;
using HarmonyLib;

namespace MoaiEnemy.src.MoaiNormal
{
    public class MoaiNormalNet
    {
        // net API definitions
        public LethalServerMessage<bool> s_moaiEnableStriker = new LethalServerMessage<bool>(identifier: "moaienablestriker");
        public LethalClientMessage<bool> c_moaiEnableStriker = new LethalClientMessage<bool>(identifier: "moaienablestriker");

        public LethalServerMessage<moaiSoundPkg> s_moaiSoundPlay = new LethalServerMessage<moaiSoundPkg>(identifier: "moaisoundplay");
        public LethalClientMessage<moaiSoundPkg> c_moaiSoundPlay = new LethalClientMessage<moaiSoundPkg>(identifier: "moaisoundplay");

        public LethalServerMessage<moaiSizePkg> s_moaiSizeSet = new LethalServerMessage<moaiSizePkg>(identifier: "moaisizeset");
        public LethalClientMessage<moaiSizePkg> c_moaiSizeSet = new LethalClientMessage<moaiSizePkg>(identifier: "moaisizeset");

        public LethalServerMessage<moaiAttachBodyPkg> s_moaiAttachBody = new LethalServerMessage<moaiAttachBodyPkg>(identifier: "moaiattachbody");
        public LethalClientMessage<moaiAttachBodyPkg> c_moaiAttachBody = new LethalClientMessage<moaiAttachBodyPkg>(identifier: "moaiattachbody");

        public LethalServerMessage<moaiDestroyBodyPkg> s_moaiDestroyBody = new LethalServerMessage<moaiDestroyBodyPkg>(identifier: "moaidestroybody");
        public LethalClientMessage<moaiDestroyBodyPkg> c_moaiDestroyBody = new LethalClientMessage<moaiDestroyBodyPkg>(identifier: "moaidestroybody");

        public LethalServerMessage<moaiHaloPkg> s_moaiHalo = new LethalServerMessage<moaiHaloPkg>(identifier: "moaisethalo");
        public LethalClientMessage<moaiHaloPkg> c_moaiHalo = new LethalClientMessage<moaiHaloPkg>(identifier: "moaisethalo");

        // linking definitions to functions
        public void setup()
        {
            Debug.Log("MOAI: NET SETUP");
            c_moaiEnableStriker.OnReceived += m_moaiEnableStriker;

            c_moaiSoundPlay.OnReceived += m_moaiSoundPlay;

            c_moaiSizeSet.OnReceived += m_moaiSizeSet;

            c_moaiAttachBody.OnReceived += m_moaiAttachBody;

            c_moaiDestroyBody.OnReceived += m_moaiDestroyBody;

            c_moaiHalo.OnReceived += m_moaiHalo;
        }

        // packages
        [Serializable]
        public struct moaiSoundPkg
        {
            public ulong netId;
            public string soundName;

            public moaiSoundPkg(ulong _netId, string _soundName)
            {
                this.netId = _netId;
                this.soundName = _soundName;
            }
        }

        [Serializable]
        public struct moaiSizePkg
        {
            public ulong netId;
            public float size;

            public float pitchAlter;

            public moaiSizePkg(ulong _netId, float _size, float _pitchAlter)
            {
                this.netId = _netId;
                this.size = _size;
                this.pitchAlter = _pitchAlter;
            }
        }

        [Serializable]
        public struct moaiAttachBodyPkg
        {
            public ulong netId;
            public ulong humanNetId;

            public moaiAttachBodyPkg(ulong _netId, ulong _humanNetId)
            {
                this.netId = _netId;
                this.humanNetId = _humanNetId;
            }
        }


        [Serializable]
        public struct moaiDestroyBodyPkg
        {
            public ulong netId;
            public ulong humanNetId;

            public moaiDestroyBodyPkg(ulong _netId, ulong _humanNetId)
            {
                this.netId = _netId;
                this.humanNetId = _humanNetId;
            }
        }

        [Serializable]
        public struct moaiHaloPkg
        {
            public ulong netId;
            public bool active;

            public moaiHaloPkg(ulong _netId, bool _active)
            {
                this.netId = _netId;
                this.active = _active;
            }
        }

        // functions
        private void m_moaiEnableStriker(bool pkg)
        {
            Debug.Log("MOAI: Enabling LightningStriker Obj.");

            GameObject weather = GameObject.Find("TimeAndWeather");

            if (weather == null)
            {
                Debug.LogError("MOAI: Not enabling LightningStriker Obj for Blue Moai: TimeAndWeather not found!");
            }

            // find "Stormy" in weather
            GameObject striker = null;
            for (int i = 0; i < weather.transform.GetChildCount(); i++)
            {
                GameObject g = weather.transform.GetChild(i).gameObject;
                if (g.name.Equals("Stormy"))
                {
                    //Debug.Log("Lethal Chaos: Found Stormy!");
                    striker = g;
                }
            }
            StormyWeather strikerObj = striker.GetComponent<StormyWeather>();
            striker.SetActive(true);
            Debug.Log("MOAI: striker successfully enabled.");
        }

        private void m_moaiSoundPlay(moaiSoundPkg moaiPkg)
        {
            // ai.NetworkObjectId synchronizes across moai
            MOAIAICORE target = null;
            //Debug.Log("MOAI: received moaisound pkg from host: " + moaiPkg.netId.ToString() + " :: " + moaiPkg.soundName);
            MOAIAICORE[] moais = GameObject.FindObjectsOfType<MOAIAICORE>();
            for (int i = 0; i < moais.Length; i++)
            {
                MOAIAICORE ai = moais[i];
                if (ai.NetworkObjectId == moaiPkg.netId)
                {
                    target = ai;
                }
            }
            if (target == null)
            {
                Debug.LogError("moaisoundplay call failed:: " + moaiPkg.netId.ToString() + " :: " + moaiPkg.soundName);
                return;
            }

            switch (moaiPkg.soundName)
            {
                case "creatureSFX":
                    target.stopAllSound();
                    target.creatureSFX.Play();
                    break;
                case "creatureVoice":
                    target.stopAllSound();

                    // start time intervals, for variance
                    double[] timeIntervals = [0.0, 0.8244, 11.564, 29.11, 34.491, 37.840, 48.689, 64.518, 89.535, 92.111];
                    int selectedTime = UnityEngine.Random.Range(0, timeIntervals.Length);

                    //Debug.Log("selected time: " + timeIntervals[selectedTime]);
                    target.creatureVoice.Play();  // time is in seconds
                    target.creatureVoice.SetScheduledStartTime(timeIntervals[selectedTime]);
                    target.creatureVoice.time = (float)timeIntervals[selectedTime];
                    break;
                case "creatureFood":
                    target.creatureSFX.Stop();
                    target.creatureVoice.Stop();
                    target.creatureFood.Play();
                    break;
                case "creatureEat":
                    target.creatureSFX.Stop();
                    target.creatureVoice.Stop();
                    target.creatureEat.Play();
                    break;
                case "creatureEatHuman":
                    target.creatureSFX.Stop();
                    target.creatureVoice.Stop();
                    target.creatureEatHuman.Play();
                    break;
                case "creatureHit":
                    target.creatureHit.Play();
                    break;
                case "creatureDeath":
                    target.stopAllSound();
                    target.creatureDeath.Play();
                    break;
                case "creatureBelch":
                    target.creatureBelch.Play();
                    break;
                case "creatureBlitz":
                    target.playSoundId("creatureBlitz");
                    break;
                case "creaturePrepare":
                    target.playSoundId("creaturePrepare");
                    break;
                case "slidingBasic":
                    target.stopSlideSounds();
                    target.isSliding = true;
                    target.slidingBasic.Play();
                    break;
                case "slidingGravel":
                    target.stopSlideSounds();
                    target.isSliding = true;
                    target.slidingGravel.Play();
                    break;
                case "slidingMetal":
                    target.stopSlideSounds();
                    target.isSliding = true;
                    target.slidingMetal.Play();
                    break;
                case "slidingSnow":
                    target.stopSlideSounds();
                    target.isSliding = true;
                    target.slidingSnow.Play();
                    break;
                case "slidingWood":
                    target.stopSlideSounds();
                    target.isSliding = true;
                    target.slidingWood.Play();
                    break;
                case "stopSliding":
                    target.stopSlideSounds();
                    target.isSliding = false;
                    break;


            }
        }

        private void m_moaiSizeSet(moaiSizePkg sizePkg)
        {
            MOAIAICORE target = null;
            Debug.Log(sizePkg);
            Debug.Log("MOAI: received moaisize pkg from host: " + sizePkg.netId.ToString() + " :: " + sizePkg.size);
            MOAIAICORE[] moais = GameObject.FindObjectsOfType<MOAIAICORE>();
            for (int i = 0; i < moais.Length; i++)
            {
                MOAIAICORE ai = moais[i];
                if (ai.NetworkObjectId == sizePkg.netId)
                {
                    target = ai;
                }
            }
            if (target == null)
            {
                Debug.LogError("moaisizeset call failed:: " + sizePkg.netId.ToString() + " :: " + sizePkg.size);
                return;
            }
            target.gameObject.transform.localScale *= (sizePkg.size * Plugin.moaiGlobalSize.Value);
            target.gameObject.GetComponent<NavMeshAgent>().height *= sizePkg.size;

            target.creatureSFX.pitch /= sizePkg.pitchAlter;
            target.creatureVoice.pitch /= sizePkg.pitchAlter;
            target.creatureFood.pitch /= sizePkg.pitchAlter;
            target.creatureEat.pitch /= sizePkg.pitchAlter;
            target.creatureEatHuman.pitch /= sizePkg.pitchAlter;
            target.creatureHit.pitch /= sizePkg.pitchAlter;
            target.creatureDeath.pitch /= sizePkg.pitchAlter;
        }

        private void m_moaiAttachBody(moaiAttachBodyPkg bodyPkg)
        {
            MOAIAICORE target = null;
            Debug.Log("MOAI: received moaiattachbody pkg from host: " + bodyPkg.netId.ToString() + " :: " + bodyPkg.humanNetId);
            MOAIAICORE[] moais = GameObject.FindObjectsOfType<MOAIAICORE>();
            for (int i = 0; i < moais.Length; i++)
            {
                MOAIAICORE ai = moais[i];
                if (ai.NetworkObjectId == bodyPkg.netId)
                {
                    target = ai;
                }
            }
            if (target == null)
            {
                Debug.LogError("moaisizeset call failed:: " + bodyPkg.netId.ToString() + " :: " + bodyPkg.humanNetId);
                return;
            }

            for (int i = 0; i < RoundManager.Instance.playersManager.allPlayerScripts.Length; i++)
            {
                PlayerControllerB player = RoundManager.Instance.playersManager.allPlayerScripts[i];

                if (player != null && player.name != null && player.transform != null)
                {
                    if (player.NetworkObject.NetworkObjectId == bodyPkg.humanNetId)
                    {
                        player.deadBody.attachedLimb = player.deadBody.bodyParts[5];

                        if(target.mouth)
                        {
                            Debug.Log("MOAI: Successfully attached body to mouth with id = " + bodyPkg.humanNetId);
                            player.deadBody.attachedTo = target.mouth.transform;
                        }
                        else
                        {
                            Debug.Log("MOAI: Successfully attached body to eye with id = " + bodyPkg.humanNetId);
                            player.deadBody.attachedTo = target.eye.transform;
                        }
                        player.deadBody.canBeGrabbedBackByPlayers = true;
                    }
                }
            }
        }

        private void m_moaiDestroyBody(moaiDestroyBodyPkg destroyPkg)
        {
            Debug.Log("MOAI: received moaidestroybody pkg from host: " + destroyPkg.netId.ToString());

            for (int i = 0; i < RoundManager.Instance.playersManager.allPlayerScripts.Length; i++)
            {
                PlayerControllerB player = RoundManager.Instance.playersManager.allPlayerScripts[i];

                if (player != null && player.name != null && player.transform != null)
                {
                    if (player.NetworkObject.NetworkObjectId == destroyPkg.humanNetId)
                    {
                        Debug.Log("MOAI: Successfully destroyed body with id = " + destroyPkg.humanNetId);
                        player.deadBody.DeactivateBody(false);
                    }
                }
            }
        }

        private void m_moaiHalo(moaiHaloPkg haloPkg)
        {
            MOAIAICORE target = null;
            Debug.Log("MOAI: received moaisethalo pkg from host: " + haloPkg.netId.ToString());
            MOAIAICORE[] moais = GameObject.FindObjectsOfType<MOAIAICORE>();
            for (int i = 0; i < moais.Length; i++)
            {
                MOAIAICORE ai = moais[i];
                if (ai.NetworkObjectId == haloPkg.netId)
                {
                    target = ai;
                }
            }
            if (target == null)
            {
                Debug.LogError("moaisethalo call failed:: " + haloPkg.netId.ToString());
                return;
            }

            target.setHalo(haloPkg.active);
        }
    }
}
