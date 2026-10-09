using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_AnimationUV2 : MonoBehaviour
{
	public float speed = 0.5f;

	void  Start ()
	{

	}

	void  Update ()
	{
		Vector2 offset = GetComponent<Renderer> ().material.mainTextureOffset;
		offset.x += speed * Time.deltaTime;
		if (offset.x > 1) {
			offset.x = 0;
		}
		if (offset.x < 0) {
			offset.x = 1;
		}
		GetComponent<Renderer> ().material.mainTextureOffset = offset;
	}
}
