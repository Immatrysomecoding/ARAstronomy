using Assets.Scripts.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlaceHolder : MonoBehaviour
{
    public RocketController rocketController;
    public ParticleSystem Circle;
    public AstronomicalObject M1;
    public int count = 5;
    static public int maxTryCount = 5;
    private double MassAnswer;
    private Transform M2;
    public TextMeshProUGUI TryCount;
    private AstronomicalObject CurrentAns;
    public Slider ScoreSlider;
    private AstronomicalObject TempObject;
    private Vector3 oldScale;
    private ParticleSystem.MinMaxGradient oldColor;
    public ParticleSystem.MinMaxGradient changeColor;
    public Image[] TryCountLines;
    public void SetAttribues(double massAnswer)
    {
        this.MassAnswer = massAnswer;
        count = maxTryCount;
        rocketController.ResetGame();
        oldColor = Circle.main.startColor;
    }
    // Start is called before the first frame update
    void Start()
    {
        oldScale = Circle.transform.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        cooldown -= Time.deltaTime;
        CheckM2Out();
        TryCount.SetText(count.ToString());
        ScoreSlider.value = 1.0f * count / maxTryCount;
        for(int i = 0; i < count; i++)
        {
            TryCountLines[i].enabled = true;
        }
        for(int i = count; i < TryCountLines.Length; i++)
        {
            TryCountLines[i].enabled = false;
        }
    
        if (TempObject != null && TempObject.GetComponent<Renderer>().enabled && Vector3.Distance(TempObject.transform.position, transform.position) < 10)
        {

            Circle.transform.localScale = TempObject.scaleFactor * Vector3.one;
            var main = Circle.main;
            main.startColor = changeColor;
        }
        else
        {
            Circle.transform.localScale = oldScale;
            var main = Circle.main;
            main.startColor = oldColor;
        }
    }
    void CheckM2Out()
    {
        if (M2!=null && M2.GetComponent<Renderer>().enabled == false)
        {
            this.rocketController.Cancel();
        }
        
    }
    public void TimeUp()
    {
        if (CurrentAns == null)
        {
            this.rocketController.FlyTo(M1.transform, true);
        }
        else
        {
            var mass2 = CurrentAns.Mass;
            if (MassAnswer > mass2)
            {
                this.rocketController.FlyTo(M1.transform, true);
            }
            else if (MassAnswer < mass2)
            {
                this.rocketController.FlyTo(CurrentAns.transform, true);
            }

        }
        
    }
    IEnumerator coroutine = null;
    private float cooldown = 0.25f;
    private void OnTriggerEnter(Collider other)
    {
        if (rocketController.level.IsAnimationTime())
            return;
        var aObj = other.GetComponent<AstronomicalObject>();
        if (aObj && cooldown < 0)
        {
            if (coroutine == null)
            {
                TempObject = aObj;
                coroutine = Check(aObj);
                StartCoroutine(coroutine);
            }
            
        }
    }
    
    IEnumerator Check(AstronomicalObject aObj)
    {
        var pos = aObj.transform.position;
        yield return null;
        if (Vector3.Distance(aObj.transform.position, pos) < 3.5)
        {
            var mass2 = aObj.Mass;
            M2 = aObj.transform;
            CurrentAns = aObj;
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

        cooldown = 0.25f;
        coroutine = null;
    }
    private void OnTriggerExit(Collider other)
    {
        if (rocketController.level.IsAnimationTime())
            return;
        if (cooldown <= 0)
        {
            CurrentAns = null;
            this.rocketController.Cancel();
            cooldown = 1;
            TempObject = null;
            Circle.transform.localScale = oldScale;
        }
        

    }
}
