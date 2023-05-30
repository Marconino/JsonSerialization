using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JSONManager : MonoBehaviour
{
    static JSONManager instance;

    [SerializeField] List<MonoBehaviour> scripts;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void GetJSONObjects()
    {
        if (instance != null)
        {
            foreach (MonoBehaviour script in instance.scripts)
            {
                CustomJSON.GetJSONObjects(script);
            }
        }

    }

    public static void Save(string _filename)
    {
        if (instance != null)
        {
            CustomJSON.Save(_filename);
        }
    }

    public static void Load(string _filename)
    {
        if (instance != null)
        {
            CustomJSON.Load(_filename, instance.scripts);
        }
    }
}
