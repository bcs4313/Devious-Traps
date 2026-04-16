using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace DeviousTraps.src.MouseTrap
{
    // what actually spawns in the factory
    // creates a group of mouse traps
    public class MouseTrapSpawner : NetworkBehaviour
    {
        int minTraps = 6;
        int maxTraps = 9;
        public static System.Random rnd = new System.Random();

        // controls the grouping of the mouse traps
        public static float minDistFromInstancesRatio = 0.02f;  // proportion of the spawn circle
        float spawnCubeMinSize = 7f;
        float spawnCubeMaxSize = 13f;
        int maxAttempts = 3;  // max attempts a single mousetrap can make to find a position

        public void Start()
        {
            if(RoundManager.Instance.IsHost)
            {
                var pos1 = transform.position;
                for (int i = 0; i < 2; i++)
                {
                    SpawnTraps(pos1);
                }

                var pos2 = RoundManager.Instance.insideAINodes[rnd.Next(0, RoundManager.Instance.insideAINodes.Length)].transform.position;
                for (int i = 0; i <  2; i++)
                {
                    SpawnTraps(pos2);
                }

                var pos3 = RoundManager.Instance.insideAINodes[rnd.Next(0, RoundManager.Instance.insideAINodes.Length)].transform.position;
                for (int i = 0; i < 2; i++)
                {
                    SpawnTraps(pos3);
                }
            }
        }

        public void SpawnTraps(Vector3 pos)
        {
            int amount = rnd.Next(minTraps, maxTraps);
            float spread = amount;
            for (int i = 0; i < amount; i++)
            {
                Quaternion spawnRot = Plugin.MouseTrapPrefab.transform.rotation;

                spawnRot = Quaternion.Euler(spawnRot.x, (float)(360 * rnd.NextDouble()), spawnRot.z);

                // calculate spawn position
                Vector3 spawnPos = PickSpawnPosition(pos);
                int attempts = maxAttempts - 1;
                while (spawnPos == Vector3.zero && attempts > 0)
                {
                    spawnPos = PickSpawnPosition(pos);
                    attempts -= 1;
                }

                if (spawnPos != Vector3.zero)
                {
                    GameObject trap = GameObject.Instantiate(Plugin.MouseTrapPrefab, spawnPos, spawnRot);
                    trap.GetComponent<NetworkObject>().Spawn();
                    spawnLocations.Add(spawnPos);
                }
            }
        }

        public Vector3 PickSpawnPosition(Vector3 pos)
        {
            float spawnCube = (float)(spawnCubeMinSize + (rnd.NextDouble() * (spawnCubeMaxSize - spawnCubeMinSize)));
            // generate a random coordinate
            float x = (float)((spawnCube / 2) - rnd.NextDouble() * spawnCube);
            float y = (float)((spawnCube / 2) - rnd.NextDouble() * spawnCube);
            float z = (float)((spawnCube / 2) - rnd.NextDouble() * spawnCube);

            NavMeshHit hit;
            bool result = NavMesh.SamplePosition(new Vector3(pos.x + x, pos.y, pos.z + z), out hit, spawnCube * 3, NavMesh.AllAreas);

            if(result && withinProximity(hit.position, spawnCube))
            {
                return hit.position;
            }
            else
            {
                return Vector3.zero;
            }
        }

        public List<Vector3> spawnLocations = new List<Vector3>();
        public bool withinProximity(Vector3 pos, float spawnCube)
        {
            for(int i = 0; i < spawnLocations.Count; i++)
            {
                if(Vector3.Distance(pos, spawnLocations[i]) < (spawnCube * minDistFromInstancesRatio))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
