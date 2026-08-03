using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ExportScriptTool : EditorWindow
{
    // Chuyển thành List để chứa nhiều đường dẫn thư mục cùng lúc
    [SerializeField]
    private List<string> targetFolders = new List<string> { "Assets/Scripts", "Assets/Editor" };
    private string outputFile = "ExportedScripts.txt";

    private SerializedObject serializedWindow;
    private SerializedProperty foldersProperty;

    [MenuItem("Tools/Export Scripts To Text")]
    static void Open()
    {
        var window = GetWindow<ExportScriptTool>("Export Scripts");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        serializedWindow = new SerializedObject(this);
        foldersProperty = serializedWindow.FindProperty(nameof(targetFolders));
    }

    private void OnGUI()
    {
        serializedWindow.Update();

        GUILayout.Label("Export All C# Scripts", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Hiển thị danh sách thư mục (có thể nhấn dấu + hoặc - để thêm bớt trực tiếp trên UI)
        EditorGUILayout.PropertyField(foldersProperty, new GUIContent("Target Folders"), true);
        serializedWindow.ApplyModifiedProperties();

        EditorGUILayout.Space(5);
        outputFile = EditorGUILayout.TextField("Output File Name", outputFile);

        GUILayout.Space(15);

        if (GUILayout.Button("Export All", GUILayout.Height(30)))
        {
            ExportScripts();
        }
    }

    void ExportScripts()
    {
        List<string> allFiles = new List<string>();

        // Quét qua toàn bộ danh sách thư mục được cấu hình
        foreach (string folder in targetFolders)
        {
            if (string.IsNullOrEmpty(folder)) continue;

            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"Thư mục không tồn tại (Bỏ qua): {folder}");
                continue;
            }

            // Lấy tất cả file .cs trong thư mục hiện tại và các thư mục con của nó
            string[] filesInFolder = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
            allFiles.AddRange(filesInFolder);
        }

        if (allFiles.Count == 0)
        {
            Debug.LogError("Không tìm thấy file .cs nào trong các thư mục đã chỉ định!");
            return;
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========================================");
        sb.AppendLine("UNITY SCRIPT EXPORT");
        sb.AppendLine("========================================");
        sb.AppendLine($"Tổng số file quét được: {allFiles.Count}");
        sb.AppendLine();

        foreach (string file in allFiles)
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

        Debug.Log($"<color=green>Export hoàn thành!</color> Đã xuất {allFiles.Count} file tại: {savePath}");

        EditorUtility.RevealInFinder(savePath);
    }
}