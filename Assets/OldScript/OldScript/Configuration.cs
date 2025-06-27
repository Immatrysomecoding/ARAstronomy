using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vuforia;

public class Configuration : MonoBehaviour
{
    public AudioSource BackGroundMusic;
    public AudioSource Sound;
    public Slider BackGroundMusicSlider;
    public Slider SoundSlider;
    public GameObject Menu;
    public GameObject SettingsCanvas;
    public GameObject EndingCanvas;
    public TMP_InputField TryCountField;
    public TMP_InputField NumberOfLevelField;
    public TMP_InputField TimeField;
    public Button SaveBtn;
    public GameObject EndingCanvasLeft;
    public GameObject EndingCanvasRight;
    // Start is called before the first frame update
    void Start()
    {
        LoadParams();
    }
    void LoadParams()
    {
        int numberOfLevel = PlayerPrefs.GetInt("numLevel", 3);
        int timeEachLevel = PlayerPrefs.GetInt("time", 30);
        int tryCount = PlayerPrefs.GetInt("tryCount", 5);
        Level.maxTime = timeEachLevel;
        Level.NumberOfLevels = numberOfLevel;
        PlaceHolder.maxTryCount = tryCount;
        float musicVolume = PlayerPrefs.GetFloat("musicVolume", 1);
        float soundVolume = PlayerPrefs.GetFloat("soundVolume", 0.7f);
        BackGroundMusic.volume = musicVolume;
        Sound.volume = soundVolume;
        BackGroundMusicSlider.value = musicVolume;
        SoundSlider.value = soundVolume;
    }

    // Update is called once per frame
    void Update()
    {
        if (NoValueChange())
        {
            SaveBtn.interactable = false;
        }
        else
        {
            SaveBtn.interactable = true;
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            Menu.SetActive(!Menu.activeSelf);
        }
    }
    private bool NoValueChange()
    {
        int numberOfLevel = 0;
        int timeEachLevel = 0;
        int tryCount = 0;
        Int32.TryParse(NumberOfLevelField.text, out numberOfLevel);
        Int32.TryParse(TimeField.text, out timeEachLevel);
        Int32.TryParse(TryCountField.text, out tryCount);
        if (Level.maxTime != timeEachLevel || 
        Level.NumberOfLevels != numberOfLevel ||
        PlaceHolder.maxTryCount != tryCount)
        {
            return false;
        }
        return true;
    }
    public void OpenSettings()
    {
        SettingsCanvas.SetActive(true);
        Menu.SetActive(false);
        int numberOfLevel = PlayerPrefs.GetInt("numLevel",3);
        int timeEachLevel = PlayerPrefs.GetInt("time", 30);
        int tryCount = PlayerPrefs.GetInt("tryCount", 5);
        float musicVolume = PlayerPrefs.GetFloat("musicVolume", 1);
        float soundVolume = PlayerPrefs.GetFloat("soundVolume", 0.7f);
        BackGroundMusic.volume = musicVolume;
        Sound.volume = soundVolume;
        BackGroundMusicSlider.value = musicVolume;
        SoundSlider.value = soundVolume;
        TryCountField.text = tryCount.ToString();
        TimeField.text = timeEachLevel.ToString();
        NumberOfLevelField.text = numberOfLevel.ToString();
    }
    public void Save()
    {
        int numberOfLevel = 0;
        int timeEachLevel = 0;
        int tryCount = 0;
        if (!Int32.TryParse(NumberOfLevelField.text, out numberOfLevel))
        {
            AlertError();
            return;
        }
        if (!Int32.TryParse(TimeField.text, out timeEachLevel))
        {
            AlertError();
            return;
        }
        if (!Int32.TryParse(TryCountField.text, out tryCount))
        {
            AlertError();
            return;
        }

        PlayerPrefs.SetInt("numLevel", numberOfLevel);
        PlayerPrefs.SetInt("time", timeEachLevel);
        PlayerPrefs.SetInt("tryCount", tryCount);
  
        Level.maxTime = timeEachLevel;
        Level.NumberOfLevels = numberOfLevel;
        PlaceHolder.maxTryCount = tryCount;
        
    }

    void AlertError()
    {
        print("error");
    }
    public void OpenMenu() {
        SettingsCanvas.SetActive(false);
        Menu.SetActive(true);
        EndingCanvas.SetActive(false);
        EndingCanvasLeft.SetActive(false);
        EndingCanvasRight.SetActive(false);
        UIHandler.Instance.OpenMainMenu();
    }
    public void MusicVolumeChange(float val)
    {
        BackGroundMusic.volume = val;
        PlayerPrefs.SetFloat("musicVolume", val);
    }
    public void SoundVolumeChange(float val)
    {
        Sound.volume = val;
        PlayerPrefs.SetFloat("soundVolume", val);
    }
    public void Close()
    {
        OpenMenu();
    }
}
