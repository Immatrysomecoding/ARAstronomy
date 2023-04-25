using Assets.Scripts.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlaceHolder : MonoBehaviour
{
    public RocketController rocketController;
    public AstronomicalObject M1;
    public int count = 3;
    private double MassAnswer;
    private Transform M2;
    public TextMeshProUGUI TryCount;
    public void SetAttribues(double massAnswer)
    {
        this.MassAnswer = massAnswer;
        count = 3;
        rocketController.ResetGame();
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        cooldown -= Time.deltaTime;
        CheckM2Out();
        TryCount.SetText("Lượt thử: " + count);
    }
    void CheckM2Out()
    {
        if (M2!=null && M2.GetComponent<Renderer>().enabled == false)
        {
            this.rocketController.Cancel();
        }
        
    }
    IEnumerator coroutine = null;
    private float cooldown = 0.5f;
    private void OnTriggerEnter(Collider other)
    {
        var aObj = other.GetComponent<AstronomicalObject>();
        if (aObj && cooldown < 0)
        {
            if (coroutine == null)
            {
                coroutine = Check(aObj);
                StartCoroutine(coroutine);
            }
            
        }
    }
    
    IEnumerator Check(AstronomicalObject aObj)
    {
        var pos = aObj.transform.position;
        yield return null;
        print(Vector3.Distance(aObj.transform.position, pos));
        if (Vector3.Distance(aObj.transform.position, pos) < 3.5)
        {
            var mass2 = aObj.Mass;
            M2 = aObj.transform;
            if (MassAnswer > mass2)
            {
                this.rocketController.FlyTo(M1.transform, --count <= 0);
            }
            else if (MassAnswer < mass2)
            {
                this.rocketController.FlyTo(aObj.transform, --count <= 0);
            }
            else
            {
                this.rocketController.NotifySuccess();
            }
        }

        cooldown = 0.5f;
        coroutine = null;
    }
    private void OnTriggerExit(Collider other)
    {
        if (cooldown <= 0)
        {
            this.rocketController.Cancel();
            cooldown = 1;
        }
        

    }
}
