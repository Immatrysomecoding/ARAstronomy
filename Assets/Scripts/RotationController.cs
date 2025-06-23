using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationController : MonoBehaviour
{
    public LayerMask planeLayer;
    public Transform Planet;
    public float distance = 20;
    private Transform odlParent;
    // Start is called before the first frame update
    void Start()
    {
        odlParent = transform.parent;
    }

    // Update is called once per frame
    void Update()
    {
        if (Planet == null)
        {
            Destroy(this.gameObject);
        }
        if (Planet && Planet.GetComponent<Renderer>().enabled == true)
        {
            transform.SetParent(null);
            for (var i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(true);
            }
        }
        else
        {
            transform.SetParent(odlParent);
            for(var i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }
        UpdatePostion();
        transform.LookAt(Camera.main.transform);
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x - 180,0, transform.localEulerAngles.z);

    }
    void UpdatePostion()
    {
        if (Planet != null)
        {
            // Vector2 _2dPointOfPlanet = Camera.main.WorldToScreenPoint(Planet.transform.position);
            //  transform.position = getPointInPlane(_2dPointOfPlanet);
            var pos = Planet.transform.position;
            transform.position = pos;
        }
        
    }
    Vector3 getPointInPlane(Vector2 screenPoint)
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(screenPoint);

        if (Physics.Raycast(ray, out hit, 100, planeLayer))
        {
          
            return hit.point;
        }
        return Vector3.zero;
    }
}
