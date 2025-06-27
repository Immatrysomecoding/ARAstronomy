using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaceHolderTextController : MonoBehaviour
{
    public LayerMask planeLayer;
    public Transform Planet;
    private Transform odlParent;
    public float distance = 80;
    // Start is called before the first frame update
    void Start()
    {
        odlParent = transform.parent;
    }

    // Update is called once per frame
    void Update()
    {
        if (Planet && Planet.gameObject.activeSelf == true )
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
            for (var i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }
        UpdatePostion();
       // transform.LookAt(Camera.main.transform);
        transform.localEulerAngles = new Vector3(0,0, 0);

    }
    void UpdatePostion()
    {
        Vector2 _2dPointOfPlanet = Camera.main.WorldToScreenPoint(Planet.transform.position);
        _2dPointOfPlanet.y -= distance;
        transform.position = getPointInPlane(_2dPointOfPlanet);
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
