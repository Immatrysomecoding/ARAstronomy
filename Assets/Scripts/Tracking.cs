using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tracking : MonoBehaviour
{
    public Transform imageTarget;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    
    }
    public void Track()
    {
        StartCoroutine(waitAndHide());
    }
    IEnumerator waitAndHide()
    {
        yield return new WaitForSeconds(2f);
        transform.parent = null;
        imageTarget.gameObject.SetActive(false);
    }
}
