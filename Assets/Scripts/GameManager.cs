using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        { 
            JSONSerialization.Save("TestSave2");
        }   
        if (Input.GetKeyDown(KeyCode.L))
        {
            JSONSerialization.Load("TestSave2");
        }
    }
}
