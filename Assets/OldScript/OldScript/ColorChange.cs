using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorChange : MonoBehaviour
{
    private Material mat;
    public Color color1;
    public Color color2;
    private bool mutex = true;
    public float interval = 1;
    private float timer = 1;
    // Start is called before the first frame update
    void Start()
    {
        mat = GetComponent<Renderer>().material;
        timer = interval;
    }

    // Update is called once per frame
    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            mutex = !mutex;
            timer = interval;
            if (mutex)
            {
                mat.color = color1;
            }
            else
            {
                mat.color = color2;
            }
        }
    }
}
