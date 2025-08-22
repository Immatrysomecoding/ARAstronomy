using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;

public class OpenCVDebugVisualizer : MonoBehaviour
{
    [Header("Debug Display Settings")]
    public bool enableVisualization = true;
    public int debugWindowSize = 400;
    public float updateInterval = 0.5f; // Update display every 0.5 seconds

    [Header("Visualization Options")]
    public bool showOriginalMarker = true;
    public bool showGrayscaleMarker = true;
    public bool showCLAHEMarker = true;
    public bool showDenoisedMarker = true;
    public bool showSharpenedMarker = true;
    public bool showKeypointsMarker = true;
    public bool showCurrentFrame = true;
    public bool showProcessedFrame = true;
    public bool showMatches = true;

    // UI References
    private GameObject debugCanvas;
    private Dictionary<string, RawImage> debugWindows = new Dictionary<string, RawImage>();
    private Dictionary<string, Texture2D> debugTextures = new Dictionary<string, Texture2D>();

    // Position offsets for windows
    private int windowColumns = 3;
    private float windowSpacing = 10f;

    private static OpenCVDebugVisualizer instance;
    public static OpenCVDebugVisualizer Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<OpenCVDebugVisualizer>();
                if (instance == null)
                {
                    GameObject go = new GameObject("OpenCVDebugVisualizer");
                    instance = go.AddComponent<OpenCVDebugVisualizer>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(gameObject);

        CreateDebugCanvas();
    }

    void CreateDebugCanvas()
    {
        // Create a canvas for debug windows
        debugCanvas = new GameObject("OpenCV Debug Canvas");
        Canvas canvas = debugCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Make sure it's on top

        CanvasScaler scaler = debugCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        debugCanvas.AddComponent<GraphicRaycaster>();

        // Add a background panel
        GameObject bgPanel = new GameObject("Background Panel");
        bgPanel.transform.SetParent(debugCanvas.transform, false);
        Image bg = bgPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Initially hide the canvas
        debugCanvas.SetActive(enableVisualization);
    }

    public RawImage CreateDebugWindow(string name, int row, int col)
    {
        if (debugWindows.ContainsKey(name))
            return debugWindows[name];

        GameObject windowObj = new GameObject($"Debug_{name}");
        windowObj.transform.SetParent(debugCanvas.transform, false);

        // Add background
        Image bg = windowObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Add RawImage for displaying texture
        GameObject imageObj = new GameObject("Image");
        imageObj.transform.SetParent(windowObj.transform, false);
        RawImage rawImage = imageObj.AddComponent<RawImage>();

        // Position the window
        RectTransform rect = windowObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(debugWindowSize, debugWindowSize);

        float x = col * (debugWindowSize + windowSpacing) + windowSpacing;
        float y = -row * (debugWindowSize + windowSpacing) - windowSpacing;
        rect.anchoredPosition = new Vector2(x, y);

        // Setup image rect
        RectTransform imgRect = imageObj.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.sizeDelta = new Vector2(-10, -30); // Leave space for title
        imgRect.anchoredPosition = new Vector2(0, -25);

        // Add title text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(windowObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = name;
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 14;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = new Vector2(0, 25);
        titleRect.anchoredPosition = new Vector2(0, -12.5f);

        debugWindows[name] = rawImage;
        return rawImage;
    }

    public void ShowMat(string windowName, Mat mat, int row = 0, int col = 0)
    {
        if (!enableVisualization || mat == null || mat.empty())
            return;

        // Create or get the debug window
        RawImage rawImage = CreateDebugWindow(windowName, row, col);

        // Create or get texture
        if (!debugTextures.ContainsKey(windowName))
        {
            debugTextures[windowName] = new Texture2D(mat.cols(), mat.rows(), TextureFormat.RGBA32, false);
        }

        Texture2D texture = debugTextures[windowName];

        // Resize texture if needed
        if (texture.width != mat.cols() || texture.height != mat.rows())
        {
            texture.Reinitialize(mat.cols(), mat.rows());
        }

        // Convert Mat to Texture2D
        Mat displayMat = new Mat();

        // Convert to RGBA for display
        if (mat.channels() == 1)
        {
            Imgproc.cvtColor(mat, displayMat, Imgproc.COLOR_GRAY2RGBA);
        }
        else if (mat.channels() == 3)
        {
            Imgproc.cvtColor(mat, displayMat, Imgproc.COLOR_RGB2RGBA);
        }
        else if (mat.channels() == 4)
        {
            displayMat = mat.clone();
        }

        // Convert to texture and display
        Utils.matToTexture2D(displayMat, texture);
        rawImage.texture = texture;

        displayMat.Dispose();
    }

    public void ShowMatWithKeypoints(string windowName, Mat mat, MatOfKeyPoint keypoints, int row = 0, int col = 0)
    {
        if (!enableVisualization || mat == null || mat.empty())
            return;

        Mat display = new Mat();

        // Convert to color if grayscale
        if (mat.channels() == 1)
        {
            Imgproc.cvtColor(mat, display, Imgproc.COLOR_GRAY2RGB);
        }
        else
        {
            display = mat.clone();
        }

        // Draw keypoints
        Mat output = new Mat();
        Features2d.drawKeypoints(display, keypoints, output, new Scalar(0, 255, 0), Features2d.DrawMatchesFlags_DRAW_RICH_KEYPOINTS);

        ShowMat(windowName, output, row, col);

        display.Dispose();
        output.Dispose();
    }

    public void ShowMatches(string windowName, Mat img1, MatOfKeyPoint keypoints1,
                           Mat img2, MatOfKeyPoint keypoints2,
                           List<DMatch> matches, int row = 0, int col = 0)
    {
        if (!enableVisualization || img1 == null || img2 == null)
            return;

        Mat display1 = new Mat();
        Mat display2 = new Mat();

        // Convert to color if needed
        if (img1.channels() == 1)
            Imgproc.cvtColor(img1, display1, Imgproc.COLOR_GRAY2RGB);
        else
            display1 = img1.clone();

        if (img2.channels() == 1)
            Imgproc.cvtColor(img2, display2, Imgproc.COLOR_GRAY2RGB);
        else
            display2 = img2.clone();

        // Convert matches to MatOfDMatch
        MatOfDMatch matchesMat = new MatOfDMatch();
        matchesMat.fromList(matches);

        // Draw matches
        Mat output = new Mat();
        Features2d.drawMatches(display1, keypoints1, display2, keypoints2,
                              matchesMat, output,
                              new Scalar(0, 255, 0), new Scalar(255, 0, 0),
                              new MatOfByte(), Features2d.DrawMatchesFlags_DEFAULT);

        ShowMat(windowName, output, row, col);

        display1.Dispose();
        display2.Dispose();
        matchesMat.Dispose();
        output.Dispose();
    }

    public void ToggleVisualization()
    {
        enableVisualization = !enableVisualization;
        if (debugCanvas != null)
            debugCanvas.SetActive(enableVisualization);
    }

    public void ClearAll()
    {
        foreach (var window in debugWindows.Values)
        {
            if (window != null && window.gameObject != null)
                Destroy(window.gameObject.transform.parent.gameObject);
        }
        debugWindows.Clear();

        foreach (var texture in debugTextures.Values)
        {
            if (texture != null)
                Destroy(texture);
        }
        debugTextures.Clear();
    }

    void OnDestroy()
    {
        ClearAll();
        if (debugCanvas != null)
            Destroy(debugCanvas);
    }

    // Convenience method to show processing pipeline
    public void ShowProcessingPipeline(string prefix, Mat original, Mat gray, Mat clahe,
                                      Mat denoised, Mat sharpened, MatOfKeyPoint keypoints)
    {
        int row = prefix == "Marker" ? 0 : 2;

        if (showOriginalMarker && original != null)
            ShowMat($"{prefix}_Original", original, row, 0);

        if (showGrayscaleMarker && gray != null)
            ShowMat($"{prefix}_Grayscale", gray, row, 1);

        if (showCLAHEMarker && clahe != null)
            ShowMat($"{prefix}_CLAHE", clahe, row, 2);

        if (showDenoisedMarker && denoised != null)
            ShowMat($"{prefix}_Denoised", denoised, row + 1, 0);

        if (showSharpenedMarker && sharpened != null)
            ShowMat($"{prefix}_Sharpened", sharpened, row + 1, 1);

        if (showKeypointsMarker && gray != null && keypoints != null)
            ShowMatWithKeypoints($"{prefix}_Keypoints", gray, keypoints, row + 1, 2);
    }
}