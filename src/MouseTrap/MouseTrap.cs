using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace DeviousTraps.src.MouseTrap
{
    public class MouseTrap : NetworkBehaviour, IHittable
    {
        public Animator animator;
        public AudioSource activateSound;

        public Rigidbody rigidBody;
        public SpringJoint springJoint;
        public BoxCollider collider;

        public static List<MouseTrap> instances = new List<MouseTrap>();

        float BigSize = 3f;
        float MegaSize = 5;
        bool isMega = false;
        bool isBig = false;

        public Transform ScrapAttachment;

        public static float LandmineForceSensitivity = 5f;
        public static float FlashForceSensitivity = 5f;
        public static float DistFalloff = 2f;
        public static float UpTendency = 0.37f;
        public static System.Random rnd = new System.Random();

        public ItemTriggerBox triggerBox;
        public GrabbableObject localObj;

        public RemoveTrapBox removalHitbox;
        public BoxCollider removalCollider;

        int Dmg = 0;

        public float BcontactOffset;
        public Vector3 Bsize;

        public void Start()
        {
            try
            {

                if (RoundManager.Instance.IsHost)
                {
                    if (rnd.NextDouble() < (Plugin.GiantMTrapChance.Value / 100))
                    {
                        MakeGiantClientRpc();
                        AttachScrap();
                    }
                    else if (rnd.NextDouble() < (Plugin.BigMTrapChance.Value / 100))
                    {
                        MakeBigClientRpc();
                    }
                    else
                    {
                        MakeSmallClientRpc();
                    }
                    instances.Add(this);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        [ClientRpc]
        public void MakeSmallClientRpc()
        {
            Dmg = Plugin.SmallMTrapDmg.Value;
        }

        [ClientRpc]
        public void MakeBigClientRpc()
        {
            transform.localScale = new Vector3(BigSize, BigSize, BigSize);
            Dmg = Plugin.BigMTrapDmg.Value;
            activateSound.pitch *= 0.5f;
            isBig = true;
        }

        [ClientRpc]
        public void MakeGiantClientRpc()
        {
            transform.localScale = new Vector3(MegaSize, MegaSize, MegaSize);
            Dmg = Plugin.GiantMTrapDmg.Value;
            activateSound.pitch *= 0.25f;
            isBig = true;
            isMega = true;
            GiantVectSize = new Vector3(collider.size.x, collider.size.y * 0.5f, collider.size.z);
            GiantVectCenter = new Vector3(collider.center.x, collider.center.y * 0.285f, collider.center.z);
            collider.set_size_Injected(ref GiantVectSize);
            collider.set_center_Injected(ref GiantVectCenter);
        }

        private static Vector3 GiantVectSize;
        private static Vector3 GiantVectCenter;

        float teleportToPlayerThreshold = 5f;
        public void Update()
        {
            if (localObj && RoundManager.Instance.IsHost && triggerBox.attachedObject != null)
            {
                localObj.transform.localPosition = Vector3.zero;
                localObj.transform.parent = ScrapAttachment;
                localObj.startFallingPosition = Vector3.zero;
                localObj.targetFloorPosition = Vector3.zero;
            }

            if (stuckTo)
            {
                // teleport to player when too far away
                if (Vector3.Distance(stuckTo.transform.position, transform.position) > teleportToPlayerThreshold)
                {
                    this.transform.position = stuckTo.transform.position;
                }
            }
        }

        public bool inBaitSet(GrabbableObject gobj)
        {
            string list = Plugin.MTrapWhitelist.Value;
            string[] list_entries = list.Split(",");
            foreach (string entry in list_entries)
            {
                var entry_stripped = entry.ToLower().Trim(); 

                if (gobj)
                {
                    var node = gobj.GetComponentInChildren<ScanNodeProperties>();
                    if (node)
                    {
                        if (node.headerText.ToLower().Trim().Contains(entry_stripped))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void AttachScrap()
        {
            if (ScrapAttachment == null)
            {
                Debug.LogError("MouseTrap: ScrapAttachment is null");
                return;
            }

            var scrapOptions = Resources.FindObjectsOfTypeAll<GrabbableObject>();
            List<GrabbableObject> finalOptions = new List<GrabbableObject>();

            if (scrapOptions == null || scrapOptions.Length == 0)
            {
                Debug.LogWarning("MouseTrap: No scrap options found");
                return;
            }


            for (int i = 0; i < scrapOptions.Length; i++)
            {
                var scrap = scrapOptions[i];
                if (scrap != null && inBaitSet(scrap))
                {
                    finalOptions.Add(scrap);
                }
            }

            var bestScrap = finalOptions[rnd.Next(0, finalOptions.Count)];

            if (bestScrap == null) return;

            var instance = Instantiate(
                bestScrap,
                ScrapAttachment.position,
                ScrapAttachment.rotation,
                ScrapAttachment
            );

            instance.scrapValue = rnd.Next(instance.itemProperties.minValue, instance.itemProperties.maxValue);
            if(instance.GetComponentInChildren<ScanNodeProperties>())
            {
                instance.GetComponentInChildren<ScanNodeProperties>().subText = "Value: " + instance.scrapValue;
            }

            if (IsServer && instance.TryGetComponent(out NetworkObject netObj))
                netObj.Spawn();

            triggerBox.LinkItemToTrigger(instance);
            var obj = instance.GetComponent<GrabbableObject>();
            if (obj)
            {
                localObj = obj;
            }
        }

        bool activated = false;
        public void OnTriggerEnter(Collider other)
        {
            //Debug.Log("triggerenter: " + other.name);
            if (activated) { return; }

            if (gameObject.layer != LayerMask.NameToLayer("MapHazards"))
            {
                return;
            }

            if (other.gameObject.layer != LayerMask.NameToLayer("Player") && !other.gameObject.name.ToLower().Contains("shin") && !other.gameObject.name.ToLower().Contains("thigh"))
            {
                return;
            }

            //Debug.Log("Mouse Trap hit: " + other.gameObject);

            if (RoundManager.Instance.IsHost)
            {
                var go = other.gameObject;
                PlayerControllerB ply = playerParentWalk(go);

                DmgEvent(ply);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void DmgEventServerRpc()
        {
            DmgEvent(null);
        }

        public void DmgEvent(PlayerControllerB ply)
        {
            if (activated) { return; }
            activated = true;
            if (ply)
            {
                int dmg = Dmg;
                if (dmg >= ply.health)
                {
                    KillPlayerClientRpc(ply.NetworkObject.NetworkObjectId);
                    Snap(ply, true);
                }
                else
                {
                    DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, dmg);
                    Snap(ply, false);
                }

                activated = true;
            }
            else
            {
                Snap(null, false);
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

        // attaches to a target player, plays a sound, animates the trap
        public void Snap(PlayerControllerB player, bool isDead)
        {
            if (RoundManager.Instance.IsHost) { PlaySnapAnimationClientRpc(); }

            if (player)
            {
                if (!isBig && !isMega)
                {
                    if (Plugin.SmallMTrapAttaches.Value) { SnapToPlayer(player); }
                }
            }
        }

        [ClientRpc]
        public void PlaySnapAnimationClientRpc()
        {
            activateSound.Play();
            animator.Play("Activate");
        }

        public PlayerControllerB stuckTo;

        public Transform attachmentPoint;
        public void SnapToPlayer(PlayerControllerB player)
        {
            Debug.Log("MouseTrap: SnapToPlayerBody");
            if (player == null) return;

            // Find the player's main rigidbody (root collision body)
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb == null)
            {
                Debug.LogWarning("MouseTrap: Player has no Rigidbody!");
                return;
            }

            // Ensure the trap has a rigidbody
            Rigidbody trapRb = GetComponent<Rigidbody>();
            if (trapRb == null)
            {
                trapRb = gameObject.AddComponent<Rigidbody>();
            }

            // Configure trap physics so it dangles nicely
            trapRb.mass = 0.5f;
            trapRb.drag = 0.2f;
            trapRb.angularDrag = 0.05f;
            trapRb.interpolation = RigidbodyInterpolation.Interpolate;
            trapRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Remove any existing joint (failsafe)
            SpringJoint existing = GetComponent<SpringJoint>();
            if (existing != null)
            {
                Destroy(existing);
            }

            // Create a new spring joint
            SpringJoint joint = gameObject.AddComponent<SpringJoint>();

            // Connect to the player body
            joint.connectedBody = playerRb;

            // Anchor is on the trap (your attachment point)
            if (attachmentPoint != null)
            {
                joint.anchor = transform.InverseTransformPoint(attachmentPoint.position);
            }
            else
            {
                joint.anchor = Vector3.zero;
            }

            // Attach to the center of the player body
            joint.autoConfigureConnectedAnchor = false;

            // Where on the trap the clamp point is (trap-local)
            joint.anchor = attachmentPoint != null
                ? transform.InverseTransformPoint(attachmentPoint.position)
                : Vector3.zero;

            // Where on the player the clamp grabs (playerRb-local)
            Vector3 trapAttachWorld = attachmentPoint != null ? attachmentPoint.position : transform.position;
            joint.connectedAnchor = ComputeConnectedAnchor(player, playerRb, trapAttachWorld, ConnectedAnchorMode.ClosestPointOnBodyCollider);
            // or: ConnectedAnchorMode.AutoBest / UpperSpine / etc

            // Joint tuning for "dangling mousetrap" feel
            joint.autoConfigureConnectedAnchor = false;
            joint.spring = 80f;
            joint.damper = 8f;
            joint.minDistance = 0f;
            joint.maxDistance = 0.15f;
            joint.tolerance = 0.02f;

            // Optional: prevent trap from colliding with the player
            Collider trapCol = GetComponent<Collider>();
            Collider playerCol = player.GetComponent<Collider>();
            if (trapCol && playerCol)
            {
                Physics.IgnoreCollision(trapCol, playerCol, true);
                //Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), true);
            }

            disableColliderClientRpc(player.NetworkObjectId);

            // Snap trap near the player initially
            transform.position = attachmentPoint != null
                ? attachmentPoint.position
                : player.transform.position;

            // add to weight 
            if (!stuckTo)
            {
                player.carryWeight += 0.05f; // 11 lb
            }

            stuckTo = player;
            triggerBox.enabled = false;
            removalHitbox.Activate();
            gameObject.layer = LayerMask.NameToLayer("InteractableObject");
            springJoint = joint;
            rigidBody = trapRb;
            var newCol = gameObject.AddComponent<BoxCollider>();
            newCol.enabled = true;
            newCol.contactOffset = BcontactOffset;
            newCol.size = Bsize;
            newCol.isTrigger = true;
        }

        [ClientRpc]
        public void disableColliderClientRpc(ulong pid)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            PlayerControllerB player = players[0];
            for(int i = 0; i < players.Length; i++)
            {
                var ply = players[i];
                if(ply.NetworkObjectId == pid)
                {
                    player = ply;
                }
            }

            Collider[] trapCols = gameObject.GetComponents<Collider>();
            Collider[] playerCols = player.gameObject.GetComponents<Collider>();

            if (trapCols.Length != 0 && playerCols.Length != 0)
            {
                //Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), true);
                for (int i = 0; i < playerCols.Length; i++)
                {
                    try
                    {
                        for (int j = 0; j < trapCols.Length; j++)
                        {
                            Debug.Log("checking: " + trapCols[j] + " to " + playerCols[i]);
                            if (!trapCols[j].isTrigger)
                            {
                                Debug.Log("ignorecollision: " + trapCols[j] +  " to " + playerCols[i]);
                                Physics.IgnoreCollision(trapCols[j], playerCols[i], true);
                            }
                        }
                    }
                    catch(Exception e) {
                        Debug.LogError(e);
                    }
                }
            }

        }

        public void OnDestroy()
        {
            if (stuckTo)
            {
                stuckTo.carryWeight -= 0.05f; // 11 lb
            }
        }

        public enum ConnectedAnchorMode
        {
            ClosestPointOnBodyCollider, // best general default for "attach where you hit"
            UpperSpine,
            LowerSpine,
            Head,
            HipsOrRoot,                 // "This Player Body" / root transform-ish
            ArmsMetarig,                // if you want it to bias toward arms rig
            AutoBest                    // tries a ranked list, then collider fallback
        }

        private Vector3 ComputeConnectedAnchor(PlayerControllerB player, Rigidbody playerRb, Vector3 trapAttachWorldPos, ConnectedAnchorMode mode)
        {
            if (player == null || playerRb == null)
                return Vector3.zero;

            // Helper: convert a world point into connectedBody local space (what SpringJoint wants)
            Vector3 ToConnectedLocal(Vector3 worldPoint) => playerRb.transform.InverseTransformPoint(worldPoint);

            // 1) Collider-based anchor (robust, works even if bone refs are null)
            Vector3 ClosestPointAnchor()
            {
                // Prefer the player's root body collider if available
                var bodyCol = player.GetComponent<Collider>();
                if (bodyCol != null)
                {
                    // ClosestPoint is stable for convex colliders; for non-convex MeshCollider it’s still usually OK.
                    Vector3 p = bodyCol.ClosestPoint(trapAttachWorldPos);
                    return ToConnectedLocal(p);
                }

                // Fallback: use rigidbody transform position
                return ToConnectedLocal(playerRb.worldCenterOfMass);
            }

            // 2) Transform-based anchors (uses the references you showed)
            // NOTE: These field/property names must match your PlayerControllerB members.
            Transform TryGet(Transform t) => t != null ? t : null;

            Transform upperSpine = null;
            Transform lowerSpine = null;
            Transform head = null;
            Transform hipsOrRoot = null;
            Transform armsMetarig = null;

            // These names are based on what you showed in the inspector.
            // If any don't compile, rename to your actual PlayerControllerB field names.
            try
            {
                // common LC PlayerControllerB refs (from your screenshot)
                // adjust names to match your actual compiled fields:
                upperSpine = TryGet(player.upperSpine);               // "Upper Spine"
                lowerSpine = TryGet(player.lowerSpine);               // "Lower Spine"
                head = TryGet(player.playerGlobalHead);               // "Player Global Head"
                hipsOrRoot = TryGet(player.thisPlayerBody);           // "This Player Body" (Transform)
                armsMetarig = TryGet(player.playerModelArmsMetarig);  // "Player Model Arms Metarig"
            }
            catch
            {
                // If fields differ between versions/mod forks, we’ll just rely on collider fallback.
            }

            Vector3 TransformAnchor(Transform t)
            {
                if (t == null) return ClosestPointAnchor();
                return ToConnectedLocal(t.position);
            }

            // 3) Mode selection
            switch (mode)
            {
                case ConnectedAnchorMode.ClosestPointOnBodyCollider:
                    return ClosestPointAnchor();

                case ConnectedAnchorMode.UpperSpine:
                    return TransformAnchor(upperSpine);

                case ConnectedAnchorMode.LowerSpine:
                    return TransformAnchor(lowerSpine);

                case ConnectedAnchorMode.Head:
                    return TransformAnchor(head);

                case ConnectedAnchorMode.HipsOrRoot:
                    return TransformAnchor(hipsOrRoot);

                case ConnectedAnchorMode.ArmsMetarig:
                    return TransformAnchor(armsMetarig);

                case ConnectedAnchorMode.AutoBest:
                default:
                    {
                        // Ranked: upper/lower spine usually look best for "clamped to body",
                        // then hips/root, then head, then arms rig, then collider closest point.
                        if (upperSpine != null) return TransformAnchor(upperSpine);
                        if (lowerSpine != null) return TransformAnchor(lowerSpine);
                        if (hipsOrRoot != null) return TransformAnchor(hipsOrRoot);
                        if (head != null) return TransformAnchor(head);
                        if (armsMetarig != null) return TransformAnchor(armsMetarig);
                        return ClosestPointAnchor();
                    }
            }
        }


        [ClientRpc]
        public void DmgPlayerClientRpc(ulong netid, int amount)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var ply in players)
            {
                if (ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.DamagePlayer(amount);
                    Snap(ply, false);
                }
            }
        }

        [ClientRpc]
        public void KillPlayerClientRpc(ulong netid)
        {
            var players = RoundManager.Instance.playersManager.allPlayerScripts;
            foreach (var ply in players)
            {
                if (ply.NetworkObject.NetworkObjectId == netid)
                {
                    ply.KillPlayer(ply.playerRigidbody.velocity, true, CauseOfDeath.Crushing);
                    Snap(ply, true);
                }
            }
        }

        // trap activates if hit by a shovel
        public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            Debug.Log("Devious Traps: MouseTrap Hit -> playerWhoHit: " + playerWhoHit + " hitID = " + hitID);

            if(Plugin.MTrapCanBeDisabled.Value == false) { return false; }

            if (!activated & RoundManager.Instance.IsHost)
            {
                DmgEvent(null);
            }
            else
            {
                DmgEventServerRpc();
            }



            if (RoundManager.Instance.IsHost)
            {
                removalHitbox.detachEventClientRpc();
            }
            else
            {
                removalHitbox.detachEventServerRpc();
            }


            if (playerWhoHit) { return true; }
            return false;
        }
    }
}
