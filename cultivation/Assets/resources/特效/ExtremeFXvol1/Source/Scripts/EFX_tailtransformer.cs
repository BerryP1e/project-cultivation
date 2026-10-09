using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_tailtransformer : MonoBehaviour
{
	private Vector3 target;
	public Vector3 DirectionRandomMax;
	public Vector3 DirectionRandomMin;
	public float Speed = 1;
	private float timetemp;
	public float redirectRate = 0.5f;

	void  Start ()
	{
	}

	void  Update ()
	{
		if (Time.time >= timetemp + redirectRate) {
			target = new Vector3 (Random.Range (DirectionRandomMin.x, DirectionRandomMax.x), Random.Range (DirectionRandomMin.y, DirectionRandomMax.y), Random.Range (DirectionRandomMin.z, DirectionRandomMax.z));
			timetemp = Time.time;
		} 
		Quaternion targetRotation = Quaternion.LookRotation (target - transform.position);
		float str = Mathf.Min (10 * Time.deltaTime, 1);
		transform.rotation = Quaternion.Lerp (transform.rotation, targetRotation, str);
		transform.position += transform.forward * Speed * Time.deltaTime;
	}
}