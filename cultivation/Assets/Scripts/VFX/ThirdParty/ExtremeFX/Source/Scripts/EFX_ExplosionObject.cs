using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_ExplosionObject : MonoBehaviour
{

	public Vector3 Force;
	public GameObject Objcet;
	public int Num;
	public int Scale = 20;
	public int ScaleMin = 10;
	public AudioClip[] Sounds;
	public float LifeTimeObject = 2;
	public float postitionoffset = 2;
	public bool random;

	void  Start ()
	{
		Physics.IgnoreLayerCollision (2, 2);
		if (Sounds.Length > 0) {
			AudioSource.PlayClipAtPoint (Sounds [Random.Range (0, Sounds.Length)], transform.position);
		}
		if (Objcet) {
			if (random) {
				Num = Random.Range (1, Num);
			}
			for (int i = 0; i < Num; i++) {
				Vector3 pos = new Vector3 (Random.Range (-postitionoffset, postitionoffset), Random.Range (-postitionoffset, postitionoffset), Random.Range (-postitionoffset, postitionoffset)) / 10f;
				GameObject obj = Instantiate (Objcet, this.transform.position + pos, Random.rotation);
				float scale = Random.Range (ScaleMin, Scale);
				Destroy (obj, LifeTimeObject);
				if (Scale > 0)
					obj.transform.localScale = new Vector3 (scale, scale, scale);
				if (obj.GetComponent<Rigidbody> ()) {
					obj.GetComponent<Rigidbody> ().AddForce (new Vector3 (Random.Range (-Force.x, Force.x), Random.Range (-Force.y, Force.y), Random.Range (-Force.z, Force.z)));

				}

			}
		}
	}

}