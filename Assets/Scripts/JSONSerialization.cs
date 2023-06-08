using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using UnityEngine.Tilemaps;

[AttributeUsage(AttributeTargets.Field)]
public class JSONRead : Attribute { }

public class JSONObject
{
    public FieldInfo fieldInfo;
    public Type type;
    public object obj;
    public string fieldName = string.Empty;

    public JSONObject(FieldInfo _fieldInfo, Type _type, object _obj)
    {
        fieldInfo = _fieldInfo;
        type = _type;
        obj = _obj;
    }

    public JSONObject(string _name, Type _type, object _obj)
    {
        fieldName = _name;
        fieldInfo = null;
        type = _type;
        obj = _obj;
    }
}

public static class JSONSerialization
{
    static Dictionary<MonoBehaviour, List<JSONObject>> jsonObjects = new Dictionary<MonoBehaviour, List<JSONObject>>();
    //public static List<JSONObject> listObjects = new List<JSONObject>();

    public static void GetJSONObjects(MonoBehaviour _script)
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
            foreach (MonoBehaviour script in jsonObjects.Keys)
            {
                if (index == 0)
                {
                    json += "\"Script : " + script.name + "\" : \n{\n";
                }
                else
                {
                    json += ",\n\"Script : " + script.name + "\" : \n{\n";
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

    public static void Load(string _filename)
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

                    MonoBehaviour script = jsonObjects.First(n => n.Key.name == scriptName).Key;
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
                            else if (field.FieldType.Name.Contains("List") || field.FieldType.IsArray)
                            {
                                value = ArrayFromString(parts[1], field.FieldType);
                            }
                            else if (field.FieldType.IsEnum)
                            {
                                value = Enum.Parse(field.FieldType, parts[1]);
                            }
                            else if (field.FieldType == typeof(GameObject))
                            {
                                value = GameObjectFromString(ref currLine, stream);
                                Debug.Log("test");
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
        char[] filters = new char[] { '\n', '\"', ':', ' '};

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

    static object GameObjectFromString(ref string _currLine, StreamReader _stream)
    {
        GameObject go = new GameObject();
        go.hideFlags = HideFlags.HideAndDontSave;

        object returnGo = null;
        int step = 0;
        int componentsCount = 0;

        do
        {
            _currLine = _stream.ReadLine();
            string value = _currLine.Remove(0,_currLine.IndexOf(':'));
            Filter(ref value);

            switch(step)
            {
                case 0: go.name = value; break;
                case 1: go.tag = value; break;
                case 2: go.layer = int.Parse(value); break;
                case 3: go.SetActive(bool.Parse(value)); break;
                case 4: componentsCount = int.Parse(value); break;
            } 
            step++;

        } while (step < 5); //Name, Tag, Layer, IsActive, Nb Components

        step = 0;

        _currLine = _stream.ReadLine();
        string componentName = _currLine;
        Filter(ref componentName);
        componentName += ",UnityEngine"; //Pour le formattage
        Type componentType = Type.GetType(componentName);

        PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach(PropertyInfo property in properties)
        {
            if (!property.Name.Contains("root"))
            {
                _currLine = _stream.ReadLine();
                _currLine = _stream.ReadLine();
                string[] parts = _currLine.Split(':');

                for (int i = 0; i < parts.Length; i++)
                {
                    Filter(ref parts[i]);
                }

                if (property.Name == parts[0])
                {
                    
                    object test = parts[1].ConvertTo(property.PropertyType); 
                    property.SetValue(go, parts[1]);
                }
            }
        }

        //go.AddComponent()

        return null;
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

        for (int i = 0; i < _stringValue.Length + 1; i++)
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

        switch (parts.Length)
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
        if (_jsonObject.fieldInfo == null && _jsonObject.fieldName == string.Empty)
            returnStr = value + ", ";
        else if (_jsonObject.fieldName != string.Empty)
            returnStr = "\"" + _jsonObject.fieldName + "\" : \"" + value + "\",\n";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : " + value + "\n";

        return returnStr;
    }

    static string ParseEnum(JSONObject _jsonObject)
    {
        string value = _jsonObject.obj.ToString();

        string returnStr = string.Empty;
        if (_jsonObject.fieldInfo == null && _jsonObject.fieldName == string.Empty)
            returnStr = "\"" + value + "\", ";
        else if (_jsonObject.fieldName != string.Empty)
            returnStr = "\"" + _jsonObject.fieldName + "\" : \"" + value + "\",\n";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : \"" + value + "\"\n";

        return returnStr;
    }

    static string ParseValueType(JSONObject _jsonObject)
    {
        string returnStr = string.Empty;

        if (_jsonObject.fieldInfo == null && _jsonObject.fieldName == string.Empty)
            returnStr = "\"" + _jsonObject.obj + "\", ";
        else if (_jsonObject.fieldName != string.Empty)
            returnStr = "\"" + _jsonObject.fieldName + "\" : \"" + (_jsonObject.type == typeof(Matrix4x4) ? ParseMatrix4x4(_jsonObject) : _jsonObject.obj) + "\",\n";
        else
            returnStr = "\"" + _jsonObject.fieldInfo.Name + "\" : \"" + _jsonObject.obj + "\"\n";

        return returnStr;
    }

    static string ParseMatrix4x4(JSONObject _jsonObject)
    {
        string value = string.Empty;

        Matrix4x4 matrix4x4 = (Matrix4x4)_jsonObject.obj.ConvertTo(typeof(Matrix4x4));
        Vector4[] rows = new Vector4[4];
        
        for (int i = 0; i < 4; i++)
        {
            rows[i] = matrix4x4.GetRow(i);
            value += rows[i].ToString() + "|";
        }
        RemoveLast("|", ref value);
        return value;
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

                    JSONObject itemJSONObject = new JSONObject(string.Empty, type, item);
                    value += Parse(itemJSONObject);

                }
            }

            RemoveLast(",", ref value);
            value += "]\n";
        }
        else if (_jsonObject.type.Name == "String")
        {
            value += "\"" + _jsonObject.obj + "\"\n";
        }
        else if (_jsonObject.type.Name.Contains("GameObject"))
        {

            /*
            TODO Save GameObject :

            -Components
            -Tag
            -Layer
            -Name
            -IsActive
            -Children

            */
            value += "{";

            GameObject gameObject = (GameObject)_jsonObject.obj.ConvertTo(typeof(GameObject));
            value += "\n\"GO_Name\" : \"" + gameObject.name + "\",\n" + "\"GO_Tag\" : \"" + gameObject.tag + "\",\n" + "\"GO_Layer\" : " + gameObject.layer + ",\n" + "\"GO_IsActive\" : " + gameObject.activeSelf.ToString().ToLower() + ",\n";

            Component[] components = gameObject.GetComponents<Component>();

            value += "\"Components\" : " + components.Length + ",\n";
            foreach (Component component in components)
            {
                Type type = component.GetType();
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                value += "\"" + type + "\" : \n{\n";

                foreach (PropertyInfo property in properties)
                {
                    if (!property.Name.Contains("root"))
                    {
                        object propertyValue = property.GetValue(component);
                        if (propertyValue == null)
                            value += "\"" + property.Name + "\" : null,\n";
                        else
                            value += Parse(new JSONObject(property.Name, property.PropertyType, propertyValue));
                    }
                }
                RemoveLast(",", ref value);
                value += "},\n";
            }
            RemoveLast(",", ref value);
            value += "\n}\n";
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

                JSONObject itemJSONObject = new JSONObject(string.Empty, type, item);
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