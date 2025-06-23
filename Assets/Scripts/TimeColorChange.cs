using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeColorChange : MonoBehaviour
{
    public TextMeshProUGUI TimeText;
    public Image ClockImage;
    public AudioSource ClockSource;
    static public AudioSource StaticClockSource;
    // Start is called before the first frame update
    void Awake()
    {
        StaticClockSource = ClockSource;
    }

    // Update is called once per frame
    void Update()
    {
    }
    public void UpdateColor(int time)
    {
  
        if (time <= 10)
        {
            if (!StaticClockSource.isPlaying && time > 5)
            {
                print("Play clock");
                StaticClockSource.Play();
            }
            ClockImage.color = Color.red;
            TimeText.color = Color.red;
        }
        else if(time <= 30)
        {
            ClockImage.color = Color.yellow;
            TimeText.color = Color.yellow;
        }
        else
        {
            ClockImage.color = Color.white;
            TimeText.color = Color.white;
        }
    }
}
