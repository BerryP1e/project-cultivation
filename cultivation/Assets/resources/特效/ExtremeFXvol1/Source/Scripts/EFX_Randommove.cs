using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_Randommove : MonoBehaviour
{
	public Vector3 SpeedMin;
	public Vector3 SpeedMax;
	public bool velosityRandom;
	public bool zigzax;
	private Vector3 power;
	private Vector3 powerlast;

	void  Update ()
	{
		if (velosityRandom) {
			if (this.gameObject.GetComponent<Rigidbody> ()) {
				Vector3 velocity = this.gameObject.GetComponent<Rigidbody> ().velocity;
				velocity.x += Random.Range (SpeedMin.x, SpeedMax.y) * Time.deltaTime;
				velocity.y += Random.Range (SpeedMin.y, SpeedMax.y) * Time.deltaTime;
				velocity.z += Random.Range (SpeedMin.z, SpeedMax.z) * Time.deltaTime;
				this.gameObject.GetComponent<Rigidbody> ().velocity = velocity;
			}
		} else {
			if (zigzax) {
				power.x = Random.Range (0, SpeedMax.x);
				power.y = Random.Range (0, SpeedMax.y);
				power.z = Random.Range (0, SpeedMax.z);

				if (powerlast.x > power.x) {
					power.x *= -1;
				}

				if (powerlast.x > power.z) {
					power.z *= -1;
				}

				this.transform.position += power * Time.deltaTime;
				powerlast = power;
			} else {
				Vector3 speedrandom = this.transform.position;
				speedrandom.x += Random.Range (SpeedMin.x, SpeedMax.y) * Time.deltaTime;
				speedrandom.y += Random.Range (SpeedMin.y, SpeedMax.y) * Time.deltaTime;
				speedrandom.z += Random.Range (SpeedMin.z, SpeedMax.z) * Time.deltaTime;
				this.transform.position = speedrandom;
			}
		}
	}
}