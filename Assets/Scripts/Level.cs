using Assets.Scripts.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Level : MonoBehaviour
{
    [Header("List of Planets")]
    public List<AstronomicalObject> allPlanets;
    //Hide
    [HideInInspector]
    public AstronomicalObject M1;
    public AstronomicalObject m;
    [HideInInspector]
    public AstronomicalObject M2;
    public ParticleSystem WinEffect;
    public TimeColorChange timeColorChange;
    [Header("Config Params")]
    static public int NumberOfLevels = 3;
    static public float maxTime = 30;
    public PlaceHolder PlaceHolder;
    public LayerMask planeLayer;

    [Header("UI Components")]
    public TextMeshProUGUI TimeCounterText;
    public TextMeshProUGUI ScoreText;
    public TextMeshProUGUI LevelText;
    public GameObject UIComponents;
    public GameObject EndGameUI;
    public Text TotalScore;
    public Text TotalTime;

    //Private
    private bool playing = false;
    private int levelCounter = 0;
    private int score = 0;
    private float timeCounter;
    private float totalTime = 0;
    private bool animationTime = false;
    // Start is called before the first frame update
    void Awake()
    {
        m.gameObject.SetActive(false);
        PlaceHolder.gameObject.SetActive(false);
        EndGameUI.SetActive(false);
    }
    public bool IsEnding() { return levelCounter > NumberOfLevels || timeCounter <= 0; }
    public bool IsPlaying() { return playing; }
    public bool IsAnimationTime() { return animationTime; }
    // Update is called once per frame
    void Update()
    {
        if (playing)
        {
            timeCounter -= Time.deltaTime;
            if (timeCounter <= 0)
            {
                TimeCounterText.text =  "00:00";
                
            }
            else
            {

                timeColorChange.UpdateColor((int)timeCounter);
                TimeSpan time = TimeSpan.FromSeconds(timeCounter);
                string minute = time.Minutes.ToString();
                if (time.Minutes < 10)
                {
                    minute = "0" + time.Minutes;
                }
                string second = time.Seconds.ToString();
                if (time.Seconds < 10)
                {
                    second = "0" + time.Seconds;
                }
                TimeCounterText.text = minute + ":" + second; 
            }
            
            if (timeCounter < 0)
            {
                EndGame();
                PlaceHolder.TimeUp();
               
            }
            
        }
    }
    public void RandomizePosition()
    {
        var center = GetCenterPoint();
        var id1 = UnityEngine.Random.Range(0, allPlanets.Count);
        M1 = allPlanets[id1];
       // M1 = allPlanets[5];
        if (id1 < 4)
        {
            M2 = allPlanets[UnityEngine.Random.Range(0, 4)];
        }
        else
        {
            M2 = allPlanets[UnityEngine.Random.Range(4, allPlanets.Count)];
        }
        
        bool left = Random.Range(0, 2) == 0 ? true : false;
        M1 = Clone(M1, left);
        M1.transform.parent = PlaceHolder.transform.parent.parent.parent;
        M1.setHeight();
        M1.gameObject.SetActive(true);
        


        M2 = Clone(M2, !left);
        M2.transform.parent = PlaceHolder.transform.parent.parent.parent;
        M2.setHeight();
        M2.gameObject.SetActive(false);

        PlaceHolder.transform.position = M2.transform.position;
        PlaceHolder.transform.localPosition = new Vector3(PlaceHolder.transform.localPosition.x, 0, PlaceHolder.transform.localPosition.z);
        PlaceHolder.gameObject.SetActive(true);
    }
    virtual protected float GetSpawnX(bool leftPart = true)
    {
        float spawnX = 0;
        float padding = Screen.width / 6;
        if (leftPart)
        {
            spawnX = Random.Range(0 + padding, Screen.width / 2 - padding);
        }
        else
            spawnX = Random.Range(Screen.width / 2 + padding, Screen.width - padding);
        print(spawnX);
        return spawnX;
    }
     AstronomicalObject Clone(AstronomicalObject origin, bool leftPart = true)
    {

        float spawnY = Screen.height/2;
        float spawnX = GetSpawnX(leftPart);
        float padding = Screen.width / 12;
        
        var spawnScreen = new Vector2(spawnX, spawnY);
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(spawnScreen);

        if (Physics.Raycast(ray, out hit, 100, planeLayer))
        {
            Transform objectHit = hit.transform;
            if (origin.transform.name.Contains("07_saturn"))
                return GameObject.Instantiate(origin, hit.point, Quaternion.Euler(210, 0, 0));
            return GameObject.Instantiate(origin, hit.point, Quaternion.Euler(180, 0, 0));
            // Do something with the object that was hit by the raycast.
        }
        return null;
    }
    public void FindMeanAndSetRocket()
    {
        var center = GetCenterPoint();
        var d = Vector3.Distance(M1.transform.position, M2.transform.position);
        var d2 = d /(1 + Math.Sqrt(M1.Mass / M2.Mass));
        var direction = (M1.transform.position - M2.transform.position).normalized;
        var answer = M2.transform.position + direction * ((float)d2);
        m.transform.position = answer;
        m.setHeight();
        m.gameObject.SetActive(true);

        var d1 = d2 * Math.Sqrt(M1.Mass / M2.Mass);
        PlaceHolder.M1 = M1;
        PlaceHolder.SetAttribues(M2.Mass);
    }
    public void Play()
    {
      
        RandomizePosition();
        FindMeanAndSetRocket();
        StartCoroutine(SetPositionBeforePlaying(0.5f));

    }
    IEnumerator SetPositionBeforePlaying(float time)
    {
        animationTime = true;
        var center = GetCenterPoint();
        var M1Pos = M1.transform.position;
        var placeHolderPos = PlaceHolder.transform.position;
        var mPos = m.transform.position;
        M1.transform.position = center;
        PlaceHolder.transform.position = center;
        m.transform.position = center;
        for (float t = 0f; t <= 1; t += Time.deltaTime / time)
        {
            M1.transform.position = Vector3.Lerp(center, M1Pos, t); ;
            PlaceHolder.transform.position = Vector3.Lerp(center, placeHolderPos, t); ;
            m.transform.position = Vector3.Lerp(center, mPos, t); ;

            yield return null;
        }
        M1.transform.position = M1Pos;
        PlaceHolder.transform.position = placeHolderPos;
        m.transform.position = mPos;

        animationTime = false;
        playing = true;
       // timeCounter = maxTime;

    }
    virtual protected Vector3 GetCenterPoint()
    {
        var spawnScreen = new Vector2(Screen.width / 2, Screen.height / 2);
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(spawnScreen);

        if (Physics.Raycast(ray, out hit, 100, planeLayer))
        {
            return hit.point;
        }
        return Vector3.zero;
    }
   
    public void StartGame()
    {
        EndGameUI.SetActive(false);
        UIComponents.SetActive(true);
        levelCounter = 1;
        score = 0;
        totalTime = 0;
        timeCounter = maxTime;
        UpdateTextAfterEachLevel();
        Play();
    }
    public void EndGame()
    {
        playing = false;
        PlaceHolder.gameObject.SetActive(false);
        StartCoroutine(EndGameCoroutine());
    }
    IEnumerator EndGameCoroutine()
    {
        yield return new WaitForSeconds(0.0f);
        totalTime = maxTime-timeCounter;
        EndGameUI.SetActive(true);
        UIComponents.SetActive(false);
        //   TotalScore.text = score + "/" + NumberOfLevels;

        //  TotalTime.text = totalTime + " giây";
        TotalScore.text = score.ToString();
        TimeSpan time = TimeSpan.FromSeconds(timeCounter);
        TotalTime.text = time.Minutes + ":" + time.Seconds;
        UIHandler.Instance.EndGame();
    }
    private void UpdateTextAfterEachLevel()
    {
        if (LevelText != null)
            LevelText.text = levelCounter + "/" + NumberOfLevels;
        if (ScoreText != null) 
            ScoreText.text = score.ToString();
    }
    public void EndLevel(bool isWin)
    {
        StartCoroutine(EndLevelCoroutine(isWin));
    }
    IEnumerator EndLevelCoroutine(bool isWin)
    {
        animationTime = true;
        if (isWin)
        {
            score++;
            WinEffect.gameObject.transform.position = GetCenterPoint();
            WinEffect.Play();
        }
        


        if (ScoreText != null) 
            ScoreText.text = score.ToString();
        //playing = false;
       // totalTime += (int)(maxTime - timeCounter);
        
        yield return new WaitForSeconds(2.0f);

        
        StartCoroutine(SetPositionAfterEnding(0.5f));
        
    }
    IEnumerator SetPositionAfterEnding(float time)
    {
        animationTime = true;
        var center = GetCenterPoint();
        var M1Pos = M1.transform.position;
        var placeHolderPos = PlaceHolder.transform.position;
        var mPos = m.transform.position;
        for (float t = 0f; t <= 1; t += Time.deltaTime / time)
        {
            M1.transform.position = Vector3.Lerp(M1Pos, center, t); ;
            PlaceHolder.transform.position = Vector3.Lerp(placeHolderPos, center, t); ;
            m.transform.position = Vector3.Lerp(mPos, center, t); ;

            yield return null;
        }
        animationTime = false;
        Destroy(M1.gameObject);
        Destroy(M2.gameObject);

        m.gameObject.SetActive(false);
        PlaceHolder.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        levelCounter++;
        if (levelCounter <= NumberOfLevels && timeCounter > 0)
        {
            Play();
            UpdateTextAfterEachLevel();
        }
        else
        {
            EndGame();
        }
    }
    

}
