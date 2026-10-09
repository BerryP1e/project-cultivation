using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EFX_Camera : MonoBehaviour {
	public GameObject[] particleSpanwner;
	public int indexSpawn = 0;
	public bool  epictime;
	private float timetemp= 0;

	void  Start (){
		timetemp = Time.time;
	}
	void  Update (){
		this.transform.RotateAround(Vector3.up,Time.deltaTime * 0.2f);

		if(Input.GetButtonDown("Fire1"))
		{
			Ray ray = GameObject.Find("Main Camera").GetComponent<Camera>().ScreenPointToRay (Input.mousePosition);
			RaycastHit hit;

			if (Physics.Raycast (ray, out hit, 100)) {
				if(hit.transform.tag == "ground"){
					if(particleSpanwner.Length>0){
						SpawnParticle(hit.point);
					}
				}
			}

		}
		if(epictime){
			if(Time.time>timetemp+0.7f){
				timetemp = Time.time;
				SpawnParticle(new Vector3(Random.Range(-10,10),0,Random.Range(-10,10)));
				indexSpawn = Random.Range(0,particleSpanwner.Length);
			}
		}
	}


	void  SpawnParticle ( Vector3 position  ){
		Vector3 offset = Vector3.zero;

		if(particleSpanwner[indexSpawn].GetComponent<EFX_ParticleSetting>()){
			offset = particleSpanwner[indexSpawn].GetComponent<EFX_ParticleSetting>().PositionOffset;
		}
		GameObject.Instantiate(particleSpanwner[indexSpawn], position + offset, Quaternion.identity);   	
	}





	void  OnGUI (){
		if(GUI.Button(new Rect(10,10,150,30),"Prev")){
			indexSpawn--;
			if(indexSpawn<0){
				indexSpawn = particleSpanwner.Length-1;
			}
		}
		GUI.Label(new Rect(10,40,1000,30),"Particle Name: "+particleSpanwner[indexSpawn].name.ToString());
		if(GUI.Button(new Rect(170,10,150,30),"Next")){
			indexSpawn++;
			if(indexSpawn>=particleSpanwner.Length){
				indexSpawn = 0;
			}
		}

		if(GUI.Button(new Rect(350,10,120,30),"Ground "+GameObject.Find("Plane").gameObject.GetComponent<Renderer>().enabled)){
			if(GameObject.Find("Plane").gameObject.GetComponent<Renderer>().enabled){
				GameObject.Find("Plane").gameObject.GetComponent<Renderer>().enabled = false;
			}else{
				GameObject.Find("Plane").gameObject.GetComponent<Renderer>().enabled = true;
			}
		}

		if(GUI.Button(new Rect(480,10,120,30),"Show time")){
			if(epictime){
				epictime = false;
			}else{
				epictime = true;
			}
		}


	}
}