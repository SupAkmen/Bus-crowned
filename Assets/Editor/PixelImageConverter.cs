// Assets/Editor/PixelImageConverter.cs
//
// Cach dung:
// 1. Dat file nay vao thu muc "Assets/Editor/"
// 2. Mo qua menu: Tools > Passenger Game > Pixel Image Converter
// 3. Keo anh nguon (Texture2D) vao "Source Image" (phai bat Read/Write Enabled,
//    tool se tu dong bat gium neu ban quen)
// 4. (Neu anh nen la mau dac, khong co alpha - vd anh AI-gen) Bat "Remove Background
//    (Flood Fill)" - tool se tu dong xoa vung nen noi lien voi bien ngoai cua anh,
//    GIU LAI cac vung mau gan trang/nhat nam BEN TRONG hinh (vd vien trang cua qua
//    dua hau) vi chung khong cham bien ngoai.
// 5. Nhap Target Width / Target Height
// 6. (Tuy chon) Bat "Snap To Palette" + keo PassengerColor[] vao de mau khop chinh xac
//    voi mau ban da khai bao trong game
// 7. Bam Convert, xem Preview, roi Save As PNG...

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PixelImageConverter : EditorWindow
{
    Texture2D sourceImage;
    int targetWidth = 32;
    int targetHeight = 32;

    bool snapToPalette = false;
    PassengerColor[] palette;
    SerializedObject serializedWindow;
    SerializedProperty paletteProperty;

    [Header("Background Removal")]
    bool removeBackgroundFloodFill = true;
    [Range(1, 100)] int backgroundTolerance = 40; // sai so mau (tong |dR|+|dG|+|dB|, thang 0-255 moi kenh) de coi la "cung mau nen"

    // >>> FIX (halo trang bao quanh cac mau khac): bat dau ----------------------
    [Range(0, 5)] int haloErodePixels = 1; // so lop pixel vien (anti-aliased) can "an mon" them sau flood fill
    // >>> FIX (halo trang bao quanh cac mau khac): ket thuc ----------------------

    // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): bat dau ----------------------
    [Header("Auto-Crop")]
    bool autoCropToContent = true;
    [Range(0, 20)] int cropPaddingPercent = 2; // them chut le nho de hinh khong dinh sat vien luoi
    // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): ket thuc ----------------------

    Texture2D previewTexture;
    Vector2 scrollPos;

    const float paletteMatchThreshold = 0.05f;

    [MenuItem("Tools/Passenger Game/Pixel Image Converter")]
    public static void ShowWindow()
    {
        var window = GetWindow<PixelImageConverter>("Pixel Image Converter");
        window.minSize = new Vector2(380, 520);
    }

    void OnEnable()
    {
        serializedWindow = new SerializedObject(this);
        paletteProperty = serializedWindow.FindProperty(nameof(palette));
    }

    void OnGUI()
    {
        serializedWindow.Update();

        EditorGUILayout.LabelField("1. Source Image", EditorStyles.boldLabel);
        sourceImage = (Texture2D)EditorGUILayout.ObjectField("Source Image", sourceImage, typeof(Texture2D), false);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("2. Remove Background", EditorStyles.boldLabel);
        removeBackgroundFloodFill = EditorGUILayout.Toggle("Remove Background (Flood Fill)", removeBackgroundFloodFill);

        if (removeBackgroundFloodFill)
        {
            backgroundTolerance = EditorGUILayout.IntSlider("Tolerance", backgroundTolerance, 1, 100);
            EditorGUILayout.HelpBox(
                "Xoa vung mau NOI LIEN voi bien ngoai cua anh (bat dau tu 4 canh), dua theo do " +
                "giong mau so voi MAU NEN THAM CHIEU (trung binh mau vien ngoai anh). Cac vung mau " +
                "gan giong nhung nam BEN TRONG hinh (vd vien trang cua qua dua hau, khong cham bien " +
                "ngoai truc tiep bang mau giong het nen) se it bi xoa nham hon so voi cach so sanh " +
                "pixel-lien-ke-voi-pixel-lien-ke truoc day.",
                MessageType.Info);

            // >>> FIX (halo trang bao quanh cac mau khac): bat dau ----------------------
            haloErodePixels = EditorGUILayout.IntSlider("Erode Halo (pixels)", haloErodePixels, 0, 5);
            EditorGUILayout.HelpBox(
                "Anh AI-gen thuong co vien anti-alias mo dan giua mau hinh va mau nen (1-2 pixel). " +
                "Vien do khong du giong nen de bi Flood Fill xoa, nhung lai qua nhat/kem bao hoa nen " +
                "khi so mau se bi snap nham thanh mau White, tao thanh 'vien trang' bao quanh moi vung " +
                "mau trong game. Tang gia tri nay de an mon them N lop pixel sat bien ngoai cung cua " +
                "moi vung mau (coi luon la nen), loai bo vien halo do. Neu hinh co nhung chi tiet mong " +
                "(dong 1-2px), tang qua cao co the lam mat chi tiet - nen bat dau tu 1.",
                MessageType.Info);
            // >>> FIX (halo trang bao quanh cac mau khac): ket thuc ----------------------

            // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): bat dau ----------------------
            EditorGUILayout.Space(4);
            autoCropToContent = EditorGUILayout.Toggle("Auto-Crop To Content", autoCropToContent);

            if (autoCropToContent)
            {
                cropPaddingPercent = EditorGUILayout.IntSlider("Crop Padding (%)", cropPaddingPercent, 0, 20);
            }

            EditorGUILayout.HelpBox(
                "Anh nguon (vd tu ChatGPT) thuong co rat nhieu khoang trang thua bao quanh hinh ve " +
                "chinh. Neu KHONG bat muc nay, toan bo canvas goc (ke ca khoang trang thua) se bi ep " +
                "vua khit vao luoi dich -> hinh ve chinh chi chiem mot phan nho o giua, con lai la o " +
                "trong/nen bao quanh rat day (trong dan den viec cac mau khong 'ra het' toi vien luoi). " +
                "BAT muc nay se tu dong cat sat (crop) ve dung vung bounding-box cua noi dung (sau khi " +
                "da xoa nen) truoc khi resize, giup hinh ve chiem gan het khung luoi dich.",
                MessageType.Info);
            // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): ket thuc ----------------------
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("3. Target Grid Size", EditorStyles.boldLabel);
        targetWidth = Mathf.Max(1, EditorGUILayout.IntField("Target Width (columns)", targetWidth));
        targetHeight = Mathf.Max(1, EditorGUILayout.IntField("Target Height (rows)", targetHeight));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("4. Snap Colors To Game Palette (Optional)", EditorStyles.boldLabel);
        snapToPalette = EditorGUILayout.Toggle("Snap To Palette", snapToPalette);

        if (snapToPalette)
        {
            EditorGUILayout.PropertyField(paletteProperty, new GUIContent("Palette (PassengerColor[])"), true);
            serializedWindow.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(
                "Moi pixel sau khi resize se duoc gan ve mau PassengerColor gan nhat trong palette.",
                MessageType.Info);
        }

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(sourceImage == null))
        {
            if (GUILayout.Button("Convert", GUILayout.Height(32)))
            {
                Convert();
            }
        }

        EditorGUILayout.Space(8);

        if (previewTexture != null)
        {
            EditorGUILayout.LabelField($"Preview ({previewTexture.width} x {previewTexture.height})", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(240));

            Rect rect = GUILayoutUtility.GetRect(
                previewTexture.width * PreviewScale(),
                previewTexture.height * PreviewScale());

            EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit, 0, 0, UnityEngine.Rendering.ColorWriteMask.All);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Save As PNG...", GUILayout.Height(28)))
            {
                SaveAsPng();
            }
        }
    }

    float PreviewScale()
    {
        int maxDim = Mathf.Max(previewTexture.width, previewTexture.height);
        if (maxDim <= 0) return 1f;
        float scale = 300f / maxDim;
        return Mathf.Clamp(scale, 4f, 24f);
    }

    /// <summary>
    /// Flood fill (BFS) tu tat ca pixel o bien ngoai (4 canh) cua anh, lan rong qua cac pixel
    /// lang gieng, danh dau la NEN (alpha = 0) neu du "giong" MAU NEN THAM CHIEU CO DINH.
    ///
    /// >>> FIX (color creep bug): ban truoc day so sanh moi pixel voi PIXEL LIEN KE TRUOC DO
    /// (current) thay vi voi mau nen goc. Dieu nay gay hien tuong "loang dan qua gradient":
    /// mien la moi buoc lech mau nho hon tolerance, flood fill se tiep tuc di xa dan, cong don
    /// sai so qua nhieu buoc, cuoi cung "leak" vao ca nhung vung mau khac han mau nen ban dau.
    ///
    /// Voi anh dua hau: dai vo trang/kem nam o goc duoi-trai va duoi-phai cua hinh tam giac
    /// (canh xieu cua hinh cat ngang qua dai vo), nen dai vo do CHAM TRUC TIEP vao nen trang
    /// ben ngoai, khong co vien sam mau nao ngan cach (khac voi phan thit do co vien nau sam
    /// chan flood fill lai). Vi mau vo trang gan voi mau nen trang, flood fill cu cach cu se
    /// loang thang tu nen vao dai vo, roi lan tiep doc theo toan bo dai vo (vi mau trong dai
    /// kha dong deu) - xoa sach ca dai vo du no nam "ben trong" hinh ve mat logic.
    ///
    /// FIX: so sanh MOI pixel ung vien voi MAU NEN THAM CHIEU CO DINH (trung binh mau o vien
    /// ngoai anh) thay vi so sanh voi pixel lien ke truoc do. Nho vay flood fill se dung dung
    /// khi gap mau thuc su khac nen (vd dai vo trang/kem khac mau trang thuan mot chut), thay
    /// vi loang lan dan qua chuoi cac pixel co gradient mo dan/leo thang tich luy sai so.
    /// </summary>
    Color[] RemoveBackgroundFloodFill(Color[] pixels, int width, int height, int toleranceInt)
    {
        Color[] result = (Color[])pixels.Clone();

        bool[] visited = new bool[width * height];
        Queue<int> queue = new Queue<int>();

        float tolerance = toleranceInt / 255f; // chuyen ve thang 0-1 de so sanh voi Color (0-1)

        Color bgReference = ComputeBackgroundReferenceColor(pixels, width, height);

        void TrySeed(int x, int y)
        {
            int idx = y * width + x;
            if (visited[idx]) return;
            visited[idx] = true;
            queue.Enqueue(idx);

            Color c = pixels[idx];
            result[idx] = new Color(c.r, c.g, c.b, 0f);
        }

        for (int x = 0; x < width; x++)
        {
            TrySeed(x, 0);
            TrySeed(x, height - 1);
        }
        for (int y = 0; y < height; y++)
        {
            TrySeed(0, y);
            TrySeed(width - 1, y);
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x = idx % width;
            int y = idx / width;

            TryExpand(x - 1, y);
            TryExpand(x + 1, y);
            TryExpand(x, y - 1);
            TryExpand(x, y + 1);

            void TryExpand(int nx, int ny)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;

                int nIdx = ny * width + nx;
                if (visited[nIdx]) return;

                visited[nIdx] = true; // danh dau da tham, tranh xu ly lai

                Color neighbour = pixels[nIdx];

                // So sanh voi mau nen THAM CHIEU CO DINH, khong phai pixel lien ke truoc do
                float dr = Mathf.Abs(neighbour.r - bgReference.r);
                float dg = Mathf.Abs(neighbour.g - bgReference.g);
                float db = Mathf.Abs(neighbour.b - bgReference.b);
                float dist = dr + dg + db;

                if (dist <= tolerance)
                {
                    result[nIdx] = new Color(neighbour.r, neighbour.g, neighbour.b, 0f);
                    queue.Enqueue(nIdx);
                }
                // Neu vuot tolerance -> day la bien cua hinh, dung lan tiep tu huong nay
            }
        }

        return result;
    }

    /// <summary>
    /// Lay mau nen tham chieu bang cach trung binh cac pixel nam tren 4 canh bien ngoai cua anh.
    /// Dung lam moc co dinh de so sanh, thay vi so sanh pixel-lien-ke-voi-pixel-lien-ke
    /// (cach cu bi "color creep" - loang dan qua cac gradient nho, cong don sai so).
    /// </summary>
    Color ComputeBackgroundReferenceColor(Color[] pixels, int width, int height)
    {
        float r = 0, g = 0, b = 0;
        int count = 0;

        void Sample(int x, int y)
        {
            Color c = pixels[y * width + x];
            r += c.r; g += c.g; b += c.b;
            count++;
        }

        for (int x = 0; x < width; x++)
        {
            Sample(x, 0);
            Sample(x, height - 1);
        }
        for (int y = 0; y < height; y++)
        {
            Sample(0, y);
            Sample(width - 1, y);
        }

        if (count == 0) return Color.white;

        return new Color(r / count, g / count, b / count);
    }

    // >>> FIX (halo trang bao quanh cac mau khac): bat dau ----------------------
    /// <summary>
    /// An mon (erode) N lop pixel sat vien ngoai cung cua vung "hinh ve" (alpha > 0),
    /// tuc la: pixel nao dang la hinh ve nhung LIEN KE TRUC TIEP voi mot pixel da la nen
    /// (alpha = 0) thi cung bi chuyen thanh nen luon.
    ///
    /// Ly do can buoc nay: RemoveBackgroundFloodFill chi xoa pixel du GIONG mau nen (trong
    /// tolerance). Nhung vien anti-alias giua mau hinh va mau nen (do AI-gen tu ve mo dan)
    /// thuong la mau BLEND nua voi - khong du giong nen de bi flood fill xoa, nhung lai qua
    /// nhat/kem bao hoa nen khi mapping sang PassengerColor se bi snap nham thanh White,
    /// tao ra "vien trang" bao quanh moi vung mau trong game.
    ///
    /// Erode blind (khong can so mau, chi can lien ke voi nen) se ăn mòn dung dung lop pixel
    /// mo/anti-alias do, vi chung luon nam sat ranh gioi voi nen.
    /// </summary>
    Color[] ErodeAlphaHalo(Color[] pixels, int width, int height, int iterations)
    {
        if (iterations <= 0) return pixels;

        Color[] current = (Color[])pixels.Clone();

        for (int iter = 0; iter < iterations; iter++)
        {
            Color[] next = (Color[])current.Clone();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    if (current[idx].a < 0.5f) continue; // da la nen roi, bo qua

                    bool touchesBackground =
                        (x > 0 && current[y * width + (x - 1)].a < 0.5f) ||
                        (x < width - 1 && current[y * width + (x + 1)].a < 0.5f) ||
                        (y > 0 && current[(y - 1) * width + x].a < 0.5f) ||
                        (y < height - 1 && current[(y + 1) * width + x].a < 0.5f);

                    if (touchesBackground)
                    {
                        Color c = current[idx];
                        next[idx] = new Color(c.r, c.g, c.b, 0f);
                    }
                }
            }

            current = next;
        }

        return current;
    }
    // >>> FIX (halo trang bao quanh cac mau khac): ket thuc ----------------------

    // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): bat dau ----------------------
    /// <summary>
    /// Tim bounding box (vung hinh chu nhat nho nhat) bao quanh tat ca cac pixel CON LA HINH VE
    /// (alpha > 0.5, tuc chua bi Flood Fill/Erode coi la nen). Dung de crop bo het khoang trang
    /// thua xung quanh truoc khi resize xuong luoi dich.
    /// </summary>
    bool TryGetContentBoundingBox(Color[] pixels, int width, int height, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = width;
        minY = height;
        maxX = -1;
        maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a > 0.5f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        return maxX >= minX && maxY >= minY; // co it nhat 1 pixel hinh ve
    }

    /// <summary>
    /// Cat (crop) mang pixel ve dung vung bounding box da tinh, co them mot chut le (padding)
    /// tuy theo cropPaddingPercent de hinh khong dinh sat vien luoi dich.
    /// </summary>
    Color[] CropToBoundingBox(Color[] pixels, int width, int height, int minX, int minY, int maxX, int maxY, int paddingPercent, out int newW, out int newH)
    {
        int boxW = maxX - minX + 1;
        int boxH = maxY - minY + 1;

        // Them le (padding) dua theo % kich thuoc bounding box, gioi han trong bien anh goc
        int padX = Mathf.RoundToInt(boxW * (paddingPercent / 100f));
        int padY = Mathf.RoundToInt(boxH * (paddingPercent / 100f));

        int cropMinX = Mathf.Clamp(minX - padX, 0, width - 1);
        int cropMinY = Mathf.Clamp(minY - padY, 0, height - 1);
        int cropMaxX = Mathf.Clamp(maxX + padX, 0, width - 1);
        int cropMaxY = Mathf.Clamp(maxY + padY, 0, height - 1);

        newW = cropMaxX - cropMinX + 1;
        newH = cropMaxY - cropMinY + 1;

        Color[] result = new Color[newW * newH];

        for (int y = 0; y < newH; y++)
        {
            for (int x = 0; x < newW; x++)
            {
                result[y * newW + x] = pixels[(y + cropMinY) * width + (x + cropMinX)];
            }
        }

        return result;
    }
    // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): ket thuc ----------------------

    void Convert()
    {
        if (sourceImage == null) return;

        EnsureReadable(sourceImage);

        Color[] srcPixels = sourceImage.GetPixels();
        int srcW = sourceImage.width;
        int srcH = sourceImage.height;

        // Chay flood fill xoa nen TREN ANH GOC (do phan giai day du) truoc khi resize,
        // vi bien hinh se sac net hon so voi chay sau khi da downsample xuong luoi nho
        if (removeBackgroundFloodFill)
        {
            srcPixels = RemoveBackgroundFloodFill(srcPixels, srcW, srcH, backgroundTolerance);

            // An mon them vien halo (anti-alias) sat bien ngoai cua hinh, tranh bi snap
            // nham thanh mau White khi mapping sang PassengerColor
            srcPixels = ErodeAlphaHalo(srcPixels, srcW, srcH, haloErodePixels);

            // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): bat dau ------------
            // Cat bo het khoang trang thua (da bi flood fill/erode coi la nen) xung quanh hinh,
            // chi giu lai dung vung noi dung + mot chut le nho, de khi resize xuong luoi dich
            // hinh ve chiem gan het khung thay vi bi thu nho lai vi con nguyen ca canvas goc.
            if (autoCropToContent && TryGetContentBoundingBox(srcPixels, srcW, srcH, out int minX, out int minY, out int maxX, out int maxY))
            {
                srcPixels = CropToBoundingBox(srcPixels, srcW, srcH, minX, minY, maxX, maxY, cropPaddingPercent, out int croppedW, out int croppedH);
                srcW = croppedW;
                srcH = croppedH;
            }
            // >>> FIX (anh co qua nhieu khoang trang thua xung quanh hinh): ket thuc ------------
        }

        Color[] destPixels = new Color[targetWidth * targetHeight];

        for (int ty = 0; ty < targetHeight; ty++)
        {
            for (int tx = 0; tx < targetWidth; tx++)
            {
                int sx = Mathf.Clamp((int)((tx + 0.5f) * srcW / targetWidth), 0, srcW - 1);
                int sy = Mathf.Clamp((int)((ty + 0.5f) * srcH / targetHeight), 0, srcH - 1);

                Color color = srcPixels[sy * srcW + sx];

                if (snapToPalette && palette != null && palette.Length > 0)
                {
                    color = SnapColor(color);
                }

                destPixels[ty * targetWidth + tx] = color;
            }
        }

        previewTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        previewTexture.filterMode = FilterMode.Point;
        previewTexture.SetPixels(destPixels);
        previewTexture.Apply();
    }

    Color SnapColor(Color pixel)
    {
        if (pixel.a < 0.1f) return new Color(0, 0, 0, 0); // giu trong suot cho nen (da danh dau boi flood fill hoac alpha goc)

        PassengerColor best = null;
        float bestDist = float.MaxValue;

        foreach (var pc in palette)
        {
            if (pc == null) continue;

            Color target = pc.pixelColor;
            float dr = pixel.r - target.r;
            float dg = pixel.g - target.g;
            float db = pixel.b - target.b;
            float dist = dr * dr + dg * dg + db * db;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = pc;
            }
        }

        if (best == null || bestDist > paletteMatchThreshold)
        {
            return new Color(0, 0, 0, 0);
        }

        Color snapped = best.pixelColor;
        snapped.a = 1f;
        return snapped;
    }

    void EnsureReadable(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(path)) return;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    void SaveAsPng()
    {
        if (previewTexture == null) return;

        string defaultName = sourceImage != null ? sourceImage.name + "_converted.png" : "layout_converted.png";
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Converted Image",
            defaultName,
            "png",
            "Chon noi luu anh da convert",
            "Assets");

        if (string.IsNullOrEmpty(path)) return;

        byte[] pngBytes = previewTexture.EncodeToPNG();
        File.WriteAllBytes(path, pngBytes);

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

        EditorUtility.DisplayDialog("Da luu", $"Da luu anh tai:\n{path}\n\nAnh da co alpha channel dung (nen = trong suot, hinh ve = dac), san sang dung lam layoutImage.", "OK");

        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
    }
}