using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using System.Globalization;
using UnityEngine.Windows;

[AttributeUsage(AttributeTargets.Field)]
public class JSONRead : Attribute { }

class JSONObject
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
                    if (!jsonObjects[_script].Contains(jsonObject))
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
        UpdateJSONObjects();

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
        UpdateJSONObjects();

        using (StreamReader stream = new StreamReader(Application.streamingAssetsPath + "/Saves/" + _filename + ".txt"))
        {
            string currLine = stream.ReadLine();
            currLine = stream.ReadLine(); //For first {

            Dictionary<string, GameObject> gameObjectsInstancied = new Dictionary<string, GameObject>();

            while (!stream.EndOfStream)
            {
                if (currLine.Contains("Script"))
                {
                    string scriptName = currLine;
                    Filter(ref scriptName);

                    MonoBehaviour script = jsonObjects.FirstOrDefault(n => n.Key.name == scriptName).Key;

                    if (script != null)
                    {
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
                                    GameObject go = (GameObject)field.GetValue(script).ConvertTo(typeof(GameObject));

                                    if (go == null)
                                    {
                                        if (!gameObjectsInstancied.TryGetValue(field.Name, out go))
                                        {
                                            go = new GameObject();
                                            gameObjectsInstancied.Add(field.Name, go);
                                            int indexOldGO = jsonObjects[script].FindIndex(n => n.fieldInfo.Name == field.Name);
                                            jsonObjects[script][indexOldGO].obj = go;
                                        }
                                    }
                                    GameObjectFromString(ref currLine, stream, ref go);
                                    value = go;
                                }
                                else
                                {
                                    value = Convert.ChangeType(parts[1], field.FieldType, CultureInfo.InvariantCulture); //Dernier param�tre pour que la virgule soit consid�r� comme un point
                                }

                                field.SetValue(script, value);
                            }

                        } while (!currLine.Contains("}"));
                        currLine = stream.ReadLine();
                    }
                    else //Passe au prochain script ou termine le fichier texte
                    {
                        do
                        {
                            currLine = stream.ReadLine();
                        }
                        while (currLine != null && !currLine.Contains("Script")); //il peut être null quand le stream est terminé
                    }
                }
                else if (currLine.Contains("}")) //EndOfObject
                {
                    currLine = stream.ReadLine();
                }
            }

        }
    }

    #region SaveMethods
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
    static string ParseClass(JSONObject _jsonObject)
    {
        string value = string.Empty;

        if (_jsonObject.fieldInfo != null)
            value = "\"" + _jsonObject.fieldInfo.Name + "\" : ";
        else if (_jsonObject.fieldName != string.Empty)
            value = "\"" + _jsonObject.fieldName + "\" : ";

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

            if (_jsonObject.fieldInfo != null && _jsonObject.fieldName == string.Empty)
                value += "]\n";
            else
                value += "],\n";
        }
        else if (_jsonObject.type.Name == "String")
        {
            if (value == string.Empty) //c'est un element d'une liste
                value = "\"" + _jsonObject.obj + "\", ";
            else
                value += "\"" + _jsonObject.obj + "\"\n";

        }
        else if (_jsonObject.type.Name.Contains("GameObject"))
        {
            value += "{";

            GameObject gameObject = (GameObject)_jsonObject.obj.ConvertTo(typeof(GameObject));
            value += "\n\"GO_Name\" : \"" + gameObject.name + "\",\n" + "\"GO_Tag\" : \"" + gameObject.tag + "\",\n" + "\"GO_Layer\" : " + gameObject.layer + ",\n" + "\"GO_IsActive\" : " + gameObject.activeSelf.ToString().ToLower() + ",\n";

            Component[] components = gameObject.GetComponents<Component>();
            bool isChild = _jsonObject.fieldName.Contains("Child");

            value += "\"Components\" : " + components.Length + ",\n";
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                Type componentType = component.GetType();
                value += "\"" + componentType + "\" : \n{\n";

                if (componentType.BaseType == typeof(Component) || componentType.Name.Contains("Mesh")) //Est un component d'Unity
                {
                    PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                    foreach (PropertyInfo property in properties)
                    {
                        if (!property.Name.Contains("root"))
                        {
                            object propertyValue = property.GetValue(component);

                            if (propertyValue == null)
                                value += "\"" + property.Name + "\" : null,\n";
                            else if (property.Name.Contains("childCount"))
                            {
                                int childrenCount = (int)propertyValue.ConvertTo(typeof(int));
                                value += "\"" + property.Name + "\"" + ": ";

                                if (childrenCount > 0)
                                {
                                    value += "[";
                                    for (int j = 0; j < childrenCount; j++)
                                    {
                                        value += "\"" + component.transform.GetChild(j).name + "\",";
                                    }
                                    RemoveLast(",", ref value);
                                    value += "],";
                                }
                                else
                                {
                                    value += "0,";
                                }
                                value += "\n";
                            }
                            else if (property.Name.Contains("parent"))
                            {
                                value += "\"" + property.Name + "\"" + ": \"" + component.transform.parent + "\",\n";
                            }
                            else
                                value += Parse(new JSONObject(property.Name, property.PropertyType, propertyValue));
                        }
                    }

                    if (componentType == typeof(MeshRenderer))
                    {
                        MeshRenderer renderer = (MeshRenderer)component;
                        string[] materialsName = renderer.materials.Select(n => n != null ? n.name.Replace(" (Instance)", string.Empty) : null).ToArray();
                        string[] sharedMaterialsName = renderer.materials.Select(n => n != null ? n.name.Replace(" (Instance)", string.Empty) : null).ToArray();
                        value += ParseArray(new JSONObject("materials", typeof(string[]), materialsName));
                        value += ParseArray(new JSONObject("sharedMaterials", typeof(string[]), sharedMaterialsName));
                    }
                }
                else //Est un component personnalisé
                {
                    FieldInfo[] fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    foreach (FieldInfo field in fields)
                    {
                        object propertyValue = field.GetValue(component);

                        if (propertyValue == null)
                            value += "\"" + field.Name + "\" : null,\n";
                        else
                            value += Parse(new JSONObject(field.Name, field.FieldType, propertyValue));
                    }
                }
                RemoveLast(",", ref value);
                value += "\n},\n";

                if (i + 1 == components.Length) //Children
                {
                    int childrenCount = gameObject.transform.childCount;

                    for (int j = 0; j < childrenCount; j++)
                    {
                        value += Parse(new JSONObject("Child " + j, typeof(GameObject), gameObject.transform.GetChild(j).gameObject));
                    }
                }
            }
            RemoveLast(",", ref value);

            if (isChild)
                value += "\n},\n";
            else
                value += "\n}\n";
        }
        else if (_jsonObject.type.Name.Contains("Mesh"))
        {
            value += "\n{\n";
            Mesh mesh = (Mesh)_jsonObject.obj.ConvertTo(typeof(Mesh));
            PropertyInfo[] properties = _jsonObject.type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in properties)
            {
                object propertyValue = property.GetValue(mesh);

                if (propertyValue == null)
                    value += "\"" + property.Name + "\" : null,\n";
                else
                    value += Parse(new JSONObject(property.Name, property.PropertyType, propertyValue));
            }
            RemoveLast(",", ref value);
            value += "\n},\n";
        }
        return value;
    }
    static string ParseArray(JSONObject _jsonObject)
    {
        string value = string.Empty;

        if (_jsonObject.fieldInfo == null)
            value = "\"" + _jsonObject.fieldName + "\" : [";
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
        if (_jsonObject.fieldInfo != null && _jsonObject.fieldName == string.Empty)
            value += "]\n";
        else
            value += "],\n";

        return value;
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
    #endregion

    #region LoadMethods
    static object GameObjectFromString(ref string _currLine, StreamReader _stream, ref GameObject _go)
    {
        int step = 0;
        int componentsCount = 0;

        do
        {
            _currLine = _stream.ReadLine();
            string value = _currLine.Remove(0, _currLine.IndexOf(':'));
            if (step > 1) //Pour ne pas changer le nom ni le tag enregistré
            Filter(ref value);
            else
            {
                value = value.Remove(0, value.IndexOf('\"') + 1);
                int lastIndex = value.LastIndexOf('\"');
                value = value.Remove(lastIndex, value.Length - lastIndex);
            }

            switch (step)
            {
                case 0: _go.name = value; break;
                case 1: _go.tag = value; break;
                case 2: _go.layer = int.Parse(value); break;
                case 3: _go.SetActive(bool.Parse(value)); break;
                case 4: componentsCount = int.Parse(value); break;
            }
            step++;

        } while (step < 5); //Name, Tag, Layer, IsActive, Nb Components

        _currLine = _stream.ReadLine();

        Component[] components = _go.GetComponents<Component>();
        string[] children = null;

        for (int i = 0; i < componentsCount; i++)
        {
            bool isCustomComponent = false;
            string componentName = _currLine;
            Filter(ref componentName);
            componentName += ",UnityEngine"; //Pour le formattage
            Type componentType = Type.GetType(componentName);

            if (componentType == null) //Est un component custom
            {
                isCustomComponent = true;
                componentName = componentName.Remove(componentName.IndexOf(","));
                componentType = Type.GetType(componentName);
            }

            Component currComponent = components.FirstOrDefault(n => n.GetType() == componentType) ?? _go.AddComponent(componentType);

            _currLine = _stream.ReadLine();
            _currLine = _stream.ReadLine(); //Je mets le curseur sur la premi�re propri�t�

            if (isCustomComponent)
            {
                FieldInfo[] fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (FieldInfo field in fields)
                {
                    string[] parts = _currLine.Split(':');

                    for (int j = 0; j < parts.Length; j++)
                    {
                        Filter(ref parts[j]);
                    }

                    if (field.Name == parts[0])
                    {
                        object value = GetObjectFromString(field.FieldType, parts[1], ref _currLine, ref _go, _stream);

                        field.SetValue(currComponent, value);
                    }
                    _currLine = _stream.ReadLine();
                }
            }
            else
            {
                PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (PropertyInfo property in properties)
                {
                    if (!property.Name.Contains("root"))
                    {
                        string[] parts = _currLine.Split(':');
                        Filter(ref parts[0]);

                        if (property.Name.Contains("childCount"))
                        {
                            for (int k = 0; k < parts[1].Length; k++)
                            {
                                parts[1] = parts[1].Replace("\"", string.Empty);
                            }
                            parts[1] = parts[1].Trim(' ', ',');

                            children = (string[])ArrayFromString(parts[1], typeof(string[]));
                            if (children[0].Contains("0"))
                                children = new string[0];
                        }
                        else if (property.Name == parts[0] && property.CanWrite)
                        {
                            Filter(ref parts[1]);

                            object value = GetObjectFromString(property.PropertyType, parts[1], ref _currLine, ref _go, _stream);

                            if (property.Name.Contains("hierarchyCapacity"))
                                property.SetValue(currComponent, _go.transform.hierarchyCapacity);
                            else
                                property.SetValue(currComponent, value);
                        }
                        _currLine = _stream.ReadLine();
                    }
                }
            }
            if (componentType == typeof(MeshRenderer)) //Get materials and sharedMaterials
            {
                MeshRenderer meshRenderer = currComponent as MeshRenderer;

                for (int j = 0; j < 2; j++)
                {
                    string[] parts = _currLine.Split(':');

                    for (int k = 0; k < parts[1].Length; k++)
                    {
                        parts[1] = parts[1].Replace("\"", string.Empty);
                    }
                    parts[1] = parts[1].Trim(' ', ',');

                    string[] materialsName = (string[])ArrayFromString(parts[1], typeof(string[]));
                    Material[] materials = new Material[materialsName.Length];
                    for (int k = 0; k < materials.Length; k++)
                    {
                        materials[k] = materialsName[k].Contains("Default-MaterialInstance") ? new Material(Shader.Find("Standard")) : Resources.Load<Material>("Materials/" + materialsName[k]);
                    }

                    if (j == 0)
                        meshRenderer.materials = materials;
                    else
                        meshRenderer.sharedMaterials = materials;

                    _currLine = _stream.ReadLine();
                }

            }
            _currLine = _stream.ReadLine();
        }

        List<Transform> childrenT = new List<Transform>();
        for (int j = 0; j < _go.transform.childCount; j++)
        {
            childrenT.Add(_go.transform.GetChild(j));
        }

        for (int j = 0; j < children.Length; j++)
        {
            GameObject child = null;

            int siblingIndex = childrenT.FindIndex(n => n.name == children[j]);

            if (siblingIndex == -1) //Si l'enfant sauvegardé n'est pas dans la hierarchie
            {
                child = new GameObject();
                child.transform.SetParent(_go.transform);
            }
            else
            {
                child = childrenT[siblingIndex].gameObject;
            }

            child.transform.SetSiblingIndex(j); //Je mets l'enfant au bon index de la hierarchie
            GameObjectFromString(ref _currLine, _stream, ref child);
            _currLine = _stream.ReadLine();
        }

        return _go;
    }
    static object MeshFromString(ref string _currLine, StreamReader _stream, ref GameObject _go)
    {
        Mesh mesh = null;

        if (!_currLine.Contains("null"))
        {
            _currLine = _stream.ReadLine();
            _currLine = _stream.ReadLine(); //Je mets le curseur sur la premiere propriete

            mesh = new Mesh();
            PropertyInfo[] properties = mesh.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in properties)
            {
                string[] parts = _currLine.Split(':');

                for (int j = 0; j < parts.Length; j++)
                {
                    Filter(ref parts[j]);
                }

                if (property.Name == parts[0] && property.CanWrite)
                {
                    object value = null;

                    if (_currLine.Contains("bounds"))
                    {
                        Bounds bounds = new Bounds();
                        RemoveLast(",", ref parts[2]);
                        bounds.center = (Vector3)VectorFromString(parts[2], typeof(Vector3));
                        bounds.extents = (Vector3)VectorFromString(parts[3], typeof(Vector3));
                    }
                    else if (!parts[1].Contains("null"))
                    {
                        value = GetObjectFromString(property.PropertyType, parts[1], ref _currLine, ref _go, _stream);
                    }

                    property.SetValue(mesh, value);
                }
                _currLine = _stream.ReadLine();
            }
        }

        return mesh.ConvertTo(typeof(object));
    }
    static object Matrix4x4FromString(string _stringValue)
    {
        string[] parts = _stringValue.Split('|');
        Matrix4x4 matrix4X4 = new Matrix4x4();

        for (int i = 0; i < 4; i++)
        {
            matrix4X4.SetRow(i, (Vector4)VectorFromString(parts[i], typeof(Vector4)).ConvertTo(typeof(Vector4)));
        }

        return matrix4X4;
    }
    static object QuaternionFromString(string _stringValue)
    {
        string[] parts = _stringValue.Split(',');
        float[] values = new float[parts.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
        }

        return new Quaternion(values[0], values[1], values[2], values[3]);
    }
    static object ArrayFromString(string _stringValue, Type _arrayType)
    {
        List<object> array = new List<object>();

        if (_stringValue != "[]")
        {
            _stringValue = _stringValue.Trim('[', ']');

            Type elementType = _arrayType.IsArray ? _arrayType.GetElementType() : _arrayType.GetProperty("Item").PropertyType; //R�cup�re le type des �l�ments de la liste OU du tableau
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
    #endregion

    #region Utilities
    static void UpdateJSONObjects()
    {
        foreach (MonoBehaviour key in jsonObjects.Keys.ToList()) //Pour créer une copie afin de pas supprimer un élément en itérant dessus
        {
            if (key == null)
            {
                jsonObjects.Remove(key);
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

            if (_string != string.Empty && _string[_string.Length - 1] == ',')
                RemoveLast(",", ref _string);
        }
        else
        {
            _string = _string.Substring(_string.IndexOf(':') + 2);
            RemoveLast("\"", ref _string);
        }
    }

    static void RemoveLast(string charRemoved, ref string _value)
    {
        int count = _value.LastIndexOf(charRemoved);
        _value = count > 0 ? _value.Remove(count, _value.Length - count) : _value;
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

    static object GetObjectFromString(Type _objectType, string _strValue, ref string _currLine, ref GameObject _go, StreamReader _stream)
    {
        object value = null;

        if (_objectType.Name.Contains("Quaternion"))
        {
            value = QuaternionFromString(_strValue);
        }
        else if (_objectType == typeof(Matrix4x4))
        {
            value = Matrix4x4FromString(_strValue);
        }
        else if (_objectType.Name.Contains("List") || _objectType.IsArray)
        {
            value = ArrayFromString(_strValue, _objectType);
        }
        else if (_objectType.Name.Contains("Vector"))
        {
            value = VectorFromString(_strValue, _objectType);
        }
        else if (_objectType.IsEnum)
        {
            value = Enum.Parse(_objectType, _strValue);
        }
        else if (_objectType == typeof(Transform))
        {
            value = _strValue.Equals("null") ? null : _go.transform.parent;
        }
        else if (_objectType == typeof(Mesh))
        {
            value = MeshFromString(ref _currLine, _stream, ref _go);
        }
        else
        {
            value = Convert.ChangeType(_strValue, _objectType, CultureInfo.InvariantCulture); //Dernier param�tre pour que la virgule soit consid�r� comme un point
        }
        return value;
    }
    #endregion
}