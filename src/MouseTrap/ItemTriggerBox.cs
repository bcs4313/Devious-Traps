using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeviousTraps.src.MouseTrap
{
    public class ItemTriggerBox : MonoBehaviour
    {
        public GrabbableObject attachedObject;

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
        public void Update()
        {
            if(attachedObject && attachedObject.playerHeldBy != null)
            {
                attachedObject.grabbable = true;
                attachedObject = null;
            }
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
            if (ply && cont.localPlayerController.NetworkObjectId == ply.NetworkObjectId)
            {
                if (attachedObject) { attachedObject.grabbable = true; }
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
            if (ply && cont.localPlayerController.NetworkObjectId == ply.NetworkObjectId)
            {
                if (attachedObject) { attachedObject.grabbable = false; }
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
