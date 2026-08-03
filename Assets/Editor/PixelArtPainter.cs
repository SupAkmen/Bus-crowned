// Assets/Editor/PixelArtPainter.cs
//
// Cach dung:
// 1. Dat file nay vao thu muc "Assets/Editor/"
// 2. Mo qua menu: Tools > Passenger Game > Pixel Art Painter
// 3. (Tuy chon) Gan mot PassengerColorPalette vao o "Palette" de dung dung mau
//    da khai bao trong game. Neu khong gan, co the tu tao mau tuy y bang ColorField.
// 4. Chon mau -> chon cong cu (Brush/Eraser/Bucket/Eyedrop) -> click hoac keo
//    chuot len canvas de ve.
// 5. Ctrl+Z de Undo, Ctrl+Y (hoac Ctrl+Shift+Z) de Redo.
// 6. Bam "Save As PNG..." de xuat anh, san sang dung lam layoutImage trong
//    PassengerSpawnAreas.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PixelArtPainter : EditorWindow
{
    enum Tool { Brush, Eraser, Bucket, Eyedropper }

    [Header("Canvas")]
    int canvasWidth = 32;
    int canvasHeight = 32;

    Texture2D canvasTexture;
    Color[] pixels;

    [Header("Palette")]
    PassengerColorPalette colorPalette;
    List<Color> customColors = new(); // fallback neu chua gan palette
    int selectedColorIndex = 0;
    Color customPickColor = Color.red;

    Tool currentTool = Tool.Brush;
    int brushSize = 1;

    Vector2 scrollPos;
    float zoom = 14f;

    const int minCanvasSize = 1;
    const int maxCanvasSize = 256;

    // >>> UNDO/REDO: bat dau ==========================================
    // Stack luu snapshot Color[] cua toan bo canvas. Day khong phai Unity's
    // native Undo system (Undo.RecordObject) vi texture/pixels la runtime data,
    // khong phai serialized field cua mot UnityEngine.Object cu the. Thay vao do
    // ta tu quan ly 1 stack lich su don gian, du dung cho nhu cau cua 1 tool ve
    // pixel art. Luu y: lich su nay se mat khi dong cua so hoac Unity recompile
    // script (domain reload) - day la danh doi hop ly de giu code don gian.
    List<Color[]> undoStack = new();
    List<Color[]> redoStack = new();
    const int maxHistorySize = 50; // gioi han so buoc undo de tranh ton bo nho voi canvas lon
    // >>> UNDO/REDO: ket thuc ==========================================

    [MenuItem("Tools/Passenger Game/Pixel Art Painter")]
    public static void ShowWindow()
    {
        var window = GetWindow<PixelArtPainter>("Pixel Art Painter");
        window.minSize = new Vector2(560, 520);
    }

    void OnEnable()
    {
        if (canvasTexture == null)
            NewCanvas(canvasWidth, canvasHeight);
    }

    void NewCanvas(int w, int h)
    {
        canvasWidth = Mathf.Clamp(w, minCanvasSize, maxCanvasSize);
        canvasHeight = Mathf.Clamp(h, minCanvasSize, maxCanvasSize);

        pixels = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0, 0, 0, 0); // trong suot

        canvasTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
        canvasTexture.filterMode = FilterMode.Point;
        canvasTexture.wrapMode = TextureWrapMode.Clamp;
        ApplyPixels();

        // Canvas moi (kich thuoc khac) -> lich su undo/redo cu khong con y nghia
        undoStack.Clear();
        redoStack.Clear();
    }

    void ApplyPixels()
    {
        canvasTexture.SetPixels(pixels);
        canvasTexture.Apply();
    }

    void OnGUI()
    {
        HandleKeyboardShortcuts();

        DrawToolbar();
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        DrawPaletteColumn();
        DrawCanvasArea();
        EditorGUILayout.EndHorizontal();
    }

    // >>> UNDO/REDO: bat dau ==========================================
    void HandleKeyboardShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        bool ctrlOrCmd = e.control || e.command; // command cho macOS

        if (ctrlOrCmd && e.keyCode == KeyCode.Z && !e.shift)
        {
            Undo();
            e.Use();
        }
        else if (ctrlOrCmd && ((e.keyCode == KeyCode.Z && e.shift) || e.keyCode == KeyCode.Y))
        {
            Redo();
            e.Use();
        }
    }

    /// <summary>
    /// Luu snapshot HIEN TAI cua pixels vao undoStack TRUOC KHI thuc hien thay doi moi.
    /// Chi goi 1 lan duy nhat khi bat dau 1 thao tac (MouseDown), khong goi lien tuc
    /// trong khi keo chuot (MouseDrag), de ca 1 net ve chi tao ra 1 buoc undo duy nhat
    /// thay vi hang chuc buoc rieng le theo tung pixel.
    /// </summary>
    void PushUndoSnapshot()
    {
        undoStack.Add((Color[])pixels.Clone());

        if (undoStack.Count > maxHistorySize)
            undoStack.RemoveAt(0);

        // Bat dau thao tac moi -> xoa redo cu (dung hanh vi undo/redo chuan cua moi tool ve)
        redoStack.Clear();
    }

    void Undo()
    {
        if (undoStack.Count == 0) return;

        // Luu trang thai hien tai vao redo truoc khi phuc hoi lai trang thai cu
        redoStack.Add((Color[])pixels.Clone());

        Color[] previous = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);

        pixels = previous;
        ApplyPixels();
        Repaint();
    }

    void Redo()
    {
        if (redoStack.Count == 0) return;

        undoStack.Add((Color[])pixels.Clone());

        Color[] next = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);

        pixels = next;
        ApplyPixels();
        Repaint();
    }
    // >>> UNDO/REDO: ket thuc ==========================================

    void DrawToolbar()
    {
        EditorGUILayout.LabelField("Pixel Art Painter", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        int newW = EditorGUILayout.IntField("Width", canvasWidth);
        int newH = EditorGUILayout.IntField("Height", canvasHeight);
        if (GUILayout.Button("New / Resize", GUILayout.Width(110)))
        {
            if (EditorUtility.DisplayDialog("New Canvas",
                "Doi kich thuoc se xoa toan bo noi dung hien tai. Tiep tuc?", "Co", "Huy"))
            {
                NewCanvas(newW, newH);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        currentTool = (Tool)GUILayout.Toolbar((int)currentTool,
            new[] { "Brush", "Eraser", "Bucket", "Eyedrop" }, GUILayout.Height(24));
        brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 8);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        zoom = EditorGUILayout.Slider("Zoom", zoom, 4f, 32f);

        // >>> UNDO/REDO: nut Undo/Redo tren toolbar, tu disable khi stack rong
        using (new EditorGUI.DisabledScope(undoStack.Count == 0))
        {
            if (GUILayout.Button("Undo (Ctrl+Z)", GUILayout.Width(100))) Undo();
        }
        using (new EditorGUI.DisabledScope(redoStack.Count == 0))
        {
            if (GUILayout.Button("Redo (Ctrl+Y)", GUILayout.Width(100))) Redo();
        }

        if (GUILayout.Button("Clear All", GUILayout.Width(90)))
        {
            if (EditorUtility.DisplayDialog("Clear Canvas", "Xoa toan bo hinh ve hien tai?", "Co", "Huy"))
            {
                PushUndoSnapshot(); // cho phep undo lai thao tac Clear All
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0, 0, 0, 0);
                ApplyPixels();
            }
        }
        if (GUILayout.Button("Load PNG...", GUILayout.Width(90))) LoadFromPng();
        if (GUILayout.Button("Save As PNG...", GUILayout.Width(110))) SaveAsPng();
        EditorGUILayout.EndHorizontal();
    }

    void DrawPaletteColumn()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(170));
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        colorPalette = (PassengerColorPalette)EditorGUILayout.ObjectField(
            colorPalette, typeof(PassengerColorPalette), false);

        EditorGUILayout.Space(6);

        List<Color> swatches = GetSwatches();
        int columns = 4;
        int rowsCount = Mathf.CeilToInt(swatches.Count / (float)columns);

        for (int r = 0; r < rowsCount; r++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns; c++)
            {
                int idx = r * columns + c;
                if (idx >= swatches.Count) { GUILayout.Space(32); continue; }

                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = swatches[idx];

                var style = idx == selectedColorIndex ? EditorStyles.helpBox : GUI.skin.button;

                if (GUILayout.Button("", style, GUILayout.Width(32), GUILayout.Height(32)))
                {
                    selectedColorIndex = idx;
                    currentTool = Tool.Brush;
                }

                GUI.backgroundColor = prevColor;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        if (colorPalette == null)
        {
            EditorGUILayout.LabelField("Custom Colors (chua gan Palette)", EditorStyles.miniBoldLabel);
            customPickColor = EditorGUILayout.ColorField(customPickColor);
            if (GUILayout.Button("+ Add Color"))
            {
                customColors.Add(customPickColor);
                selectedColorIndex = customColors.Count - 1;
            }
            if (GUILayout.Button("Clear Custom Colors"))
            {
                customColors.Clear();
                selectedColorIndex = 0;
            }
        }

        EditorGUILayout.Space(10);
        if (selectedColorIndex >= 0 && selectedColorIndex < swatches.Count)
        {
            EditorGUILayout.LabelField("Dang chon:");
            Rect r = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
            EditorGUI.DrawRect(r, swatches[selectedColorIndex]);
        }

        EditorGUILayout.EndVertical();
    }

    List<Color> GetSwatches()
    {
        List<Color> result = new();

        if (colorPalette != null)
        {
            foreach (var pc in colorPalette.colors)
                if (pc != null) result.Add(pc.pixelColor);
        }
        else
        {
            result.AddRange(customColors);
        }

        return result;
    }

    void DrawCanvasArea()
    {
        EditorGUILayout.BeginVertical();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        float displayW = canvasWidth * zoom;
        float displayH = canvasHeight * zoom;

        Rect rect = GUILayoutUtility.GetRect(displayW, displayH,
            GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        GUI.DrawTexture(rect, canvasTexture, ScaleMode.StretchToFill, true);

        Handles.BeginGUI();
        Handles.color = new Color(0, 0, 0, 0.15f);
        for (int x = 0; x <= canvasWidth; x++)
        {
            float px = rect.x + x * zoom;
            Handles.DrawLine(new Vector3(px, rect.y), new Vector3(px, rect.y + displayH));
        }
        for (int y = 0; y <= canvasHeight; y++)
        {
            float py = rect.y + y * zoom;
            Handles.DrawLine(new Vector3(rect.x, py), new Vector3(rect.x + displayW, py));
        }
        Handles.EndGUI();

        HandleCanvasInput(rect);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void HandleCanvasInput(Rect rect)
    {
        Event e = Event.current;

        if (!rect.Contains(e.mousePosition)) return;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if (e.button != 0) return;

        int cx = Mathf.FloorToInt((e.mousePosition.x - rect.x) / zoom);

        // Toa do man hinh: 0 la TREN CUNG khung ve
        int screenRow = Mathf.FloorToInt((e.mousePosition.y - rect.y) / zoom);

        // >>> FIX (lat truc Y): bat dau ----------------------------------
        // Texture2D.SetPixels luu mang theo quy uoc index 0 = HANG DUOI CUNG
        // cua anh, nhung GUI.DrawTexture tu dong lat khi hien thi de anh hien
        // dung chieu (tren la tren, giong file PNG binh thuong). Neu dung
        // thang screenRow (0 = tren man hinh) lam index mang se bi lech nguoc:
        // click o TREN khung ve lai ghi vao hang GAN 0 cua mang, nhung hang do
        // lai hien thi ra o DUOI CUNG man hinh do co che lat cua DrawTexture.
        // Fix: dao nguoc truc truoc khi dung lam index mang.
        int cy = canvasHeight - 1 - screenRow;
        // >>> FIX (lat truc Y): ket thuc ----------------------------------

        if (cx < 0 || cx >= canvasWidth || cy < 0 || cy >= canvasHeight) return;

        // >>> UNDO/REDO: chi push snapshot 1 LAN duy nhat khi bat dau net ve
        // (MouseDown), khong push moi frame khi keo chuot (MouseDrag) - neu
        // khong ca net ve se bi chia thanh hang chuc buoc undo rieng le.
        if (e.type == EventType.MouseDown)
        {
            PushUndoSnapshot();
        }

        List<Color> swatches = GetSwatches();

        switch (currentTool)
        {
            case Tool.Brush:
                if (selectedColorIndex >= 0 && selectedColorIndex < swatches.Count)
                    PaintBrush(cx, cy, swatches[selectedColorIndex]);
                break;

            case Tool.Eraser:
                PaintBrush(cx, cy, new Color(0, 0, 0, 0));
                break;

            case Tool.Bucket:
                if (e.type == EventType.MouseDown)
                {
                    Color fillColor = (selectedColorIndex >= 0 && selectedColorIndex < swatches.Count)
                        ? swatches[selectedColorIndex] : new Color(0, 0, 0, 0);
                    FloodFill(cx, cy, fillColor);
                }
                break;

            case Tool.Eyedropper:
                if (e.type == EventType.MouseDown)
                {
                    Color picked = pixels[cy * canvasWidth + cx];
                    for (int i = 0; i < swatches.Count; i++)
                    {
                        if (ColorsApproxEqual(swatches[i], picked)) { selectedColorIndex = i; break; }
                    }
                }
                break;
        }

        e.Use();
        Repaint();
    }

    bool ColorsApproxEqual(Color a, Color b, float tol = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < tol && Mathf.Abs(a.g - b.g) < tol &&
               Mathf.Abs(a.b - b.b) < tol && Mathf.Abs(a.a - b.a) < tol;
    }

    void PaintBrush(int cx, int cy, Color color)
    {
        int half = brushSize / 2;
        bool changed = false;

        for (int dy = -half; dy <= half; dy++)
        {
            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x < 0 || x >= canvasWidth || y < 0 || y >= canvasHeight) continue;

                pixels[y * canvasWidth + x] = color;
                changed = true;
            }
        }

        if (changed) ApplyPixels();
    }

    void FloodFill(int startX, int startY, Color newColor)
    {
        int idx = startY * canvasWidth + startX;
        Color targetColor = pixels[idx];
        if (ColorsApproxEqual(targetColor, newColor)) return;

        Queue<int> queue = new();
        queue.Enqueue(idx);

        bool[] visited = new bool[pixels.Length];
        visited[idx] = true;

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            pixels[cur] = newColor;

            int x = cur % canvasWidth;
            int y = cur / canvasWidth;

            TryEnqueue(x - 1, y);
            TryEnqueue(x + 1, y);
            TryEnqueue(x, y - 1);
            TryEnqueue(x, y + 1);
        }

        ApplyPixels();

        void TryEnqueue(int nx, int ny)
        {
            if (nx < 0 || nx >= canvasWidth || ny < 0 || ny >= canvasHeight) return;
            int nIdx = ny * canvasWidth + nx;
            if (visited[nIdx]) return;
            if (!ColorsApproxEqual(pixels[nIdx], targetColor)) return;

            visited[nIdx] = true;
            queue.Enqueue(nIdx);
        }
    }

    void SaveAsPng()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Pixel Art", "pixel_art.png", "png",
            "Chon noi luu anh pixel art", "Assets");

        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllBytes(path, canvasTexture.EncodeToPNG());

        AssetDatabase.ImportAsset(path);
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        EditorUtility.DisplayDialog("Da luu", $"Da luu tai:\n{path}\n\nCo the dung ngay lam layoutImage.", "OK");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
    }

    void LoadFromPng()
    {
        string path = EditorUtility.OpenFilePanel("Load Pixel Art PNG", "Assets", "png");
        if (string.IsNullOrEmpty(path)) return;

        byte[] fileData = File.ReadAllBytes(path);
        Texture2D loaded = new Texture2D(2, 2);
        loaded.LoadImage(fileData);

        NewCanvas(loaded.width, loaded.height); // NewCanvas() da tu clear undo/redo stack
        pixels = loaded.GetPixels();
        ApplyPixels();
    }
}