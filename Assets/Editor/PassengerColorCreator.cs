using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class PassengerColorCreator : EditorWindow
{
    class ColorEntry
    {
        public string name = "NewColor";
        public Color color = Color.white;
    }

    List<ColorEntry> entries = new();
    Vector2 scroll;
    string assetFolder = "Assets/ScriptableObject";

    [MenuItem("Tools/Passenger Color Creator")]
    static void Open() => GetWindow<PassengerColorCreator>("Passenger Color Creator");

    void OnEnable()
    {
        if (entries.Count == 0)
            entries.Add(new ColorEntry() { name = "Red", color = Color.red });
    }

    void OnGUI()
    {
        GUILayout.Label("Passenger Color Creator", EditorStyles.boldLabel);
        assetFolder = EditorGUILayout.TextField("Asset Folder", assetFolder);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < entries.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            entries[i].name = EditorGUILayout.TextField(entries[i].name);
            entries[i].color = EditorGUILayout.ColorField(entries[i].color);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                entries.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        if (GUILayout.Button("+ Add Color"))
            entries.Add(new ColorEntry());

        if (GUILayout.Button("Clear List"))
            entries.Clear();

        GUILayout.Space(10);

        if (GUILayout.Button("Create All", GUILayout.Height(35)))
        {
            foreach (var e in entries)
                CreateColor(e.name, e.color);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done", "All PassengerColors created.", "OK");
        }
    }

    void CreateColor(string name, Color c)
    {
        CreateFolderIfNeeded(assetFolder);

        string matPath = AssetDatabase.GenerateUniqueAssetPath(assetFolder + "/" + name + ".mat");
        string soPath = AssetDatabase.GenerateUniqueAssetPath(assetFolder + "/" + name + ".asset");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        else
            mat.color = c;

        AssetDatabase.CreateAsset(mat, matPath);

        PassengerColor so = ScriptableObject.CreateInstance<PassengerColor>();
        so.colorName = name;
        so.material = mat;
        so.pixelColor = c;

        AssetDatabase.CreateAsset(so, soPath);
    }

    void CreateFolderIfNeeded(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] split = path.Split('/');
        string current = split[0];

        for (int i = 1; i < split.Length; i++)
        {
            string next = current + "/" + split[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, split[i]);
            current = next;
        }
    }
}