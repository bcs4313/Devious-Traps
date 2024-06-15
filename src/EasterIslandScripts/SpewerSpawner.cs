
using LethalLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.VirtualTexturing;

public class SpewerSpawner : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (RoundManager.Instance.IsHost)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(EasterIsland.Plugin.EruptionController, this.transform.position, Quaternion.Euler(Vector3.zero));
            gameObject.SetActive(value: true);
            gameObject.GetComponent<NetworkObject>().Spawn();
        }
    }
}
