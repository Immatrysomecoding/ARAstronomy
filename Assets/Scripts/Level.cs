using Assets.Scripts.Model;
using Assets.Scripts.State;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Level : MonoBehaviour
{
    public List<AstronomicalObject> allPlanets;
    public AstronomicalObject M1;
    public AstronomicalObject m;
    public AstronomicalObject M2;
    public float maxTime = 10;
    public float checkAnsTime = 3;
    private bool started = false;
    private bool ended = false;
    private IStateLevel state = new PreLevelState(null);
    public PlaceHolder PlaceHolder;
    public LayerMask planeLayer;
    public GameObject UI;
    // Start is called before the first frame update
    void Start()
    {
       
    }
    void ChangeState(IStateLevel state)
    {
        this.state = state;
    }

    // Update is called once per frame
    void Update()
    {
        if (started)
        {
            maxTime -= Time.deltaTime;
            if (maxTime < 0)
            {
                ended = true;
            }
            if (ended)
            {
                //checkAnsTime -= Time.De
            }
        }
    }
    public void StartLevel()
    {
        started = true;
    }
    public void RandomizePosition()
    {
        M1 = allPlanets[UnityEngine.Random.Range(0, allPlanets.Count)];
        M2 = allPlanets[UnityEngine.Random.Range(0, allPlanets.Count)];
        bool left = Random.Range(0, 2) == 0 ? true : false;
        M1 = Clone(M1, left);
        M1.transform.parent = PlaceHolder.transform.parent.parent;
        M1.setHeight();

        M2 = Clone(M2, !left);
        M2.transform.parent = PlaceHolder.transform.parent.parent;
        M2.setHeight();
        M2.gameObject.SetActive(false);

        PlaceHolder.transform.position = M2.transform.position;
        PlaceHolder.transform.localPosition = new Vector3(PlaceHolder.transform.localPosition.x, 0, PlaceHolder.transform.localPosition.z);
    }
    AstronomicalObject Clone(AstronomicalObject origin, bool left = true)
    {
        float spawnY = Screen.height/2;
        float spawnX = Random.Range(0 + Screen.width/8, Screen.width - Screen.width/2 - Screen.width/4);
        if (!left)
        {
            spawnX = Random.Range(0 + Screen.width / 2 + Screen.width/4, Screen.width - Screen.width/8);
        }
        var spawnScreen = new Vector2(spawnX, spawnY);
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(spawnScreen);

        if (Physics.Raycast(ray, out hit, 100, planeLayer))
        {
            Transform objectHit = hit.transform;
            return GameObject.Instantiate(origin, hit.point, Quaternion.Euler(-90, 0, 0));
            // Do something with the object that was hit by the raycast.
        }
        return null;
    }
    public void FindMeanAndSetRocket()
    {
        var d = Vector3.Distance(M1.transform.position, M2.transform.position);
       
        var d2 = d /(1 + Math.Sqrt(M1.Mass / M2.Mass));
        

        var direction = (M1.transform.position - M2.transform.position).normalized;
        var answer = M2.transform.position + direction * ((float)d2);
        m.transform.position = answer;
        m.setHeight();
        var d1 = d2 * Math.Sqrt(M1.Mass / M2.Mass);
        
        PlaceHolder.M1 = M1;
        PlaceHolder.SetAttribues(M2.Mass);
    }
    public void Play()
    {
        if (M1)
            Destroy(M1.gameObject);
        if (M2)
            Destroy(M2.gameObject);
        RandomizePosition();
        FindMeanAndSetRocket();
        UI.SetActive(false);
        //var forceValue1 = M1.GetAttractiveForce(m);
        //var forceValue2 = M2.GetAttractiveForce(m);
        //print((float)forceValue1);
        //print((float)forceValue2);
        //print("F1 > F2:" + (forceValue1 > forceValue2));
        //forceValue1 *= 10e-36;
        //var direction = (M1.transform.position - m.transform.position).normalized;
        //// m.GetComponent<Rigidbody>().AddForce(direction * (float)forceValue);
       
    }
    public void End()
    {
        
        UI.SetActive(true);
    }
}
