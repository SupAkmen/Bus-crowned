using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PassengerSpawnAreas : MonoBehaviour
{
    [Header("Spawn Area")]
    public Transform startPoint;

    public float width = 8f;
    public float height = 10f;

    public float spaceX = 1f;
    public float spaceZ = 1f;

    [Header("Prefab")]
    public Passenger passengerPrefab;

    [Header("Passenger Color")]
    public List<PassengerColor> colors;

    [Header("ClusterColor")]
    [SerializeField] int minClusterSize = 4;
    [SerializeField] int maxClusterSize = 8;
    Dictionary<GridNode, PassengerColor> colorMap = new();

    // >>> MOI THEM: bat dau ==========================================
    [Header("Image Layout")]
    [Tooltip("Neu gan anh vao day, se spawn theo anh thay vi random cluster")]
    public Texture2D layoutImage;

    [Tooltip("Neu bat, pixel se duoc snap ve mau gan nhat trong 'colors' thay vi doi khop tuyet doi voi pixelColor")]
    public bool snapToNearestColor = true;

    // >>> THAY DOI (v2 - Hue matching): bat dau ----------------------
    // Truoc day dung khoang cach RGB Euclidean (colorMatchThreshold ~0.05), nhung anh AI-generated
    // co shading (do sang/toi) lam mau thuc te lech xa mau "chuan" da khai bao trong pixelColor,
    // khien nhieu pixel (vd vo dua xanh) khong khop duoc mau nao. Chuyen sang so sanh trong khong
    // gian HSV co trong so: Hue (tong mau) la yeu to chinh, Saturation dung de phan biet mau
    // xam/den/trang voi mau ruc ro (giai quyet viec den va do thuan deu co Hue = 0 nen de bi lam
    // nhau), Value (do sang) chi la trong so phu vi day chinh la thu shading thay doi nhieu nhat.
    [Header("Hue Matching Weights")]
    [Tooltip("Trong so cho sai lech Hue (tong mau) - nen de cao nhat, day la yeu to quyet dinh chinh")]
    public float hueWeight = 4f;

    [Tooltip("Trong so cho sai lech Saturation (do bao hoa) - dung de tach mau den/trang/xam voi mau ruc ro")]
    public float saturationWeight = 1.2f;

    [Tooltip("Trong so cho sai lech Value (do sang) - de thap vi day la thu bi shading anh huong nhieu nhat, can du nhan (tolerant)")]
    public float valueWeight = 0.3f;

    [UnityEngine.Range(0f, 0.5f)]
    [Tooltip("Duoi nguong nay coi la mau 'khong mau' (den/trang/xam) - dung Value de so sanh thay vi Hue")]
    public float achromaticSaturationThreshold = 0.15f;

    [UnityEngine.Range(0f, 1f)]
    [Tooltip("Khoang cach HSV co trong so toi da de con duoc coi la khop mau. Vuot qua nguong nay -> bo qua o do (khong spawn)")]
    public float colorMatchThreshold = 0.35f;

    [Header("Background Detection")]
    // >>> THAY DOI (v3 - fix mat vien trang): bat dau ----------------------
    // Neu anh da duoc xu ly qua PixelImageConverter voi "Remove Background (Flood Fill)",
    // alpha da duoc bake DUNG (nen = 0, hinh ve = 1), ke ca vien trang/cream nam trong hinh.
    // Khi do NEN TAT muc nay, vi loc theo V/S se lai xoa nham vien trang do gia tri V/S cua no
    // giong het mau nen (khong the phan biet bang mau don thuan, chi phan biet duoc bang vi tri/alpha).
    // Chi BAT muc nay neu anh cua ban KHONG co alpha dung (anh nen dac, chua qua flood-fill).
    [Tooltip("BAT: dung them V/S de loc nen (danh cho anh chua co alpha dung, nen dac mau trang). TAT: chi dua vao alpha cua anh (khuyen dung neu da convert qua PixelImageConverter voi Remove Background)")]
    public bool useValueSaturationBackgroundFilter = false;
    // >>> THAY DOI (v3 - fix mat vien trang): ket thuc ----------------------
    [Tooltip("Pixel co Value tren nguong nay VA Saturation duoi nguong duoi day se bi coi la nen (khong spawn), du du la anh nen trang hay nen trong suot")]
    [UnityEngine.Range(0f, 1f)] public float backgroundValueThreshold = 0.92f;
    [UnityEngine.Range(0f, 1f)] public float backgroundSaturationThreshold = 0.10f;
    // >>> THAY DOI (v2 - Hue matching): ket thuc ----------------------

    int columns;
    int rows;

    // Sửa trong file PassengerSpawnAreas.cs
    private void Start()
    {
        rows = Mathf.FloorToInt(height / spaceZ);
        columns = Mathf.FloorToInt(width / spaceX);

        // >>> BẮT ĐẦU SỬA: Lấy Layout động từ LevelManager
        if (LevelManager.Instance != null && LevelManager.Instance.GetCurrentLevelConfig() != null)
        {
            layoutImage = LevelManager.Instance.GetCurrentLevelConfig().layoutImage;
        }
        // >>> KẾT THÚC SỬA

        if (layoutImage != null)
            SpawnFromImage();
        else
            SpawnPassengers();
    }


    void SpawnPassengers()
    {
        colorMap.Clear();

        // Grid cuc bo cho khu vuc spawn, index theo (r,c) cua khu vuc nay
        // (khac voi row/column cua PassengerGrid toan cuc)
        GridNode[,] localGrid = new GridNode[rows, columns];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 pos = startPoint.position + new Vector3(c * spaceX, 0.5f, -r * spaceZ);

                GridNode node = PassengerGrid.Instance.GetNearestNode(pos);

                // node da bi chiem -> bo qua de khong 2 passenger chong len 1 o
                if (!node.walkable) continue;

                localGrid[r, c] = node;
            }
        }


        GenerateClusters(localGrid);

        SpawnFromColorMap();
    }

    // >>> MOI THEM: bat dau ==========================================
    /// <summary>
    /// Doc layoutImage, resize (Nearest-Neighbor) khop dung so hang/cot cua khu spawn hien tai,
    /// roi anh xa moi pixel sang PassengerColor gan nhat (hoac khop tuyet doi) de dien vao colorMap.
    /// </summary>
    void SpawnFromImage()
    {
        colorMap.Clear();

        // Grid cuc bo, giong het logic trong SpawnPassengers() de dam bao toa do khop voi grid toan cuc
        GridNode[,] localGrid = new GridNode[rows, columns];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 pos = startPoint.position + new Vector3(c * spaceX, 0.5f, -r * spaceZ);

                GridNode node = PassengerGrid.Instance.GetNearestNode(pos);

                if (!node.walkable) continue;

                localGrid[r, c] = node;
            }
        }

        int srcW = layoutImage.width;
        int srcH = layoutImage.height;
        Color[] srcPixels = layoutImage.GetPixels();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (localGrid[r, c] == null) continue;

                // Anh xa (r,c) cua grid -> toa do pixel tuong ung trong anh goc (Nearest-Neighbor)
                int sx = Mathf.Clamp((int)((c + 0.5f) * srcW / columns), 0, srcW - 1);

                // >>> FIX (lat doc / upside-down): bat dau ----------------------
                // BUG CU: sy = (r + 0.5f) * srcH / rows -> r=0 (hang TREN CUNG cua vung spawn,
                // vi pos.z giam dan khi r tang) bi map thang sang sy=0.
                //
                // Nhung Texture2D.GetPixels() cua Unity luu mang pixel theo chieu TU DUOI LEN
                // TREN (index 0 = goc duoi-trai cua anh, khac voi file anh thong thuong luu tu
                // tren xuong). Nen sy=0 thuc ra la DAY anh goc, khong phai DINH anh goc.
                //
                // -> Hang tren cung cua vung spawn (r=0) bi lay nham mau o day anh goc, va hang
                // duoi cung (r=rows-1) bi lay nham mau o dinh anh goc => ca hinh bi lat doc 180
                // do (vd: dinh nhon dua hau von o tren lai bi day xuong duoi, vien xanh von o
                // duoi lai bi day len tren).
                //
                // FIX: dao nguoc truc r khi tinh sy, de r=0 (tren cung vung spawn) tuong ung voi
                // DINH anh goc (tuc gan srcH - 1 trong mang luu tu-duoi-len-tren cua Unity).
                int sy = Mathf.Clamp((int)((rows - r - 0.5f) * srcH / rows), 0, srcH - 1);
                // >>> FIX (lat doc / upside-down): ket thuc ----------------------

                Color pixel = srcPixels[sy * srcW + sx];

                if (pixel.a < 0.1f) continue; // pixel trong suot -> khong spawn o nay (alpha da duoc PixelImageConverter bake dung neu co qua Flood Fill)

                // >>> THAY DOI (v3): chi loc theo V/S neu BAT (danh cho anh chua co alpha dung).
                // Neu anh da qua PixelImageConverter voi Remove Background, KHONG bat muc nay,
                // vi se xoa nham vien trang/cream nam trong hinh (vd vien dua hau).
                if (useValueSaturationBackgroundFilter)
                {
                    Color.RGBToHSV(pixel, out float pixH, out float pixS, out float pixV);
                    if (pixV > backgroundValueThreshold && pixS < backgroundSaturationThreshold)
                        continue; // trang/gan trang, coi la nen -> khong spawn
                }
                // >>> ket thuc phan them

                PassengerColor matched = snapToNearestColor
                    ? FindClosestColorByHue(pixel)
                    : FindExactColor(pixel);

                if (matched == null) continue; // khong khop mau nao trong danh sach colors -> bo qua o nay

                colorMap[localGrid[r, c]] = matched;
            }
        }

        SpawnFromColorMap();
    }

    // >>> THAY DOI (v2 - Hue matching): bat dau ==========================================
    /// <summary>
    /// Tim PassengerColor gan nhat voi pixel dau vao bang khoang cach HSV co trong so
    /// (Hue la chinh, Saturation de tach mau xam/den/trang, Value chi la phu vi bi shading anh huong nhieu).
    /// Neu khoang cach vuot colorMatchThreshold, coi nhu khong khop mau nao (tra ve null).
    /// </summary>
    PassengerColor FindClosestColorByHue(Color pixel)
    {
        Color.RGBToHSV(pixel, out float pixHue, out float pixSat, out float pixVal);
        bool pixelIsAchromatic = pixSat < achromaticSaturationThreshold;

        PassengerColor best = null;
        float bestDist = float.MaxValue;

        foreach (var pc in colors)
        {
            if (pc == null) continue;

            Color.RGBToHSV(pc.pixelColor, out float palHue, out float palSat, out float palVal);
            bool palIsAchromatic = palSat < achromaticSaturationThreshold;

            float hueDiff;

            if (pixelIsAchromatic && palIsAchromatic)
            {
                // Ca 2 deu la mau xam/den/trang -> Hue khong co y nghia, bo qua Hue
                hueDiff = 0f;
            }
            else if (pixelIsAchromatic != palIsAchromatic)
            {
                // Mot ben co mau, mot ben khong mau -> chac chan khong khop, phat nang
                hueDiff = 0.5f;
            }
            else
            {
                // Ca 2 deu la mau co sac -> so Hue theo vong tron (0 va 1 la lien ke nhau)
                float diff = Mathf.Abs(pixHue - palHue);
                hueDiff = Mathf.Min(diff, 1f - diff);
            }

            float satDiff = pixSat - palSat;
            float valDiff = pixVal - palVal;

            float dist = hueWeight * hueDiff * hueDiff
                       + saturationWeight * satDiff * satDiff
                       + valueWeight * valDiff * valDiff;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = pc;
            }
        }

        return bestDist <= colorMatchThreshold ? best : null;
    }
    // >>> THAY DOI (v2 - Hue matching): ket thuc ==========================================

    /// <summary>
    /// Doi khop tuyet doi (trong pham vi sai so nho) thay vi lay mau gan nhat.
    /// Dung khi ban muon chi nhung pixel dung chinh xac mau da khai bao moi duoc spawn.
    /// </summary>
    PassengerColor FindExactColor(Color pixel)
    {
        const float exactTolerance = 0.02f;

        foreach (var pc in colors)
        {
            if (pc == null) continue;

            if (Mathf.Abs(pixel.r - pc.pixelColor.r) < exactTolerance &&
                Mathf.Abs(pixel.g - pc.pixelColor.g) < exactTolerance &&
                Mathf.Abs(pixel.b - pc.pixelColor.b) < exactTolerance)
            {
                return pc;
            }
        }

        return null;
    }

    /// <summary>
    /// Vong lap spawn dung chung cho ca 2 che do (random cluster va tu anh),
    /// tach rieng ra tu SpawnPassengers() cu de khong bi trung code.
    /// </summary>
    void SpawnFromColorMap()
    {
        foreach (var pair in colorMap)
        {
            GridNode node = pair.Key;

            Vector3 spawnPos = node.worldPosition;
            spawnPos.y = startPoint.position.y;

            Passenger p = Instantiate(passengerPrefab, spawnPos, Quaternion.identity, startPoint);

            p.SetColor(pair.Value);

            PassengerManager.Instance.Register(p);

            node.occupant = p;
            node.walkable = false;

            p.CurrentNode = node;
        }
    }
    // >>> MOI THEM: ket thuc ==========================================

    /// <summary>
    /// Quet qua tung o cua khu vuc spawn theo thu tu hang/cot.
    /// Voi moi o con trong, "phat trien" mot hinh chu nhat (width x height)
    /// bat dau tu o do de tao thanh 1 cluster mot mau duy nhat.
    /// </summary>
    void GenerateClusters(GridNode[,] localGrid)
    {
        bool[,] used = new bool[rows, columns];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (used[r, c]) continue;
                if (localGrid[r, c] == null) continue;

                // Chon kich thuoc muc tieu cho cluster nay, co dien tich nam trong [minClusterSize, maxClusterSize]
                int targetArea = Random.Range(minClusterSize, maxClusterSize + 1);

                int targetWidth = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(targetArea)));
                int targetHeight = Mathf.Max(1, Mathf.CeilToInt((float)targetArea / targetWidth));

                // Doi ngau nhien chieu rong/cao de cluster khong bi lap lai mot kieu hinh
                if (Random.value < 0.5f)
                {
                    (targetWidth, targetHeight) = (targetHeight, targetWidth);
                }

                Vector2Int actualSize = GetActualRectSize(localGrid, used, r, c, targetWidth, targetHeight);

                if (actualSize.x <= 0 || actualSize.y <= 0) continue;

                PassengerColor color = colors[Random.Range(0, colors.Count)];

                for (int dr = 0; dr < actualSize.y; dr++)
                {
                    for (int dc = 0; dc < actualSize.x; dc++)
                    {
                        int nr = r + dr;
                        int nc = c + dc;

                        used[nr, nc] = true;
                        colorMap[localGrid[nr, nc]] = color;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tinh kich thuoc hinh chu nhat thuc te co the tao duoc bat dau tu (r,c),
    /// gioi han boi maxWidth/maxHeight mong muon, bien khu vuc spawn va cac o da dung/khong the di chuyen.
    /// </summary>
    Vector2Int GetActualRectSize(GridNode[,] localGrid, bool[,] used, int r, int c, int maxWidth, int maxHeight)
    {
        // Tinh chieu rong toi da co the dung tu (r,c) tren cung 1 hang
        int width = 0;
        while (width < maxWidth &&
               c + width < columns &&
               localGrid[r, c + width] != null &&
               !used[r, c + width])
        {
            width++;
        }

        if (width == 0) return Vector2Int.zero;

        // Tinh chieu cao toi da: moi hang them vao phai co du "width" o hop le
        int height = 1;
        while (height < maxHeight)
        {
            int nr = r + height;

            if (nr >= rows) break;

            bool rowValid = true;

            for (int dc = 0; dc < width; dc++)
            {
                int nc = c + dc;

                if (localGrid[nr, nc] == null || used[nr, nc])
                {
                    rowValid = false;
                    break;
                }
            }

            if (!rowValid) break;

            height++;
        }

        return new Vector2Int(width, height);
    }
}