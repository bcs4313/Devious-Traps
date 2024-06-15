
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.VirtualTexturing;

public class Spewer : NetworkBehaviour
{
    int eruptHour;
    int currentHour = -1;
    bool noErupt;

    // Start is called before the first frame update
    void Start()
    {
        var g = transform.Find("meteors").gameObject;
        g.GetComponent<ParticleSystem>().Stop();
        if (RoundManager.Instance.IsHost)
        {
            // select eruption time
            if (Random.Range(0.0f, 1.0f) < 0.50)  // 50% chance for no eruption at all
            {
                eruptHour = getHour() + 999;
                noErupt = true;
            }
            else
            {
                eruptHour = getHour() + Random.Range(2, 17);
                noErupt = false;
            }

            fogTick();  // set fog color
        }
    }

    void fogTick()
    {
        if (RoundManager.Instance.IsHost)
        {
            int caseHour = eruptHour - getHour();

            if (noErupt || caseHour < 0)
            {  // light blue
                setFogColorClientRpc(new Color(0f / 255f, 255f / 255f, 206f / 255f));
            }
            else
            {
                switch (caseHour)
                {
                    case 0:  // orange
                        setFogColorClientRpc(new Color(255f / 255f, 141f / 255f, 0f / 255f));
                        break;
                    case 1:  // orange
                        setFogColorClientRpc(new Color(255f / 255f, 141f / 255f, 0f / 255f));
                        break;
                    case 2:  // red 
                        setFogColorClientRpc(new Color(255f / 255f, 0f / 255f, 0f / 255f));
                        break;
                    case 3:  // green
                        setFogColorClientRpc(new Color(0f / 255f, 255f / 255f, 0f / 255f));
                        break;
                    case 4:  // yellow
                        setFogColorClientRpc(new Color(239f / 255f, 255f / 255f, 0f / 255f));
                        break;
                    default: // purple
                        setFogColorClientRpc(new Color(135f / 255f, 0f / 255f, 255f / 255f));
                        break;
                }
            }
        }
    }

    int getHour()
    {
        return TimeOfDay.Instance.hour;
    }

    // Update is called once per frame
    void Update()
    {
        if (RoundManager.Instance.IsHost)
        {
            // called whenever the hour changes
            if (currentHour != getHour())
            {
                currentHour = getHour();
                fogTick();
                var g = transform.Find("meteors").gameObject;
                if (getHour() == eruptHour)
                {
                    var randomSeed = (uint)Random.Range(0, 255000);
                    playParticleSystemClientRpc(randomSeed);

                }
                else
                {
                    stopParticleSystemClientRpc();
                }
            }
        }
    }

    [ClientRpc]
    void setFogColorClientRpc(Color color)
    {
        Debug.Log("MOAI: setFogColorClientRpc Called");
        Transform fogParent = GameObject.Find("VolcanoMeters").transform;

        // Loop through each child of the parent GameObject
        foreach (Transform child in fogParent)
        {
            // Do something with each child
            if (child.name.Contains("Fog"))
            {
                LocalVolumetricFog fog = child.GetComponent<LocalVolumetricFog>();

                fog.parameters.albedo = color;
            }
        }
    }

    [ClientRpc]
    void playParticleSystemClientRpc(uint seed)
    {
        Debug.Log("MOAI: playParticleSystemClientRpc Called");
        var g = transform.Find("meteors").gameObject;
        var system = g.GetComponent<ParticleSystem>();
        system.randomSeed = seed;
        system.GetComponent<ParticleSystem>().Play();
    }

    [ClientRpc]
    void stopParticleSystemClientRpc()
    {
        Debug.Log("MOAI: stopParticleSystemClientRpc Called");
        var g = transform.Find("meteors").gameObject;
        var system = g.GetComponent<ParticleSystem>();
        system.Stop();
    }
}
