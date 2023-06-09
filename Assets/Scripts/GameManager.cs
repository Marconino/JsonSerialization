using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] MonoBehaviour script;
    [SerializeField] GameObject test;
    [SerializeField] Vector3 pos;
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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GameObject test2 = new GameObject();
            test2.transform.position = new Vector3(100, 200, 300);
            FieldInfo field = script.GetType().GetField("test21", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field.SetValue(script, test2);
        }
    }
}
