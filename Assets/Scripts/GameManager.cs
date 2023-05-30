using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        JSONManager.GetJSONObjects();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            JSONManager.Save("TestSave2");
        }   
        if (Input.GetKeyDown(KeyCode.L))
        {
            JSONManager.Load("TestSave2");
        }
    }
}
