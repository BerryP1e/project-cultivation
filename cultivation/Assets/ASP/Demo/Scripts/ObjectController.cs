using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASP.Demo
{
    public class ObjectController : MonoBehaviour
    {

        // Update is called once per frame
        void Update()
        {
            Vector3 movement = Vector3.zero;
            if (Input.GetKey(KeyCode.W))
            {
                movement = transform.forward * Time.deltaTime * 1f;
            }

            if (Input.GetKey(KeyCode.S))
            {
                movement = -transform.forward * Time.deltaTime * 1f;
            }

            if (Input.GetKey(KeyCode.A))
            {
                movement = -transform.right * Time.deltaTime * 1f;
            }

            if (Input.GetKey(KeyCode.D))
            {
                movement = transform.right * Time.deltaTime * 1f;
            }

            transform.position += movement;
        }
    }
}