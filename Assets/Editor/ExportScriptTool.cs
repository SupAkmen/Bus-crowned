using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ExportScriptTool : EditorWindow
{
    private string scriptsFolder = "Assets/Scripts";
    private string outputFile = "ExportedScripts.txt";

    [MenuItem("Tools/Export Scripts To Text")]
    static void Open()
    {
        GetWindow<ExportScriptTool>("Export Scripts");
    }

    private void OnGUI()
    {
        GUILayout.Label("Export All C# Scripts", EditorStyles.boldLabel);

        scriptsFolder = EditorGUILayout.TextField("Scripts Folder", scriptsFolder);
        outputFile = EditorGUILayout.TextField("Output File", outputFile);

        GUILayout.Space(10);

        if (GUILayout.Button("Export"))
        {
            ExportScripts();
        }
    }

    void ExportScripts()
    {
        if (!Directory.Exists(scriptsFolder))
        {
            Debug.LogError("Folder không tồn tại: " + scriptsFolder);
            return;
        }

        string[] files = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========================================");
        sb.AppendLine("UNITY SCRIPT EXPORT");
        sb.AppendLine("========================================");
        sb.AppendLine();

        foreach (string file in files)
        {
            string relativePath = file.Replace("\\", "/");

            sb.AppendLine("########################################");
            sb.AppendLine("FILE: " + relativePath);
            sb.AppendLine("########################################");
            sb.AppendLine();

            sb.AppendLine(File.ReadAllText(file));

            sb.AppendLine();
            sb.AppendLine();
        }

        string savePath = Path.Combine(Application.dataPath, "..", outputFile);

        File.WriteAllText(savePath, sb.ToString(), Encoding.UTF8);

        Debug.Log("Export hoàn thành: " + savePath);

        EditorUtility.RevealInFinder(savePath);
    }
}
