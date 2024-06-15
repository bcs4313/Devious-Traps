using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class MeteorScript : MonoBehaviour
{
    AudioSource src;
    public ParticleSystem part;
    public List<ParticleCollisionEvent> collisionEvents;

    // Start is called before the first frame update
    void Start()
    {
        src = this.GetComponent<AudioSource>();
        part = GetComponent<ParticleSystem>();
        collisionEvents = new List<ParticleCollisionEvent>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnParticleCollision(GameObject other)
    {
        // This method is called when particles from the system collide with another object
        int numCollisionEvents = part.GetCollisionEvents(other, collisionEvents);

        int i = 0;
        while (i < numCollisionEvents)
        {
            Vector3 pos = collisionEvents[i].intersection;
            spawnExplosionClientRpc(pos);
            i++;
        }
    }

    [ClientRpc]
    async void spawnExplosionClientRpc(Vector3 position)
    {
        Landmine.SpawnExplosion(position + UnityEngine.Vector3.up, true, 18f, 22f);
        // play sound at destination while also deleting object after a certain time
        GameObject gSource = new GameObject();
        gSource.transform.position = position;
        AudioSource source = gSource.AddComponent<AudioSource>();
        source.clip = src.clip;
        source.priority = src.priority;
        source.bypassReverbZones = src.bypassReverbZones;
        source.bypassEffects = src.bypassEffects;
        source.loop = src.loop;
        source.playOnAwake = src.playOnAwake;
        source.spatialBlend = src.spatialBlend;
        source.volume = src.volume;
        source.minDistance = src.minDistance;
        source.maxDistance = src.maxDistance;
        source.rolloffMode = src.rolloffMode;
        source.outputAudioMixerGroup = src.outputAudioMixerGroup;
        source.Play();

        await Task.Delay(8000);
        Destroy(gSource);
    }
}
