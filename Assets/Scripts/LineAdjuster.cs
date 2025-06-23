using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineAdjuster : MonoBehaviour
{
    LineRenderer line;
    public LayerMask planeLayer;
    // Start is called before the first frame update
    void Start()
    {
        line = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        line.SetPosition(0, RayCastPointOnPlane(new Vector2(Screen.width / 2, 0)));
        line.SetPosition(1, RayCastPointOnPlane(new Vector2(Screen.width / 2, Screen.height)));
    }
    Vector3 RayCastPointOnPlane(Vector2 spawnScreen)
    {
        //var spawnScreen = new Vector2(3 * Screen.width / 4, Screen.height / 2);
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(spawnScreen);

        if (Physics.Raycast(ray, out hit, 100, planeLayer))
        {
            return hit.point;
        }
        return Vector3.zero;
    }
}
