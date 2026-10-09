using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_Spawner : MonoBehaviour
{
	public GameObject ObjectSpawn;
	public float SpawnRate;
	public float LifeTimeObject = 1;
	public int LimitObject = 3;
	private float timetemp;
	private int objcount;
	public Vector3 PositionRandomSize;
	public Vector3 PositionOffset;

	void  Start ()
	{
		timetemp = Time.time;
	}

	void  Update ()
	{
		if (Time.time > timetemp + SpawnRate) {
			if (ObjectSpawn) {
				if (objcount < LimitObject) {
					Vector3 positionoffset = new Vector3 (Random.Range (-PositionRandomSize.x, PositionRandomSize.x), Random.Range (-PositionRandomSize.y, PositionRandomSize.y), Random.Range (-PositionRandomSize.z, PositionRandomSize.z));
					GameObject obj = GameObject.Instantiate (ObjectSpawn, this.transform.position + positionoffset + PositionOffset, this.transform.rotation);
					objcount++;
					Destroy (obj, LifeTimeObject);
				}
			}
			timetemp = Time.time;
		}
	}
}