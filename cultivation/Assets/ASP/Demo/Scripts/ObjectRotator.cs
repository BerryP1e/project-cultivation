using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASP.Demo
{
    public class ObjectRotator : MonoBehaviour
    {

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(new Vector3(0, 1, 0), 60 * Time.deltaTime * Mathf.Sin(Time.time));
        }
    }
}
