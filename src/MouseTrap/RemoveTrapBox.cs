using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace DeviousTraps.src.MouseTrap
{
    public class RemoveTrapBox : NetworkBehaviour
    {
        public MouseTrap TrapLink;
        public BoxCollider hitbox;
        public InteractTrigger trigger;
        public AudioSource detachSound;

        public void Start()
        {
            this.enabled = false;

            if (Plugin.AttachmentWepRequired.Value == false)
            {
                if (trigger) { trigger.hoverTip = "Pry off Mousetrap"; }
            }
        }

        public void Activate()
        {
            this.enabled = true;
        }

        public bool playerIsDefenseless(PlayerControllerB player)
        {
            if(Plugin.AttachmentWepRequired.Value == false) { return false; }

            Debug.Log("defenselesscheck: " + player);
            var slots = player.ItemSlots;

            GrabbableObject item = slots[player.currentItemSlot];
            if (item && item.itemProperties && item.itemProperties.isDefensiveWeapon)
            {
                return false;
            }

            return true;
        }

        public void startDetachEvent()
        {
            if (RoundManager.Instance.IsHost)
            {
                startDetachEventClientRpc();
            }
            else
            {
                startDetachEventServerRpc();
            }
        }

        public void detachEventCancel()
        {
            if (RoundManager.Instance.IsHost)
            {
                detachCancelEventClientRpc();
            }
            else
            {
                detachCancelEventServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void startDetachEventServerRpc()
        {
            startDetachEventClientRpc();
        }

        [ClientRpc]
        public void startDetachEventClientRpc()
        {
            detachSound.Play();
        }


        [ServerRpc(RequireOwnership = false)]
        public void detachCancelEventServerRpc()
        {
            detachCancelEventClientRpc();
        }

        [ClientRpc]
        public void detachCancelEventClientRpc()
        {
            detachSound.Stop();
        }

        public void detachEvent(PlayerControllerB targetPlayer)
        {
            if(targetPlayer == null)
            {
                Debug.LogError("DeviousTraps: Detach event error. Player is null");
                return;
            }
            Debug.Log("Devious Traps: Mouse trap detach Event");
            if (playerIsDefenseless(targetPlayer))
            {
                Debug.Log("Devious Traps: Mouse trap detach event failure, player not holding weapon");
                HUDManager.Instance.DisplayTip("Trap Removal Failed", "You need a weapon to remove the mouse trap!", true);
                return;
            }

            if(RoundManager.Instance.IsHost)
            {
                detachEventClientRpc();
            }
            else
            {
                detachEventServerRpc();
            }
        }

        [ServerRpc (RequireOwnership = false)]
        public void detachEventServerRpc()
        {
            detachEventClientRpc();
        }

        [ClientRpc]
        public void detachEventClientRpc()
        {
            Debug.Log("Devious Traps: Mouse Trap Detach Event Successfull");
            if (TrapLink.springJoint)
            {
                Destroy(TrapLink.springJoint);
            }

            if(TrapLink.stuckTo)
            {
                TrapLink.stuckTo.carryWeight -= 0.05f; // 11 lb
                TrapLink.stuckTo = null;
            }
        }
    }
}
