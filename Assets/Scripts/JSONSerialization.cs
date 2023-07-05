using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using System.Text;
using System.Runtime.Serialization;

[AttributeUsage(AttributeTargets.Field)]
public class JSONRead : Attribute { }

enum RemoveCharState
{
    Anywhere,
    Last
}


public static class JSONSerialization
{
    static Dictionary<MonoBehaviour, Dictionary<string, object>> jsonObjects;

    static JSONSerialization()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        jsonObjects = new Dictionary<MonoBehaviour, Dictionary<string, object>>();
    }

    #region PublicMethods
    public static void Save(string _filename)
    {
        UpdateJSONObjects();

        using (StreamWriter stream = new StreamWriter(Application.streamingAssetsPath + "/Saves/" + _filename + ".txt"))
        {
            StringBuilder json = new StringBuilder("{\n");

            int index = 0;
            foreach (MonoBehaviour script in jsonObjects.Keys)
            {
                json.Append((index == 0) ? "\"[Script] " + script.name + "\" :\n{\n" : ",\n\"[Script] " + script.name + "\" :\n{\n");

                foreach (var jsonObject in jsonObjects[script])
                {
                    json.Append(jsonObject.Value.ToJsonFormat(jsonObject.Key));
                }
                RemoveChar(ref json, RemoveCharState.Last, ',');

                json.Append("\n}");
                index++;
            }
            json.Append("\n}");
            stream.Write(json);
        }
    }
    public static void Load(string _filename)
    {
        UpdateJSONObjects();
        
        MonoBehaviour currentScript = null;
        StringBuilder json = new StringBuilder();
        Dictionary<string, GameObject> gameObjectsInstancied = new Dictionary<string, GameObject>();

        using (StreamReader stream = new StreamReader(Application.streamingAssetsPath + "/Saves/" + _filename + ".txt"))
        {
            json.Append(stream.ReadToEnd());
        }

        RemoveChar(ref json, RemoveCharState.Anywhere, '{', '}');
        string[] jsonLines = json.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(str => str != ",").ToArray();

        for (int i = 0; i < jsonLines.Length; i++)
        {
            string[] parts = jsonLines[i].Split(':');

            if (parts[0].Contains("Script"))
            {
                parts[0] = parts[0].Remove(0, parts[0].IndexOf(']') + 1).Trim(' ', '\"');
                currentScript = jsonObjects.Keys.FirstOrDefault(n => n.name == parts[0]);

                if (currentScript == null)
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Type test = assembly.ExportedTypes.Where(n => parts[0].Contains(n.FullName)).ToArray()[0];
                    GameObject gameObject = new GameObject(parts[0]);
                    currentScript = gameObject.AddComponent(test).GetComponent<MonoBehaviour>();
                    GetJSONObjects(currentScript);
                }
            }
            else if (parts[1] == string.Empty)
            {
                Type scriptType = currentScript.GetType();
                RemoveChar(ref parts[0], RemoveCharState.Last, ',');
                parts[0] = parts[0].Trim('\"', ' ');
                FieldInfo field = scriptType.GetField(parts[0], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                GameObject gameObject = field.GetValue(currentScript) as GameObject;

                if (gameObject == null && !gameObjectsInstancied.TryGetValue(field.Name, out gameObject))
                {
                    parts = jsonLines[++i].Split(':');
                    RemoveChar(ref parts[1], RemoveCharState.Last, ',');
                    parts[1] = parts[1].Trim('\"', ' ');

                    GameObject goInHierarchy = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(n => n.name == parts[1]);

                    if (goInHierarchy == null)
                    {
                        gameObject = new GameObject();
                        gameObjectsInstancied.Add(field.Name, gameObject);
                        string fieldName = jsonObjects[currentScript].Keys.FirstOrDefault(n => n == field.Name);
                        jsonObjects[currentScript][fieldName] = gameObject;
                    }
                    else
                    {
                        gameObject = goInHierarchy;
                    }
                    --i;
                }

                GameObjectFromString(ref jsonLines, ref i, ref gameObject);
                field.SetValue(currentScript, gameObject);
            }
            else
            {
                RemoveChar(ref parts[1], RemoveCharState.Last, ',');
                for (int j = 0; j < parts.Length; j++)
                {
                    parts[j] = parts[j].Trim('\"', ' ');
                }

                Type scriptType = currentScript.GetType();
                FieldInfo field = scriptType.GetField(parts[0], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                object value = parts[1].FromJsonString(field.FieldType);
                field.SetValue(currentScript, value);
            }
        }
    }
    public static void GetJSONObjects(MonoBehaviour _script)
    {
        Type scriptType = _script.GetType();
        FieldInfo[] fields = scriptType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo field in fields)
        {
            if (Attribute.IsDefined(field, typeof(JSONRead)))
            {
                if (jsonObjects.ContainsKey(_script))
                {
                    if (!jsonObjects[_script].ContainsKey(field.Name))
                        jsonObjects[_script].Add(field.Name, field.GetValue(_script));
                }
                else
                {
                    jsonObjects.Add(_script, new Dictionary<string, object>());
                }
            }
        }
    }
    #endregion

    #region SaveMethods
    static StringBuilder FormatStringToJson<T>(T _value, string _fieldName)
    {
        bool hasFieldName = _fieldName != string.Empty;
        StringBuilder stringBuilder = new StringBuilder(hasFieldName ? "\"" + _fieldName + "\" : " : string.Empty);
        Type type = _value.GetType();
        string valueStr = string.Empty;

        if (type == typeof(bool))
        {
            valueStr = _value.ToString().ToLower();
        }
        else if (type.IsPrimitive)
        {
            valueStr = _value.ToString();
        }
        else
        {
            valueStr = "\"" + _value.ToString() + "\"";
        }

        stringBuilder.Append(hasFieldName ? valueStr + ",\n" : valueStr + ", ");
        return stringBuilder;
    }
    static StringBuilder FormatMeshToString(Mesh _mesh, string _fieldName)
    {
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        StringBuilder value = new StringBuilder("\"[ObjectComponent] " + _fieldName + "\" :\n{\n");

        foreach (PropertyInfo property in _mesh.GetType().GetProperties(bindingFlags))
        {
            if (property.CanWrite)
            {
                object propertyValue = property.GetValue(_mesh);

                if (propertyValue == null)
                    value.Append("\"" + property.Name + "\" : null,\n");
                else
                {
                    value.Append(propertyValue.ToJsonFormat(property.Name));
                }
            }
        }
        RemoveChar(ref value, RemoveCharState.Last, ',');
        value.Append("\n},\n");
        return value;
    }
    static StringBuilder FormatGameObjectToString(GameObject _gameObject, string _fieldName)
    {
        StringBuilder value = new StringBuilder("\"" + _fieldName + "\" :\n{\n");
        value.Append("\"GO_Name\" : \"" + _gameObject.name + "\",\n" + "\"GO_Tag\" : \"" + _gameObject.tag + "\",\n" + "\"GO_Layer\" : " + _gameObject.layer + ",\n" + "\"GO_IsActive\" : " + _gameObject.activeSelf.ToString().ToLower() + ",\n" + "\"GO_ChildCount\" : " + _gameObject.transform.childCount + ",\n");

        Component[] components = _gameObject.GetComponents<Component>();

        value.Append("\"Components\" : " + components.Length + ",\n");
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            value.Append(component.ToJsonFormat(component.name));
        }

        //Children
        for (int j = 0; j < _gameObject.transform.childCount; j++)
        {
            value.Append(_gameObject.transform.GetChild(j).gameObject.ToJsonFormat("Child_" + _gameObject.transform.GetChild(j).name));
        }

        RemoveChar(ref value, RemoveCharState.Last, ',');
        value.Append("\n},\n");
        return value;
    }
    static StringBuilder FormatComponentToString(Component _component, string _fieldName)
    {
        Type componentType = _component.GetType();
        StringBuilder value = new StringBuilder("\"" + componentType + "\" :\n{\n");
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;

        foreach (PropertyInfo property in componentType.GetProperties(bindingFlags | BindingFlags.DeclaredOnly))
        {
            if (property.CanWrite && property.Name != "root" && property.Name != "scene")
            {
                object propertyValue = property.GetValue(_component);

                if (propertyValue == null)
                    value.Append("\"" + property.Name + "\" : null,\n");
                else if (property.Name == "parent")
                    value.Append("\"" + property.Name + "\"" + ": \"" + _component.transform.parent + "\",\n");
                else
                {
                    value.Append(propertyValue.ToJsonFormat(property.Name));
                }
            }
        } //Unity Component

        foreach (FieldInfo field in componentType.GetFields(bindingFlags | BindingFlags.NonPublic))
        {
            object propertyValue = field.GetValue(_component);

            if (propertyValue == null)
                value.Append("\"" + field.Name + "\" : null,\n");
            else
            {
                value.Append(propertyValue.ToJsonFormat(field.Name));
            }
        } //Custom Component

        if (componentType == typeof(MeshRenderer))
        {
            MeshRenderer renderer = (MeshRenderer)_component;
            value.Append(renderer.material.ToJsonFormat("material"));
            value.Append(renderer.sharedMaterial.ToJsonFormat("sharedMaterial"));
            value.Append(renderer.materials.ToJsonFormat("materials"));
            value.Append(renderer.sharedMaterials.ToJsonFormat("sharedMaterials"));
        }

        if (componentType.BaseType == typeof(Collider))
        {
            Collider collider = (Collider)_component;
            value.Append(collider.material.ToJsonFormat("physicMaterial"));
            value.Append(collider.sharedMaterial.ToJsonFormat("sharedPhysicMaterial"));
        }

        RemoveChar(ref value, RemoveCharState.Last, ',');
        value.Append("\n},\n");

        return value;
    }
    static StringBuilder FormatMatrix4x4ToString(Matrix4x4 _matrix4x4, string _fieldName)
    {
        StringBuilder value = new StringBuilder("\"" + _fieldName + "\" : \"");
        Vector4[] rows = new Vector4[4];

        for (int i = 0; i < 4; i++)
        {
            rows[i] = _matrix4x4.GetRow(i);
            value.Append(rows[i].ToString() + "|");
        }
        RemoveChar(ref value, RemoveCharState.Last, '|');
        value.Append("\",\n");
        return value;
    }
    static StringBuilder MaterialToString<T>(T material, string _fieldName)
    {
        StringBuilder value = null;

        string materialName = material.ToString();
        int indexInstanceStr = materialName.IndexOf("(Instance)");
        Type type = material.GetType();
        if (type == typeof(Material))
        {
            materialName = materialName.Substring(0, indexInstanceStr == -1 ? materialName.IndexOf("(UnityEngine.Material)") - 1 : materialName.IndexOf("(Instance)") - 1);

            if (_fieldName == string.Empty)
            {
                value = new StringBuilder("\"" + materialName);
            }
            else value = new StringBuilder(_fieldName == "material" ? "\"material\" : \"" + materialName : "\"sharedMaterial\" : \"" + materialName);
        }
        else
        {
            materialName = materialName.Substring(0, indexInstanceStr == -1 ? materialName.IndexOf("(UnityEngine.PhysicMaterial)") - 1 : materialName.IndexOf("(Instance)") - 1);
            if (_fieldName == string.Empty)
            {
                value = new StringBuilder("\"" + materialName);
            }
            else value = new StringBuilder(_fieldName == "physicMaterial" ? "\"physicMaterial\" : \"" + materialName : "\"sharedPhysicMaterial\" : \"" + materialName);
        }
        value.Append("\",\n");

        return value;
    }
    static StringBuilder ArrayToString(object array, string _fieldName)
    {
        StringBuilder arrayParsed = new StringBuilder("\"" + _fieldName + "\" : [");

        List<object> arrayObjects = (List<object>)array.ConvertTo(typeof(List<object>));

        if (array != null)
        {
            foreach (object item in arrayObjects)
            {
                arrayParsed.Append(item.ToJsonFormat(string.Empty));
            }
            RemoveChar(ref arrayParsed, RemoveCharState.Last, ',');
        }

        arrayParsed.Append("],\n");
        return arrayParsed;
    }

    #endregion

    #region LoadMethods
    static object VectorFromString(string _stringValue, Type _vectorType)
    {
        _stringValue = _stringValue.Trim('(', ')');
        bool hasFloat = _vectorType == typeof(Vector2) || _vectorType == typeof(Vector3) || _vectorType == typeof(Vector4) || _vectorType == typeof(Quaternion);
        string[] parts = _stringValue.Split(',');
        object[] values = new object[parts.Length];

        for (int i = 0; i < values.Length; i++)
        {
            if (hasFloat)
                values[i] = float.Parse(parts[i]);
            else
                values[i] = int.Parse(parts[i]);
        }

        ConstructorInfo constructorInfos = _vectorType.GetConstructors()[0];
        return constructorInfos.Invoke(values);
    }
    static object ColorFromString(string _stringValue)
    {
        string colorStr = _stringValue.Remove(0, 4); //formattage en Vector4
        Vector4 colorValues = (Vector4)VectorFromString(colorStr, typeof(Vector4));

        return new Color(colorValues.x, colorValues.y, colorValues.z, colorValues.w);
    }
    static object RectFromString(string _stringValue)
    {
        string rectString = _stringValue.Replace("x:", string.Empty).Replace("y:", string.Empty).Replace("width:", string.Empty).Replace("height:", string.Empty);
        Vector4 rectValues = (Vector4)VectorFromString(rectString, typeof(Vector4));
        return new Rect(rectValues.x, rectValues.y, rectValues.z, rectValues.w);
    }
    static void ComponentFromString(ref string[] _lines, ref int _i, ref GameObject _gameObject)
    {
        string[] parts = _lines[++_i].Split(':');
        RemoveChar(ref parts[0], RemoveCharState.Last, ',');
        parts[0] = parts[0].Trim('\"', ' ');

        bool isCustomComponent = false;

        parts[0] += ",UnityEngine"; //Pour le formattage
        Type componentType = Type.GetType(parts[0]);

        if (componentType == null) //Est un component custom
        {
            isCustomComponent = true;
            parts[0] = parts[0].Remove(parts[0].IndexOf(","));
            componentType = Type.GetType(parts[0]);
        }
        Component[] components = _gameObject.GetComponents<Component>();
        Component currComponent = components.FirstOrDefault(n => n.GetType() == componentType) ?? _gameObject.AddComponent(componentType);

        bool isEndOfComponent = false;

        do
        {
            if (_i < _lines.Length - 1)
            {
                parts = _lines[++_i].Split(':');
            }

            if (!parts[0].Contains("[ObjectComponent]") && parts[1] == string.Empty || _i >= _lines.Length - 1)
            {
                isEndOfComponent = true;

                if (_i < _lines.Length - 1)
                {
                    _i--;
                }
            }
            else
            {
                for (int k = 0; k < parts.Length; k++)
                {
                    parts[k] = parts[k].Trim(',', '\"', ' ');
                }

                parts[0] = parts[0].Remove(0, parts[0].IndexOf(" ") + 1); //J'enlève [ObjectComponent] si il y en a un

                if (isCustomComponent)
                {
                    object value = null;
                    FieldInfo fieldCustomComponent = componentType.GetField(parts[0], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (fieldCustomComponent.FieldType.Equals(typeof(Mesh)))
                    {
                        value = MeshFromString(ref parts[1], ref _lines, ref _i);
                    }
                    else if (parts[0] == "parent")
                    {
                        value = _gameObject.transform.parent;
                    }
                    else if (fieldCustomComponent.FieldType == typeof(Rect))
                    {
                        string rectValues = _lines[_i].Remove(0, _lines[_i].IndexOf("("));
                        RemoveChar(ref rectValues, RemoveCharState.Last, '\"');
                        value = RectFromString(rectValues);
                    }
                    else
                    {
                        value = parts[1].FromJsonString(fieldCustomComponent.FieldType);
                    }

                    fieldCustomComponent.SetValue(currComponent, value);
                }
                else
                {
                    object value = null;
                    PropertyInfo propertyUnityComponent = componentType.GetProperty(parts[0]);
                    bool hasMaterial = false;
                    if (parts[0] == "physicMaterial" || parts[0] == "sharedPhysicMaterial")
                    {
                        hasMaterial = true;
                        Collider collider = currComponent as Collider;
                        value = MaterialFromString(parts[1], typeof(PhysicMaterial));

                        if (parts[0] == "physicMaterial")
                            collider.material = value as PhysicMaterial;
                        else
                            collider.sharedMaterial = value as PhysicMaterial;
                    }
                    else if (propertyUnityComponent.PropertyType.Equals(typeof(Mesh)))
                    {
                        value = MeshFromString(ref parts[1], ref _lines, ref _i);
                    }
                    else if (parts[0] == "parent")
                    {
                        value = _gameObject.transform.parent;
                    }
                    else if (propertyUnityComponent.PropertyType == typeof(Rect))
                    {
                        string rectValues = _lines[_i].Remove(0, _lines[_i].IndexOf("("));
                        RemoveChar(ref rectValues, RemoveCharState.Last, '\"');
                        value = RectFromString(rectValues);
                    }
                    else
                    {
                        value = parts[1].FromJsonString(propertyUnityComponent.PropertyType);
                    }

                    if (!hasMaterial)
                        propertyUnityComponent.SetValue(currComponent, value);
                }
            }
        } while (!isEndOfComponent);
    }
    static void GameObjectFromString(ref string[] _lines, ref int _i, ref GameObject _gameObject)
    {
        int componentsCount = 0;
        int childCount = 0;

        string[] parts = null;
        for (int j = 0; j < 6; j++) //GameObjects : Name, Tag, Layer, IsActive, ChildCount, ComponentsCount
        {
            parts = _lines[++_i].Split(':');
            RemoveChar(ref parts[1], RemoveCharState.Last, ',');
            parts[1] = parts[1].Trim('\"', ' ');

            switch (j)
            {
                case 0: _gameObject.name = parts[1]; break;
                case 1: _gameObject.tag = parts[1]; break;
                case 2: _gameObject.layer = int.Parse(parts[1]); break;
                case 3: _gameObject.SetActive(bool.Parse(parts[1])); break;
                case 4: childCount = int.Parse(parts[1]); break;
                case 5: componentsCount = int.Parse(parts[1]); break;
            }
        }

        for (int j = 0; j < componentsCount; j++)
        {
            ComponentFromString(ref _lines, ref _i, ref _gameObject);
        }

        if (childCount > 0)
        {
            GameObject child = null;

            for (int k = 0; k < childCount; k++)
            {
                string childName = _lines[++_i];
                RemoveChar(ref childName, RemoveCharState.Last, ' ');
                childName = childName.Trim('\"').Remove(0, 6);

                Transform currChild = k < _gameObject.transform.childCount ? _gameObject.transform.GetChild(k) : null;
                if (currChild == null || currChild.name != childName) //L'enfant n'existe pas
                {
                    child = new GameObject();
                    child.transform.SetParent(_gameObject.transform);
                }
                else
                {
                    child = currChild.gameObject;
                }
                child.transform.SetSiblingIndex(k); //Je mets l'enfant au bon index de la hierarchie
                GameObjectFromString(ref _lines, ref _i, ref child); //Je récupère l'enfant
            }
        }
    }
    static object Matrix4x4FromString(string _stringValue)
    {
        string[] parts = _stringValue.Split('|');
        Matrix4x4 matrix4X4 = new Matrix4x4();

        for (int i = 0; i < 4; i++)
        {
            matrix4X4.SetRow(i, (Vector4)VectorFromString(parts[i], typeof(Vector4)));
        }

        return matrix4X4;
    }
    static object MeshFromString(ref string _firstValue, ref string[] _lines, ref int _i)
    {
        Mesh mesh = null;

        if (_firstValue != "null")
        {
            mesh = new Mesh();
            bool isEndOfComponent = false;

            do
            {
                string[] parts = _lines[++_i].Split(':');

                if (parts[1] == string.Empty)
                {
                    isEndOfComponent = true;
                    _i--;
                }
                else
                {
                    RemoveChar(ref parts[1], RemoveCharState.Last, ',');
                    for (int k = 0; k < parts.Length; k++)
                    {
                        parts[k] = parts[k].Trim('\"', ' ');
                    }

                    object value = null;
                    PropertyInfo property = mesh.GetType().GetProperty(parts[0]);

                    if (parts[0] == "bounds")
                    {
                        Bounds bounds = new Bounds();
                        RemoveChar(ref parts[2], RemoveCharState.Last, ',');
                        RemoveChar(ref parts[3], RemoveCharState.Last, '\"');
                        bounds.center = (Vector3)VectorFromString(parts[2], typeof(Vector3));
                        bounds.extents = (Vector3)VectorFromString(parts[3], typeof(Vector3));
                        value = bounds;
                    }
                    else
                    {
                        value = parts[1].FromJsonString(property.PropertyType);
                    }
                    property.SetValue(mesh, value);
                }
            } while (!isEndOfComponent);
        }

        return mesh;
    }
    static object MaterialFromString(string _materialName, Type _materialType)
    {
        string path = _materialType == typeof(Material) ? "Materials/" : "PhysicsMaterials/";
        return _materialName == "Default-Material" ? new Material(Shader.Find("Standard")) : Resources.Load(path + _materialName, _materialType);
    }
    static object ArrayFromString(string _stringValue, Type _targetType)
    {
        List<object> array = new List<object>();

        if (_stringValue != "[]" && _stringValue != "null")
        {
            _stringValue = _stringValue.Trim('[', ']');

            Type elementType = _targetType.IsArray ? _targetType.GetElementType() : _targetType.GetProperty("Item").PropertyType; //R�cup�re le type des �l�ments de la liste OU du tableau

            string[] parts = _stringValue.Split(',').Select(n => n.Trim('\"')).ToArray();
            if (elementType.Name.Contains("Vector") || elementType == typeof(Quaternion))
            {
                array = SplitVectorStringToVectorFormat(_stringValue, elementType);
            }
            else
            {
                foreach (string part in parts)
                {                    
                    array.Add(part.FromJsonString(elementType));
                }
            }
        }
        return array.ConvertTo(_targetType);
    }
    static object ValueTypeFromString(string _stringValue, Type _type)
    {
        object primitiveFromString = null;
        if (_type.Name.Contains("Vector") || _type == typeof(Quaternion))
        {
            primitiveFromString = VectorFromString(_stringValue, _type);
        }
        else if (_type.IsEnum)
        {
            primitiveFromString = Enum.Parse(_type, _stringValue);
        }
        else if (_type == typeof(Matrix4x4))
        {
            primitiveFromString = Matrix4x4FromString(_stringValue);
        }
        else if (_type == typeof(Color))
        {
            primitiveFromString = ColorFromString(_stringValue);
        }
        else if (_type == typeof(Rect))
        {
            primitiveFromString = RectFromString(_stringValue);
        }
        else
        {
            _stringValue = _stringValue.Trim(' ', '\"'); 
            primitiveFromString = Convert.ChangeType(_stringValue, _type);
        }
        return primitiveFromString;
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
    static StringBuilder ToJsonFormat<T>(this T obj, string _fieldName)
    {
        StringBuilder toJson = null;
        Type type = obj.GetType();

        if (type == typeof(Matrix4x4))
            toJson = FormatMatrix4x4ToString((obj as Matrix4x4?).Value, _fieldName);
        else if (type == typeof(string) || type.IsValueType)
            toJson = FormatStringToJson(obj, _fieldName);
        else if (type.BaseType == typeof(Array) || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            toJson = ArrayToString(obj, _fieldName);
        else if (type == typeof(GameObject))
            toJson = FormatGameObjectToString(obj as GameObject, _fieldName);
        else if (type.BaseType == typeof(Component) || type.BaseType == typeof(Renderer) || type.BaseType == typeof(Collider) ||
                 type.BaseType == typeof(Behaviour) || type.BaseType == typeof(MonoBehaviour) || type.BaseType == typeof(AudioBehaviour))
            toJson = FormatComponentToString(obj as Component, _fieldName);
        else if (type == typeof(Material) || type == typeof(PhysicMaterial))
            toJson = MaterialToString(obj, _fieldName);
        else if (type == typeof(Mesh))
            toJson = FormatMeshToString(obj as Mesh, _fieldName);
        else if (type == typeof(RenderTexture))
            toJson = new StringBuilder("\"" + _fieldName + "\" : \"" + obj.ToString().Replace(" (UnityEngine.RenderTexture)", string.Empty) + "\",\n");

        return toJson;
    }
    static object FromJsonString(this string obj, Type _target)
    {
        object objectFromString = null;

        if (_target == typeof(Matrix4x4))
            objectFromString = Matrix4x4FromString(obj);
        else if (_target == typeof(string) || _target.IsValueType)
            objectFromString = ValueTypeFromString(obj, _target);
        else if (_target.BaseType == typeof(Array) || _target.IsGenericType && _target.GetGenericTypeDefinition() == typeof(List<>))
            objectFromString = ArrayFromString(obj, _target);
        else if (_target == typeof(Material) || _target == typeof(PhysicMaterial))
            objectFromString = MaterialFromString(obj, _target);
        else if (_target == typeof(Rect))
            objectFromString = RectFromString(obj);
        else if (_target == typeof(RenderTexture))
            objectFromString = Resources.Load<RenderTexture>("RenderTextures/" + obj);

        return objectFromString;
    }
    static void RemoveChar(ref StringBuilder _valueStr, RemoveCharState _removeState, params char[] _charToRemoved)
    {
        switch (_removeState)
        {
            case RemoveCharState.Anywhere:
                foreach (char c in _charToRemoved)
                {
                    _valueStr = _valueStr.Replace(c.ToString(), string.Empty);
                }
                break;
            case RemoveCharState.Last:
                for (int i = 0; i < _charToRemoved.Length; i++)
                {
                    for (int j = _valueStr.Length - 1; j >= 0; j--)
                    {
                        if (_valueStr[j] == _charToRemoved[i])
                        {
                            _valueStr = _valueStr.Remove(j, _valueStr.Length - j);
                            break;
                        }
                    }
                }
                break;
            default:
                Debug.LogError("RemoveChar Function out of range");
                break;
        }
    }
    static void RemoveChar(ref string _valueStr, RemoveCharState _removeState, params char[] _charToRemoved)
    {
        switch (_removeState)
        {
            case RemoveCharState.Anywhere:
                foreach (char c in _charToRemoved)
                {
                    _valueStr = _valueStr.Replace(c.ToString(), string.Empty);
                }
                break;
            case RemoveCharState.Last:
                foreach (char c in _charToRemoved)
                {
                    int index = _valueStr.LastIndexOf(c);
                    _valueStr = index > 0 ? _valueStr.Remove(index, _valueStr.Length - index) : _valueStr;
                }
                break;
            default:
                Debug.LogError("RemoveChar Function out of range");
                break;
        }
    }
    static List<object> SplitVectorStringToVectorFormat(string _values, Type _vectorType)
    {
        List<object> vectorList = new List<object>();
        int startIndex = 0;
        int endIndex = 0;
        int quoteCount = 0;

        RemoveChar(ref _values, RemoveCharState.Anywhere,  ' ');

        for (int i = 0; i < _values.Length; i++)
        {
            if (_values[i] == '\"')
            {
                quoteCount++;

                if (quoteCount % 2 == 0) //Si on a atteint la fin du vecteur
                {
                    endIndex = i;

                    string element = _values.Substring(startIndex + 1, endIndex - startIndex - 1);
                    vectorList.Add(VectorFromString(element, _vectorType));

                    startIndex = i + 2;
                }
            }
        }
        return vectorList;
    }
    #endregion
}