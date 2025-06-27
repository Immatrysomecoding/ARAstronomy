using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeftScreenLevel : Level
{
    protected override float GetSpawnX(bool leftPart = true)
    {
        float spawnX = 0;
        float padding = Screen.width / 16;
        if (leftPart)
        {
            spawnX = 1.7f * padding;
        }
        else
            spawnX = Screen.width / 2 - padding;
        return spawnX;
    }
    override protected Vector3 GetCenterPoint()
    {
        var spawnScreen = new Vector2(Screen.width / 4, Screen.height / 2);
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(spawnScreen);

        if (Physics.Raycast(ray, out hit, 100, planeLayer))
        {
            return hit.point;
        }
        return Vector3.zero;
    }
}
