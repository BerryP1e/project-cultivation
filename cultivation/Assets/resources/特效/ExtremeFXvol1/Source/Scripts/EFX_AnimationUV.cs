using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EFX_AnimationUV : MonoBehaviour {
	public int uvAnimationTileX = 24;
	public int uvAnimationTileY = 1;
	public float framesPerSecond = 10.0f;
	public bool  loop;
	public bool  play = true;
	private int index; 
	private float offsettime;
	public bool Hidewhenstopplaying;

	void  Start (){
		offsettime = Time.time;
	}

	void  Update (){
		index = (int)((Time.time - offsettime) * framesPerSecond);
		if(play){
			index = (index % (uvAnimationTileX * uvAnimationTileY));
			Vector2 size = new Vector2 (1.0f / uvAnimationTileX, 1.0f / uvAnimationTileY);
			float uIndex= index % uvAnimationTileX;
			float vIndex= index / uvAnimationTileX;
			Vector2 offset = new Vector2 (uIndex * size.x, 1.0f - size.y - vIndex * size.y);

			GetComponent<Renderer>().material.SetTextureOffset ("_MainTex", offset);
			GetComponent<Renderer>().material.SetTextureScale ("_MainTex", size);
		}
		if(!loop){
			if(index >= (uvAnimationTileX * uvAnimationTileY)-1){
				play = false;
				if(Hidewhenstopplaying){
					if(GetComponent<Renderer> ())
						GetComponent<Renderer> ().enabled = false;
				}
			}
		}

	}
}