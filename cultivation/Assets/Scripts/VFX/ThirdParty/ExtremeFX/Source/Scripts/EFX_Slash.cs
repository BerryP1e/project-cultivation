using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_Slash : MonoBehaviour
{
	public Vector3 speed = Vector3.one;

	void  Update ()
	{
		Vector3 scale = transform.localScale;
		scale.x += speed.x * Time.deltaTime;
		scale.y += speed.y * Time.deltaTime;
		if (scale.y < 0) {
			scale.y = 0;
		}
		transform.localScale = scale;
	}
}