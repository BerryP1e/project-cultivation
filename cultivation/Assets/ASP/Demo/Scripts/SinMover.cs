using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASP.Demo
{
    public class SinMover : MonoBehaviour
    {
        public Vector3 Movement;

        private Vector3 m_originPosition;

        // Start is called before the first frame update
        void Start()
        {
            m_originPosition = transform.position;
        }

        // Update is called once per frame
        void Update()
        {
            transform.position = m_originPosition + Movement * Mathf.Sin(Time.time);
        }
    }
}