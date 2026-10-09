using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EFX_tailspawner : MonoBehaviour
{
	public Transform ObjectSpawn;
	public float SpawnRate = 0.2f;
	public int AreaSpawnSize = 20;
	public int LifetimeSpaned = 3;
	private float timetemp;

	void  Start ()
	{
	}

	void  Update ()
	{
		if (Time.time >= timetemp + SpawnRate) {
			Transform objectspawned = Instantiate (ObjectSpawn, transform.position + new Vector3 (Random.Range (-AreaSpawnSize, AreaSpawnSize), 0, Random.Range (-AreaSpawnSize, AreaSpawnSize)), Quaternion.identity);
			GameObject.Destroy (objectspawned.gameObject, 3);
			timetemp = Time.time;
		}  

	}
}