using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.PhotoModule;
using System.IO;

// Add this script to your scene and call ProcessAndSaveMarkerSteps() to debug
public class MarkerProcessingDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public string saveFolder = "MarkerDebugOutput";
    public bool autoProcessOnStart = true;

    [Header("Reference to Camera Manager")]
    public OpenCVCameraManager cameraManager;

    // SIFT settings (copy from your main settings)
    private SIFT siftDetector;
    private CLAHE clahe;

    void Start()
    {
        if (autoProcessOnStart)
        {
            StartCoroutine(ProcessAfterDelay());
        }
    }

    IEnumerator ProcessAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        ProcessAndSaveMarkerSteps();
    }

    [ContextMenu("Process and Save Marker Steps")]
    public void ProcessAndSaveMarkerSteps()
    {
        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<OpenCVCameraManager>();
            if (cameraManager == null)
            {
                Debug.LogError("OpenCVCameraManager not found!");
                return;
            }
        }

        // Create output directory
        string outputPath = Path.Combine(Application.persistentDataPath, saveFolder);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        Debug.Log($"=== SAVING DEBUG IMAGES TO: {outputPath} ===");

        // Initialize OpenCV components
        InitializeDebugComponents();

        // Process each marker target
        foreach (var target in cameraManager.markerTargets)
        {
            if (target.markerImage != null)
            {
                ProcessSingleMarker(target, outputPath);
            }
        }

        Debug.Log($"=== PROCESSING COMPLETE! Check folder: {outputPath} ===");

        // Open folder in file explorer (Windows)
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        System.Diagnostics.Process.Start("explorer.exe", outputPath.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        System.Diagnostics.Process.Start("open", outputPath);
#endif
    }

    void InitializeDebugComponents()
    {
        // Initialize SIFT with same settings as main camera manager
        siftDetector = SIFT.create(
            nfeatures: cameraManager.nFeatures,
            nOctaveLayers: cameraManager.nOctaveLayers,
            contrastThreshold: cameraManager.contrastThreshold,
            edgeThreshold: cameraManager.edgeThreshold,
            sigma: cameraManager.sigma
        );

        // Initialize CLAHE
        clahe = Imgproc.createCLAHE(cameraManager.claheClipLimit, new Size(8, 8));
    }

    void ProcessSingleMarker(OpenCVMarkerTarget target, string outputPath)
    {
        Debug.Log($"\n========================================");
        Debug.Log($"Processing Marker: {target.targetName}");
        Debug.Log($"========================================");

        string markerFolder = Path.Combine(outputPath, target.targetName.Replace(" ", "_"));
        if (!Directory.Exists(markerFolder))
        {
            Directory.CreateDirectory(markerFolder);
        }

        try
        {
            // Step 1: Load original image
            Mat originalMat = new Mat(target.markerImage.height, target.markerImage.width, CvType.CV_8UC4);
            Utils.texture2DToMat(target.markerImage, originalMat);
            SaveMatToFile(originalMat, Path.Combine(markerFolder, "01_Original.png"));
            Debug.Log($"✓ Step 1: Original image - Size: {originalMat.width()}x{originalMat.height()}, Channels: {originalMat.channels()}");

            // Step 2: Convert to grayscale
            Mat grayMat = new Mat();
            Imgproc.cvtColor(originalMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
            SaveMatToFile(grayMat, Path.Combine(markerFolder, "02_Grayscale.png"));
            AnalyzeImage(grayMat, "Grayscale");

            // Step 3: Apply CLAHE
            Mat claheMat = new Mat();
            if (cameraManager.enableCLAHE)
            {
                clahe.apply(grayMat, claheMat);
                SaveMatToFile(claheMat, Path.Combine(markerFolder, "03_CLAHE.png"));
                AnalyzeImage(claheMat, "CLAHE");
            }
            else
            {
                claheMat = grayMat.clone();
                Debug.Log("✗ Step 3: CLAHE disabled");
            }

            // Step 4: Apply Denoising
            Mat denoisedMat = new Mat();
            if (cameraManager.enableDenoising)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Photo.fastNlMeansDenoising(claheMat, denoisedMat, cameraManager.denoisingH, 7, 21);
                sw.Stop();
                SaveMatToFile(denoisedMat, Path.Combine(markerFolder, "04_Denoised.png"));
                Debug.Log($"✓ Step 4: Denoising applied - Time: {sw.ElapsedMilliseconds}ms");
                AnalyzeImage(denoisedMat, "Denoised");
            }
            else
            {
                denoisedMat = claheMat.clone();
                Debug.Log("✗ Step 4: Denoising disabled");
            }

            // Step 5: Apply Sharpening
            Mat sharpenedMat = new Mat();
            if (cameraManager.enableSharpening && cameraManager.sharpeningAmount > 0)
            {
                Mat blurred = new Mat();
                Imgproc.GaussianBlur(denoisedMat, blurred, new Size(0, 0), 3);
                Core.addWeighted(denoisedMat, 1.0 + cameraManager.sharpeningAmount,
                               blurred, -cameraManager.sharpeningAmount, 0, sharpenedMat);
                blurred.Dispose();

                SaveMatToFile(sharpenedMat, Path.Combine(markerFolder, "05_Sharpened.png"));
                AnalyzeImage(sharpenedMat, "Sharpened");
            }
            else
            {
                sharpenedMat = denoisedMat.clone();
                Debug.Log("✗ Step 5: Sharpening disabled");
            }

            // Step 6: Detect SIFT keypoints
            Mat finalProcessed = sharpenedMat.clone();
            MatOfKeyPoint keypoints = new MatOfKeyPoint();
            Mat descriptors = new Mat();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            siftDetector.detectAndCompute(finalProcessed, new Mat(), keypoints, descriptors);
            sw2.Stop();

            Debug.Log($"\n✓ Step 6: SIFT Detection - Time: {sw2.ElapsedMilliseconds}ms");
            Debug.Log($"  • Keypoints found: {keypoints.toArray().Length}");
            Debug.Log($"  • Descriptor dimensions: {descriptors.rows()} x {descriptors.cols()}");

            // Draw and save keypoints
            Mat keypointMat = new Mat();
            Features2d.drawKeypoints(finalProcessed, keypoints, keypointMat,
                                   new Scalar(0, 255, 0),
                                   Features2d.DrawMatchesFlags_DRAW_RICH_KEYPOINTS);
            SaveMatToFile(keypointMat, Path.Combine(markerFolder, "06_Keypoints.png"));

            // Analyze keypoint distribution
            AnalyzeKeypoints(keypoints, markerFolder);

            // Save processing summary
            SaveProcessingSummary(target, keypoints, markerFolder);

            // Cleanup
            originalMat.Dispose();
            grayMat.Dispose();
            claheMat.Dispose();
            denoisedMat.Dispose();
            sharpenedMat.Dispose();
            finalProcessed.Dispose();
            keypoints.Dispose();
            descriptors.Dispose();
            keypointMat.Dispose();

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing marker {target.targetName}: {e.Message}");
        }
    }

    void SaveMatToFile(Mat mat, string filePath)
    {
        try
        {
            // Convert to RGBA if needed
            Mat saveableMat = new Mat();
            if (mat.channels() == 1)
                Imgproc.cvtColor(mat, saveableMat, Imgproc.COLOR_GRAY2RGBA);
            else if (mat.channels() == 3)
                Imgproc.cvtColor(mat, saveableMat, Imgproc.COLOR_RGB2RGBA);
            else
                saveableMat = mat.clone();

            // Convert to Texture2D
            Texture2D texture = new Texture2D(saveableMat.width(), saveableMat.height(),
                                             TextureFormat.RGBA32, false);
            Utils.matToTexture2D(saveableMat, texture);

            // Save as PNG
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            Debug.Log($"  → Saved: {Path.GetFileName(filePath)}");

            // Cleanup
            saveableMat.Dispose();
            Destroy(texture);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save image: {e.Message}");
        }
    }

    void AnalyzeImage(Mat grayImage, string stepName)
    {
        try
        {
            // Calculate mean and standard deviation
            MatOfDouble mean = new MatOfDouble();
            MatOfDouble stdDev = new MatOfDouble();
            Core.meanStdDev(grayImage, mean, stdDev);

            // Get min and max values
            Core.MinMaxLocResult minMax = Core.minMaxLoc(grayImage);

            Debug.Log($"✓ Step {stepName} Analysis:");
            Debug.Log($"  • Mean: {mean.get(0, 0)[0]:F2}, StdDev: {stdDev.get(0, 0)[0]:F2}");
            Debug.Log($"  • Min: {minMax.minVal:F2}, Max: {minMax.maxVal:F2}");
            Debug.Log($"  • Contrast ratio: {(minMax.maxVal - minMax.minVal):F2}");

            mean.Dispose();
            stdDev.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error analyzing image: {e.Message}");
        }
    }

    void AnalyzeKeypoints(MatOfKeyPoint keypoints, string outputFolder)
    {
        KeyPoint[] kpArray = keypoints.toArray();

        if (kpArray.Length == 0)
        {
            Debug.LogWarning("  ⚠ No keypoints detected!");
            return;
        }

        float minResponse = float.MaxValue;
        float maxResponse = float.MinValue;
        float avgResponse = 0;
        float minSize = float.MaxValue;
        float maxSize = float.MinValue;

        foreach (var kp in kpArray)
        {
            minResponse = Mathf.Min(minResponse, kp.response);
            maxResponse = Mathf.Max(maxResponse, kp.response);
            avgResponse += kp.response;
            minSize = Mathf.Min(minSize, kp.size);
            maxSize = Mathf.Max(maxSize, kp.size);
        }
        avgResponse /= kpArray.Length;

        Debug.Log($"\n  Keypoint Statistics:");
        Debug.Log($"  • Response - Min: {minResponse:F4}, Max: {maxResponse:F4}, Avg: {avgResponse:F4}");
        Debug.Log($"  • Size - Min: {minSize:F2}, Max: {maxSize:F2}");
    }

    void SaveProcessingSummary(OpenCVMarkerTarget target, MatOfKeyPoint keypoints, string outputFolder)
    {
        string summaryPath = Path.Combine(outputFolder, "processing_summary.txt");

        List<string> lines = new List<string>();
        lines.Add($"Processing Summary for: {target.targetName}");
        lines.Add($"Generated: {System.DateTime.Now}");
        lines.Add("=====================================");
        lines.Add("");
        lines.Add("Settings Used:");
        lines.Add($"  SIFT nFeatures: {cameraManager.nFeatures}");
        lines.Add($"  SIFT contrastThreshold: {cameraManager.contrastThreshold}");
        lines.Add($"  SIFT edgeThreshold: {cameraManager.edgeThreshold}");
        lines.Add($"  CLAHE Enabled: {cameraManager.enableCLAHE}");
        lines.Add($"  CLAHE Clip Limit: {cameraManager.claheClipLimit}");
        lines.Add($"  Denoising Enabled: {cameraManager.enableDenoising}");
        lines.Add($"  Sharpening Enabled: {cameraManager.enableSharpening}");
        lines.Add($"  Sharpening Amount: {cameraManager.sharpeningAmount}");
        lines.Add("");
        lines.Add("Results:");
        lines.Add($"  Total Keypoints Detected: {keypoints.toArray().Length}");
        lines.Add($"  Minimum Required: {target.minMatches}");
        lines.Add($"  Status: {(keypoints.toArray().Length >= target.minMatches ? "PASS ✓" : "FAIL ✗")}");

        File.WriteAllLines(summaryPath, lines.ToArray());
        Debug.Log($"  → Summary saved to: processing_summary.txt");
    }

    void OnDestroy()
    {
        siftDetector?.Dispose();
        clahe?.Dispose();
    }
}