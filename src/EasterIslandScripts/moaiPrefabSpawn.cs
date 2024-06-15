using Unity.Netcode;
using UnityEngine.VFX;
using UnityEngine;
using System.Threading.Tasks;

public class GoldMoaiSpawn : EnemyAI
{
    bool awaitSpawn = true;

    [Space(5f)]
    public GameObject hivePrefab;

    public NoisemakerProp hive;

    public override void Start()
    {
        base.Start();
        if (base.IsServer)
        {
            SpawnHiveNearEnemy();
        }
    }

    private async void SpawnHiveNearEnemy()
    {
        if (base.IsServer)
        {
            while (awaitSpawn)
            {
                var nodes = RoundManager.Instance.outsideAINodes;
                if (nodes == null || nodes.Length == 0)
                {
                    Debug.Log($"Moai Enemy: Awaiting to spawn gold moai - 1...");
                    await Task.Delay(1000);
                    continue;
                }

                Vector3 originPos = nodes[new System.Random().Next(0, nodes.Length)].transform.position;
                Vector3 randomNavMeshPositionInBoxPredictable = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(originPos, 3f, RoundManager.Instance.navHit, new System.Random(), -5);
                if (randomNavMeshPositionInBoxPredictable == originPos)
                {
                    Debug.Log($"Moai Enemy: Awaiting to spawn gold moai - 2...");
                    await Task.Delay(1000);
                    continue;
                }

                Debug.Log($"Moai Enemy: Set gold moai random position: {randomNavMeshPositionInBoxPredictable}");
                awaitSpawn = false;
                GameObject gameObject = UnityEngine.Object.Instantiate(hivePrefab, randomNavMeshPositionInBoxPredictable + Vector3.up * 0.5f, Quaternion.Euler(Vector3.zero), RoundManager.Instance.spawnedScrapContainer);
                gameObject.SetActive(value: true);
                gameObject.GetComponent<NetworkObject>().Spawn();
                gameObject.GetComponent<NoisemakerProp>().targetFloorPosition = randomNavMeshPositionInBoxPredictable + Vector3.up * 0.5f;
                SpawnHiveClientRpc(hiveObject: gameObject.GetComponent<NetworkObject>(), hivePosition: randomNavMeshPositionInBoxPredictable + Vector3.up * 0.5f);
            }
        }
    }

    [ClientRpc]
    public void SpawnHiveClientRpc(NetworkObjectReference hiveObject, Vector3 hivePosition)
    {
        if (hiveObject.TryGet(out var networkObject))
        {
            hive = networkObject.gameObject.GetComponent<NoisemakerProp>();
            hive.targetFloorPosition = hivePosition;

            int hiveScrapValue = new System.Random().Next(50, 501);

            hive.scrapValue = hiveScrapValue;
            ScanNodeProperties componentInChildren = hive.GetComponentInChildren<ScanNodeProperties>();
            if (componentInChildren != null)
            {
                componentInChildren.scrapValue = hiveScrapValue;
                componentInChildren.headerText = "Golden Moai Head";
                componentInChildren.subText = $"VALUE: ${hiveScrapValue}";
            }

            RoundManager.Instance.totalScrapValueInLevel += hive.scrapValue;
        }
        else
        {
            Debug.LogError("Moai Enemy: Error! gold moai could not be accessed from network object reference");
        }
        Object.Destroy(this.gameObject);
    }
}