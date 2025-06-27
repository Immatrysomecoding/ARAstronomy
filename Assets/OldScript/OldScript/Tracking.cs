using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tracking : MonoBehaviour
{
    
    public Transform imageTarget;
    private Transform oldParent;
    // Start is called before the first frame update
    void Start()
    {
        oldParent = transform.parent;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ReTrack();
        }
    }
    public void Track()
    {
        StartCoroutine(waitAndHide());
    }
    IEnumerator waitAndHide()
    {
        yield return new WaitForSeconds(2f);
        transform.SetParent(null);
        imageTarget.gameObject.SetActive(false);
    }
    public void ReTrack()
    {
        transform.SetParent(oldParent);
        imageTarget.gameObject.SetActive(true);
    }
}
