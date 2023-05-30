using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

enum TestSave
{
    OUI,
    NON
}

public class Test : MonoBehaviour
{
    [JSONRead] string testString = "salutlespotes";
    [JSONRead] float floatTest = 50.4f;
    [JSONRead] int intTest2 = 4;
    [JSONRead] double doubleTest3 = 47.874847980;
    [JSONRead] long longTest4 = 44524246346332163;
    [JSONRead] bool boolTest5 = true;
    [JSONRead] Vector2 Vector2Test6 = new Vector2(14.6f,98.4f);
    [JSONRead] Vector2Int test7 = new Vector2Int(90,100);
    [JSONRead] Vector3Int test8 = new Vector3Int(40,96,78);
    [JSONRead] Vector3 test9 = new Vector3(14589.4f, 12.0f, 79.7f);
    [JSONRead] Vector4 test10 = new Vector4(14589.4f, 12.0f, 79.7f, 97889.56f);
    [JSONRead] List<float> test11 = new List<float>();
    [JSONRead] List<int> test12 = new List<int>();
    [JSONRead] List<double> test13 = new List<double>();
    [JSONRead] List<long> test14 = new List<long>();
    [JSONRead] List<bool> test15 = new List<bool>();
    [JSONRead] List<Vector2> test16 = new List<Vector2>();
    [JSONRead] List<Vector2Int> test17 = new List<Vector2Int>();
    [JSONRead] List<Vector3Int> test18 = new List<Vector3Int>();
    [JSONRead] List<Vector3> test19 = new List<Vector3>();
    [JSONRead] List<Vector4> test20 = new List<Vector4>();
    [SerializeField]  GameObject test21;
     List<GameObject> test22;
    [JSONRead] TestSave test23 = TestSave.NON;
    [JSONRead] TestSave[] test24;

    // Start is called before the first frame update
    void Start()
    {
        test11.Add(0.4f);
        test11.Add(484.7f);
        test11.Add(4.798f);

        test12.Add(14);
        test12.Add(15);
        test12.Add(16);
        test12.Add(17);
        test12.Add(18);
        test12.Add(19);

        test13.Add(789.54896489);
        test13.Add(98.123458);

        test14.Add(84846564864645);
        test14.Add(123456789);

        test15.Add(true);
        test15.Add(false);
        test15.Add(false);
        test15.Add(false);
        test15.Add(true);

        test16.Add(new Vector2(14.7f, 978.5f));
        test16.Add(new Vector2(4.1f, 98.147f));

        test24 = new TestSave[3];
        test24[0] = TestSave.OUI;
        test24[1] = TestSave.NON;
        test24[2] = TestSave.OUI;
        
        //CustomJSON.GetJSONObjects(this);

        //CustomJSON.Save("TestSave");
        //CustomJSON.Load("TestSave");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
