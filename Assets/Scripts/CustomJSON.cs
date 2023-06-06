using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using Unity.VisualScripting;
using UnityEngine;
using System.ComponentModel;
using static UnityEditor.MaterialProperty;
using System.Globalization;
using UnityEditor;

[AttributeUsage(AttributeTargets.Field)]
public class JSONRead : Attribute
{

}

public class JSONObject
{
    public FieldInfo fieldInfo;
    public Type type;
    public object obj;

    public JSONObject(FieldInfo _fieldInfo, Type _type, object _obj)
    {
        fieldInfo = _fieldInfo;
        type = _type;
        obj = _obj;
    }
}

public static class CustomJSON
{
    static Dictionary<object, List<JSONObject>> jsonObjects = new Dictionary<object, List<JSONObject>>();
    //public static List<JSONObject> listObjects = new List<JSONObject>();

    public static void GetJSONObjects(object _script)
    {
        Type scriptType = _script.GetType();
        FieldInfo[] fields = scriptType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo field in fields)
        {
            if (Attribute.IsDefined(field, typeof(JSONRead)))
            {
                JSONObject jsonObject = new JSONObject(field, field.FieldType, field.GetValue(_script).ConvertTo<object>());

                if (jsonObjects.ContainsKey(_script))
                {
                    jsonObjects[_script].Add(jsonObject);
                }
                else
                {
                    List<JSONObject> list = new List<JSONObject>() { jsonObject };
                    jsonObjects.Add(_script, list);
                }
            }
        }
    }

    public static void Save(string _filename)
    {
        using (StreamWriter stream = new StreamWriter(Application.streamingAssetsPath + "/Saves/" + _filename + ".txt"))
        {
            string json = "{\n";

            int index = 0;
            foreach (object script in jsonObjects.Keys)
            {
                MonoBehaviour monoBehaviour = script as MonoBehaviour;

                if (index == 0)
                {
                    json += "\"Script : " + monoBehaviour.name + "\" : \n{\n";
                }
                else
                {
                    json += ",\n\"Script : " + monoBehaviour.name + "\" : \n{\n";
                }

                foreach (JSONObject jsonObject in jsonObjects[script])
                {
                    json += Parse(jsonObject);

                    RemoveLast("\n", ref json);

                    json += ",\n";
                }
                RemoveLast(",", ref json);

                json += "\n}";
                index++;
            }


            json += "\n}";
            stream.Write(json);
        }
    }

    public static void Load(string _filename, List<MonoBehaviour> _scripts)
    {
        using (StreamReader stream = new StreamReader(Application.streamingAssetsPath + "/Saves/" + _filename + ".txt"))
        {
            string currLine = stream.ReadLine();
            currLine = stream.ReadLine(); //For first {

            while (!stream.EndOfStream)
            {
                if (currLine.Contains("Script"))
                {
                    string scriptName = currLine;
                    Filter(ref scriptName);
                    MonoBehaviour script = _scripts.Find(n => n.name == scriptName);
                    Type scriptType = script.GetType();

                    do
                    {
                        currLine = stream.ReadLine();

                        if (currLine.Contains(":"))
                        {
                            string[] parts = currLine.Split(':');

                            for (int i = 0; i < parts.Length; i++)
                            {
                                Filter(ref parts[i]);
                            }

                            FieldInfo field = scriptType.GetField(parts[0], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            object value = null;
                            if (field.FieldType.Name.Contains("Vector"))
                            {
                                value = VectorFromString(parts[1], field.FieldType);
                            }
                            else if(field.FieldType.Name.Contains("List") || field.FieldType.IsArray)
                            {
                                value = ArrayFromString(parts[1], field.FieldType);
                            }
                            else if (field.FieldType.IsEnum)
                            {
                                value = Enum.Parse(field.FieldType, parts[1]);
                            }
                            else
                            {
                                value = Convert.ChangeType(parts[1], field.FieldType, CultureInfo.InvariantCulture); //Dernier paramètre pour que la virgule soit considéré comme un point
                            }

                            field.SetValue(script, value);
                        }

                    } while (!currLine.Contains("}"));
                    currLine = stream.ReadLine();
                }
            }

        }
    }

    static void Filter(ref string _string)
    {
        char[] filters = new char[] { '\n', '\"', ':', ' ', '(', ')' };

        if (!_string.Contains("Script"))
        {
            foreach (char c in filters)
            {
                _string = _string.Replace(c.ToString(), string.Empty);
            }

            if (_string[_string.Length - 1] == ',')
                RemoveLast(",", ref _string);
        }
        else
        {
            _string = _string.Substring(_string.IndexOf(':') + 2);
            RemoveLast("\"", ref _string);
        }


    }

    static object ArrayFromString(string _stringValue, Type _arrayType)
    {
        List<object> array = new List<object>();

        if (_stringValue != "[]")
        {
            _stringValue = _stringValue.Trim('[', ']');

            Type elementType = _arrayType.IsArray ? _arrayType.GetElementType() : _arrayType.GetProperty("Item").PropertyType; //Récupère le type des éléments de la liste OU du tableau
            string[] parts = _stringValue.Split(',');

            if (elementType.Name.Contains("Vector"))
            {
                array = StringToVectorFormat(parts, elementType);
            }
            else
            {
                foreach (string part in parts)
                {
                    array.Add(Convert.ChangeType(part, elementType, CultureInfo.InvariantCulture));
                }
            }
        }

        return array.ConvertTo(_arrayType);
    }

    static List<object> StringToVectorFormat(string[] _stringValue, Type _vectorType)
    {
        int vecDimension = _vectorType == typeof(Vector2) ? 2 : _vectorType == typeof(Vector3) ? 3 : _vectorType == typeof(Vector4) ? 4 : 0;
        string currentVector = string.Empty;
        List<object> arrayVector = new List<object>();

        for(int i = 0; i < _stringValue.Length + 1; i++)
        {
            if (i != 0 && i % vecDimension == 0)
            {
                RemoveLast(",", ref currentVector);
                arrayVector.Add(VectorFromString(currentVector, _vectorType));

                if (i < _stringValue.Length)
                currentVector = _stringValue[i] + ",";
            }
            else
            {
                currentVector += _stringValue[i] + ",";
            }
        }
        return arrayVector;
    }

    static object VectorFromString(string _stringValue, Type _vectorType)
    {
        string[] parts = _stringValue.Split(',');

        bool hasFloat = _vectorType == typeof(Vector2) || _vectorType == typeof(Vector3) || _vectorType == typeof(Vector4);

        float[] values = new float[parts.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float.TryParse(parts[i], hasFloat ? NumberStyles.Float : NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]);
        }

        object vector = Activator.CreateInstance(_vectorType);

        switch(parts.Length)
        {
            case 2:
                if (hasFloat)
                    vector = new Vector2(values[0], values[1]);
                else
                    vector = new Vector2Int((int)values[0], (int)values[1]);
                break;
            case 3:
                if (hasFloat)
                    vector = new Vector3(values[0], values[1], values[2]);
                else
                    vector = new Vector3Int((int)values[0], (int)values[1], (int)values[2]);
                break;
            case 4: vector = new Vector4(values[0], values[1], values[2], values[3]); break;
        }
        
        return vector;
    }

    static string ParsePrimitive(JSONObject _jsonObject)
    {
        string value = _jsonObject.obj.ToString().ToLower();

        if (_jsonObject.type == typeof(float) || _jsonObject.type == typeof(double))
        {
            value = value.Replace(",", ".");
        }

        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null)
            returnStr = value + ", ";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : " + value + "\n";

        return returnStr;
    }

    static string ParseEnum(JSONObject _jsonObject)
    {
        string value = _jsonObject.obj.ToString();

        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null)
            returnStr = "\"" + value + "\", ";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : \"" + value + "\"\n";

        return returnStr;
    }

    static string ParseValueType(JSONObject _jsonObject)
    {
        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null)
            returnStr = "\"" + _jsonObject.obj + "\", ";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : \"" + _jsonObject.obj + "\"\n";

        return returnStr;
    }

    static string ParseClass(JSONObject _jsonObject)
    {
        string value = string.Empty;

        if (_jsonObject.fieldInfo != null)
            value = "\"" + _jsonObject.fieldInfo.Name + "\" : ";

        if (_jsonObject.type.Name.Contains("List"))
        {
            value += "[";
            List<object> list = _jsonObject.obj.ConvertTo<List<object>>();

            if (list != null)
            {
                foreach (object item in list)
                {
                    Type type = item.GetType();

                    JSONObject itemJSONObject = new JSONObject(null, type, item);
                    value += Parse(itemJSONObject);

                }
            }

            RemoveLast(",", ref value);
            value += "]\n";
        }
        else if (_jsonObject.type.Name == "String")
        {
            //string returnStr = string.Empty;

            //returnStr =  _jsonObject.obj + "\n";

            value += "\"" + _jsonObject.obj + "\"\n";
        }
        else if (_jsonObject.type.Name.Contains("GameObject"))
        {
            //value += "{ ";
            //GameObject go = (GameObject)_jsonObject.obj.ConvertTo(typeof(GameObject));
            ////nameGo, component, children, transform, pos

            //object[] gameobjectElements = new object[3];

            //gameobjectElements[0] = go.name;
            //gameobjectElements[1] = go.GetComponents<Component>();

            //List<int> indices = Enumerable.Range(0, go.transform.childCount).ToList();
            //Transform[] children = new Transform[go.transform.childCount];

            //indices.ForEach(index => { children[index] = go.transform.GetChild(index); });

            //gameobjectElements[2] = children;

            //foreach (object item in gameobjectElements)
            //{
            //    Type type = item.GetType();

            //    JSONObject itemJSONObject = new JSONObject(null, type, _jsonObject.scriptTargeted, item);

            //    if (type.Name == "String")
            //        value += "GameObject : " + Parse(itemJSONObject);
            //    else
            //        value += Parse(itemJSONObject);

            //    if (type.Name.Contains("Vector"))
            //        RemoveLast(",", ref value);

            //    RemoveLast("\n", ref value);
            //    value += ", ";
            //}

            //RemoveLast(",", ref value);
            //value += " }\n";
        }
        else
        {
            value += "Component : " + "\"" + _jsonObject.type + "\" { ";

            PropertyInfo[] properties = _jsonObject.type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (PropertyInfo property in properties)
            {
                object propertyValue = property.GetValue(_jsonObject.obj);
                if (propertyValue == null)
                    value += property.Name + " : null, ";
                else
                    value += property.Name + " : " + propertyValue + ", ";

                RemoveLast("\n", ref value);
            }
            RemoveLast(",", ref value);
            value += " }, ";
        }
        return value;
    }

    static string ParseArray(JSONObject _jsonObject)
    {
        string value = string.Empty;

        if (_jsonObject.fieldInfo == null)
            value = " [";
        else
            value = "\"" + _jsonObject.fieldInfo.Name + "\" : [";

        Array array = (Array)_jsonObject.obj.ConvertTo(typeof(Array));

        if (array != null)
        {
            foreach (object item in array)
            {
                Type type = item.GetType();

                JSONObject itemJSONObject = new JSONObject(null, type, item);
                value += Parse(itemJSONObject);
            }
        }

        RemoveLast(",", ref value);
        value += "]\n";
        return value;
    }

    static string Parse(JSONObject _jsonObject)
    {
        string valueParsed = string.Empty;
        if (_jsonObject.type.IsArray)
        {
            valueParsed += ParseArray(_jsonObject);
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

    static void RemoveLast(string charRemoved, ref string _value)
    {
        int count = _value.LastIndexOf(charRemoved);
        _value = count > 0 ? _value.Remove(count, _value.Length - count) : _value;
    }
}