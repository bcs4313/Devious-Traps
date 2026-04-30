using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.UIElements.StylePropertyAnimationSystem;

namespace DeviousTraps.src.MouseTrap
{
    public class ItemTriggerBox : NetworkBehaviour
    {
        public GrabbableObject attachedObject;
        public Vector3 fixedPos;
        public Quaternion fixedRot;

        public void LinkItemToTrigger(GrabbableObject obj)
        {
            attachedObject = obj;
            obj.grabbable = false;
        }

        public void Start()
        {
            try
            {
                var box = GetComponent<BoxCollider>();
                Vector3 newSize = box.size * Plugin.MTrapScrapBaitForgiveness.Value;
                box.set_size_Injected(ref newSize);
            }
            catch (Exception e) { Debug.LogError(e); }
        }

        // detach mechanism
        bool itemInitialized = false;
        float refreshItemInitCooldown = 1000; // refresh attach every 10 seconds, 1 second for first init
        public void Update()
        {
            if(!RoundManager.Instance.IsHost) { return; }
            if(attachedObject && attachedObject.playerHeldBy != null)
            {
                attachedObject.grabbable = true;
                attachedObject = null;
            }

            if((!itemInitialized || refreshItemInitCooldown < 0) && attachedObject && attachedObject.GetComponent<NetworkObject>().IsSpawned)
            {
                itemInitialized = true;
                AttachObjectClientRpc(attachedObject.NetworkObjectId);
                refreshItemInitCooldown = 10000f;
            }
            refreshItemInitCooldown -= Time.deltaTime;
        }

        public void OnTriggerEnter(Collider other)
        {

            if (gameObject.layer != LayerMask.NameToLayer("Triggers"))
            {
                return;
            }

            //Debug.Log("Mouse Trap Trigger Hit: " + other.gameObject);

            var go = other.gameObject;
            PlayerControllerB ply = playerParentWalk(go);

            var cont = RoundManager.Instance.playersManager;
            if (ply)
            {
                if (attachedObject) 
                {
                    attachedObject.grabbable = true;
                }
            }

        }

        [ClientRpc]
        public void AttachObjectClientRpc(ulong uid)
        {
            var objs = FindObjectsOfType<GrabbableObject>();
            foreach (var obj in objs)
            {
                if (uid == obj.NetworkObjectId)
                {
                    attachedObject = obj;
                    obj.grabbable = false;
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.parent = transform.root.GetComponent<MouseTrap>().ScrapAttachment;
                    obj.startFallingPosition = Vector3.zero;
                    obj.targetFloorPosition = Vector3.zero;
                }
            }
        }

        public void OnTriggerExit(Collider other)
        {

            if (gameObject.layer != LayerMask.NameToLayer("Triggers"))
            {
                return;
            }

            //Debug.Log("Mouse Trap Trigger Cancel: " + other.gameObject);

            var go = other.gameObject;
            PlayerControllerB ply = playerParentWalk(go);

            var cont = RoundManager.Instance.playersManager;
            if (ply)
            {
                attachedObject.grabbable = false;
            }
        }


        // goes up the parent tree until it finds player or null
        public PlayerControllerB playerParentWalk(GameObject leaf)
        {
            while (leaf != null && leaf.GetComponent<PlayerControllerB>() == null)
            {
                if (leaf.transform.parent && leaf.transform.parent.gameObject)
                {
                    leaf = leaf.transform.parent.gameObject;
                }
                else
                {
                    leaf = null;
                }
            }

            if (leaf && leaf.GetComponent<PlayerControllerB>())
            {
                return leaf.GetComponent<PlayerControllerB>();
            }

            return null;
        }
    }
}
