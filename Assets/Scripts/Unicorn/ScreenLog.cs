using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScreenLog : MonoBehaviour
{
    Text logText;
    public static ScreenLog Instance { get; private set; }

    public void Awake()
    {
        if (!Instance)
            Instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        logText = gameObject.GetComponent<Text>();
        logText.text = "Debug with ScreenLog\n";
    }

    private void _log(string msg)
    {
        if (logText) logText.text += msg + "\n";
    }

    public static void Log(string msg)
    {
        if (Instance) Instance._log(msg);
        Debug.Log(msg);
    }
}
