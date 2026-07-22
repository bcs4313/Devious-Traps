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
        bool itemCaptured = false;
        Transform ScrapAttachment;
        public void Update()
        {
            // store scrap attachment point
            if (!ScrapAttachment) {
                ScrapAttachment = transform.root.GetComponent<MouseTrap>().ScrapAttachment; 
            }
            
            // object is permanently grabbable once held by a player
            if (attachedObject && attachedObject.playerHeldBy != null)
            {
                attachedObject.grabbable = true;
                attachedObject = null;
                itemCaptured = true;  // prevents the item from not being grabbable again later
            }
            if(itemCaptured && attachedObject) { attachedObject.grabbable = true; }  // a little redundant but I'm playing it safe

            // lock item to position every frame until it is captured by the player
            // (simpler logic than parenting)
            if(!itemCaptured && attachedObject)
            {
                attachedObject.parentObject = null;
                attachedObject.transform.parent = null;
                attachedObject.transform.position = ScrapAttachment.transform.position;
                attachedObject.startFallingPosition = ScrapAttachment.transform.position;
                attachedObject.targetFloorPosition = ScrapAttachment.transform.position;

                // simply updates the scan node subtext
                /**
                if (attachedObject.GetComponentInChildren<ScanNodeProperties>())
                {
                    if(attachedObject.scrapValue != 0)
                    {
                        attachedObject.GetComponentInChildren<ScanNodeProperties>().subText = "Value: " + attachedObject.scrapValue;
                    }
                }
                */
            }


            // initializer, for server only to notify clients
            if (!RoundManager.Instance.IsHost) { return; }
            if ((!itemInitialized || refreshItemInitCooldown < 0) && attachedObject && attachedObject.GetComponent<NetworkObject>().IsSpawned)
            {
                itemInitialized = true;
                AttachObjectClientRpc(attachedObject.NetworkObjectId, attachedObject.scrapValue);
                refreshItemInitCooldown = UnityEngine.Random.Range(2f, 3f);
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
        public void AttachObjectClientRpc(ulong uid, int value)
        {
            var objs = FindObjectsOfType<GrabbableObject>();
            foreach (var obj in objs)
            {
                if (obj && uid == obj.NetworkObjectId && !attachedObject)
                {
                    attachedObject = obj;
                    obj.grabbable = false;
                    attachedObject.GetComponentInChildren<ScanNodeProperties>().subText = "Value: " + value;
                    // no more parenting logic. very unreliable
                    //obj.transform.parent = transform.root.GetComponent<MouseTrap>().ScrapAttachment;
                    //obj.startFallingPosition = I
                    //obj.targetFloorPosition = Vector3.zero;
                    //if (!obj.isHeld) { obj.FallToGround(); }
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
            if (ply && !itemCaptured && attachedObject)
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
