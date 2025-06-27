using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateLocal : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
    
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(transform.forward, 0.3f, Space.World);
    }
}
