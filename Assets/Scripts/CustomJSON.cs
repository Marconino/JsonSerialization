using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class JSONRead : Attribute
{

}

public class JSONObject
{
    public FieldInfo fieldInfo;
    public Type type;
    public object scriptTargeted;
    public object obj;

    public JSONObject(FieldInfo _fieldInfo, Type _type, object _scriptTargeted, object _obj)
    {
        fieldInfo = _fieldInfo;
        type = _type;
        scriptTargeted = _scriptTargeted;
        obj = _obj;
    }
}

public static class CustomJSON
{
    public static List<JSONObject> listObjects = new List<JSONObject>();

    public static void GetJSONObjects(object _script)
    {
        Type testType = _script.GetType();
        FieldInfo[] fields = testType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo field in fields)
        {
            if (Attribute.IsDefined(field, typeof(JSONRead)))
            {         
                JSONObject jsonObject = new JSONObject(field, field.FieldType, _script, field.GetValue(_script).ConvertTo<object>());
                listObjects.Add(jsonObject);

                // Debug.Log(field.FieldType + " IsPrimitive : " + jsonObject.type.IsPrimitive);

                //JSONRead jsonRead = (JSONRead)Attribute.GetCustomAttribute(field, typeof(JSONRead));
                //jsonRead.AddObjectToSaveable(field);
            }
        }
    }

    public static void Save(string _filename)
    {
        using (StreamWriter stream = new StreamWriter(Application.streamingAssetsPath + "/Saves/" + _filename + ".txt"))
        {
            string json = "{\n\n";
            bool endOfPrimitive = false;

            foreach (JSONObject jsonObject in listObjects)
            {
                json += Parse(jsonObject);
            }
            json += "\n}";

            stream.Write(json);
        }
    }

    //“key”:“value”,“key”:“value”
    // Array :
    // "students":[
    //{"firstName":"Tom", "lastName":"Jackson"},
    //{ "firstName":"Linda", "lastName":"Garner"},
    //{ "firstName":"Adam", "lastName":"Cooper"}
    //]

    static string ParsePrimitive(JSONObject _jsonObject)
    {
        string value = _jsonObject.obj.ToString();

        if (_jsonObject.type == typeof(float) || _jsonObject.type == typeof(double))
        {
            value = value.Replace(",", ".");
        }

        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null)
            returnStr =  value + ", ";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : " + value + "\n";

        return returnStr;
    }

    static string ParseEnum(JSONObject _jsonObject)
    {
        string value = _jsonObject.type + "." + _jsonObject.obj;

        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null)
            returnStr = value + ", ";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : " + value + "\n";

        return returnStr;
    }

    static string ParseValueType(JSONObject _jsonObject)
    {
        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null)
            returnStr = _jsonObject.obj + ", ";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : " + _jsonObject.obj + "\n";

        return returnStr;
    }

    static string ParseClass(JSONObject _jsonObject)
    {
        string value = "\"" + _jsonObject.fieldInfo.Name + "\" : [";
        if (_jsonObject.type.Name.Contains("List"))
        {
            List<object> list = _jsonObject.fieldInfo.GetValue(_jsonObject.scriptTargeted).ConvertTo<List<object>>();

            if (list != null)
            {
                foreach (object item in list)
                {
                    Type type = item.GetType();

                    JSONObject itemJSONObject = new JSONObject(null, type, _jsonObject.scriptTargeted, item);
                    value += Parse(itemJSONObject);

                }
            }



        }
        int count = value.LastIndexOf(",");
        if (count > 0)
            value = value.Remove(count, value.Length - count);

        value += "]\n";
        return value;
    }

    static string Parse(JSONObject _jsonObject)
    {
        string valueParsed = string.Empty;
        if (_jsonObject.type.IsArray)
        {
            //Debug.Log(jsonObject.type.Name + " is a array.");
        }
        else if (_jsonObject.type.IsEnum)
        {
            valueParsed += ParseEnum(_jsonObject);
            //Debug.Log(_jsonObject.type.Name + " is an enum.");
        }
        else if (_jsonObject.type.IsInterface)
        {
            //Debug.Log(_jsonObject.type.Name + " is an interface.");
        }
        else if (_jsonObject.type.IsPrimitive)
        {
            valueParsed += ParsePrimitive(_jsonObject);
            //Debug.Log(_jsonObject.type.Name + " is a primitive type.");
        }
        else if (_jsonObject.type.IsValueType)
        {
            valueParsed += ParseValueType(_jsonObject);
            //Debug.Log(_jsonObject.type.Name + " is a value type (struct).");
        }
        else if (_jsonObject.type.IsClass)
        {
            valueParsed += ParseClass(_jsonObject);
            //Debug.Log(_jsonObject.type.Name + " is a class.");
        }
        return valueParsed;
    }
}