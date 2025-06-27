using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIHandler : MonoBehaviour
{
    enum Mode
    {
        Default,
        SinglePlayer,
        TwoPlayer
    }
    public GameObject MainMenu;
    public Level SinglePlayer;
    public Level[] levels;
    static public UIHandler Instance;
    public GameObject ComparisonBoard;
    private Mode mode = Mode.Default;
    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        SinglePlayer.gameObject.SetActive(false);
        foreach (var l in levels)
        {
            l.gameObject.SetActive(false);
        }
        ComparisonBoard.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void StartGame()
    {

        ComparisonBoard.SetActive(false);
        MainMenu.SetActive(false);
        SinglePlayer.gameObject.SetActive(true);
        foreach (var l in levels)
        {
            l.gameObject.SetActive(false);
        }
        SinglePlayer.StartGame();
        mode = Mode.SinglePlayer;
    }
    public void TwoPlayerStartGame()
    {

        ComparisonBoard.SetActive(false);
        MainMenu.SetActive(false);
        SinglePlayer.gameObject.SetActive(false);
        foreach (var l in levels)
        {
            l.gameObject.SetActive(true);
        }
        foreach (var l in levels)
        {
            l.StartGame();
        }
        mode = Mode.TwoPlayer;
    }
    public void EndGame()
    {
        if (mode == Mode.TwoPlayer)
        {
            bool isDone = true;
            foreach(var l in levels)
            {
                if (!l.IsEnding())
                {
                    isDone = false;
                    break;
                }
            }
            if (isDone)
            {
                ComparisonBoard.SetActive(true);
            }
        }
    }
    public void OpenMainMenu()
    {
        mode = Mode.Default;
        MainMenu.SetActive(true);
        ComparisonBoard.SetActive(false);
    }
    
}
