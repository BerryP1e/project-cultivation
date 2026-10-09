using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EFX_ParticleSetting : MonoBehaviour {
	public float LightIntensityMult = -0.5f;
	public float LifeTime = 1;
	public bool  RandomRotation = false;
	public Vector3 PositionOffset;
	public GameObject SpawnEnd;
	private float timetemp;

	void  Start (){
		timetemp = Time.time;
		this.transform.position += PositionOffset;
		if(RandomRotation){
			this.gameObject.transform.rotation = Random.rotation;
		}

	}
	void  Update (){
		if(Time.time > timetemp + LifeTime){
			if(SpawnEnd){
				GameObject.Instantiate(SpawnEnd,this.transform.position,this.transform.rotation);
			}
			GameObject.Destroy(this.gameObject);
		}
		if(this.gameObject.GetComponent<Light>()){
			this.GetComponent<Light>().intensity += LightIntensityMult * Time.deltaTime;
		}
	}
}