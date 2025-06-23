using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResultTextUICopy : MonoBehaviour
{
    [Header("Player 1")]
    [Header("UIs on comparison board")]
    public Text ScoreResultPlayer1;
    public Text TimeResultPlayer1;
    public Text ScoreOnComparisionPlayer1;
    public Text TimeOnComparisionPlayer1;

    [Header("Player 2")]
    [Header("UIs on comparison board")]
    public Text ScoreResultPlayer2;
    public Text TimeResultPlayer2;
    public Text ScoreOnComparisionPlayer2;
    public Text TimeOnComparisionPlayer2;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdateUI();
    }
    void UpdateUI()
    {
        ScoreOnComparisionPlayer1.text = ScoreResultPlayer1.text;
        ScoreOnComparisionPlayer2.text = ScoreResultPlayer2.text;
        TimeOnComparisionPlayer1.text = TimeResultPlayer1.text;
        TimeOnComparisionPlayer2.text = TimeResultPlayer2.text;
    }
}
