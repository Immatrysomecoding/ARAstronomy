using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.PhotoModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.VideoModule;
using System.IO;
using System.Linq;

// Add this script to debug camera frame processing
public class CameraFrameDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public string saveFolder = "CameraFrameDebug";
    public bool captureOnKeyPress = true; // Press 'C' to capture
    public KeyCode captureKey = KeyCode.C;
    public bool autoCapture = false;
    public float autoCaptureDelay = 3f;

    [Header("Reference")]
    public OpenCVCameraManager cameraManager;

    // OpenCV components (copied from camera manager)
    private SIFT siftDetector;
    private FlannBasedMatcher flannMatcher;
    private CLAHE clahe;

    // For storing captured frame
    private bool isProcessing = false;

    void Start()
    {
        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<OpenCVCameraManager>();
        }

        if (autoCapture)
        {
            StartCoroutine(AutoCaptureAfterDelay());
        }

        Debug.Log($"=== CAMERA FRAME DEBUGGER READY ===");
        Debug.Log($"Press '{captureKey}' to capture and process a camera frame");
    }

    IEnumerator AutoCaptureAfterDelay()
    {
        yield return new WaitForSeconds(autoCaptureDelay);
        CaptureAndProcessFrame();
    }

    void Update()
    {
        if (captureOnKeyPress && Input.GetKeyDown(captureKey) && !isProcessing)
        {
            CaptureAndProcessFrame();
        }
    }

    [ContextMenu("Capture and Process Frame")]
    public void CaptureAndProcessFrame()
    {
        if (isProcessing)
        {
            Debug.LogWarning("Already processing a frame. Please wait...");
            return;
        }

        if (cameraManager == null)
        {
            Debug.LogError("Camera Manager not found!");
            return;
        }

        WebCamTexture camTexture = cameraManager.GetCameraTexture();
        if (camTexture == null || !camTexture.isPlaying)
        {
            Debug.LogError("Camera is not running!");
            return;
        }

        StartCoroutine(ProcessFrameCoroutine(camTexture));
    }

    // Store all debug information for report
    private List<string> reportLines = new List<string>();
    private Dictionary<string, long> timingData = new Dictionary<string, long>();
    private Dictionary<string, Dictionary<string, float>> imageAnalysis = new Dictionary<string, Dictionary<string, float>>();

    IEnumerator ProcessFrameCoroutine(WebCamTexture camTexture)
    {
        isProcessing = true;

        // Clear previous data
        reportLines.Clear();
        timingData.Clear();
        imageAnalysis.Clear();

        // Create output directory
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string outputPath = Path.Combine(Application.persistentDataPath, saveFolder, timestamp);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // Start report
        reportLines.Add("===========================================");
        reportLines.Add($"CAMERA FRAME PROCESSING REPORT");
        reportLines.Add($"Timestamp: {timestamp}");
        reportLines.Add($"Output folder: {outputPath}");
        reportLines.Add("===========================================");
        reportLines.Add("");

        Debug.Log($"\n===========================================");
        Debug.Log($"CAPTURING CAMERA FRAME: {timestamp}");
        Debug.Log($"Output folder: {outputPath}");
        Debug.Log($"===========================================");

        // Initialize OpenCV components
        InitializeComponents();

        // Process the frame
        ProcessCameraFrame(camTexture, outputPath);

        // Test matching with each marker
        yield return null;
        TestMatchingWithMarkers(outputPath);

        // Generate and save report
        GenerateReport(outputPath);

        Debug.Log($"\n=== PROCESSING COMPLETE ===");
        Debug.Log($"Check folder: {outputPath}");

        // Open folder
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        System.Diagnostics.Process.Start("explorer.exe", outputPath.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        System.Diagnostics.Process.Start("open", outputPath);
#endif

        // Cleanup
        CleanupComponents();

        isProcessing = false;
    }

    void InitializeComponents()
    {
        // Initialize SIFT
        siftDetector = SIFT.create(
            nfeatures: cameraManager.nFeatures,
            nOctaveLayers: cameraManager.nOctaveLayers,
            contrastThreshold: cameraManager.contrastThreshold,
            edgeThreshold: cameraManager.edgeThreshold,
            sigma: cameraManager.sigma
        );

        // Initialize FLANN matcher (simplified for OpenCV for Unity)
        flannMatcher = new FlannBasedMatcher();

        // Initialize CLAHE
        if (cameraManager.enableCLAHE)
        {
            clahe = Imgproc.createCLAHE(cameraManager.claheClipLimit, new Size(8, 8));
        }
    }

    void ProcessCameraFrame(WebCamTexture camTexture, string outputPath)
    {
        try
        {
            var totalSW = System.Diagnostics.Stopwatch.StartNew();

            reportLines.Add("CAMERA FRAME PROCESSING");
            reportLines.Add("------------------------");

            // Step 1: Capture current frame
            Mat frameMat = new Mat(camTexture.height, camTexture.width, CvType.CV_8UC4);
            Utils.webCamTextureToMat(camTexture, frameMat);
            SaveMatToFile(frameMat, Path.Combine(outputPath, "01_Camera_Original.png"));

            reportLines.Add("Step 1: Camera Frame Captured");
            reportLines.Add($"  • Resolution: {frameMat.width()}x{frameMat.height()}");
            reportLines.Add($"  • Channels: {frameMat.channels()}");
            reportLines.Add("");

            Debug.Log($"\n✓ Step 1: Camera Frame Captured");
            Debug.Log($"  • Resolution: {frameMat.width()}x{frameMat.height()}");
            Debug.Log($"  • Channels: {frameMat.channels()}");

            // Step 2: Convert to grayscale
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Mat grayMat = new Mat();
            Imgproc.cvtColor(frameMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
            sw.Stop();
            timingData["Grayscale"] = sw.ElapsedMilliseconds;
            SaveMatToFile(grayMat, Path.Combine(outputPath, "02_Camera_Grayscale.png"));

            reportLines.Add($"Step 2: Grayscale Conversion - {sw.ElapsedMilliseconds}ms");
            AnalyzeImage(grayMat, "Grayscale");
            reportLines.Add("");

            Debug.Log($"\n✓ Step 2: Grayscale Conversion - {sw.ElapsedMilliseconds}ms");

            // Step 3: Apply CLAHE
            Mat claheMat = new Mat();
            if (cameraManager.enableCLAHE)
            {
                sw.Restart();
                clahe.apply(grayMat, claheMat);
                sw.Stop();
                timingData["CLAHE"] = sw.ElapsedMilliseconds;
                SaveMatToFile(claheMat, Path.Combine(outputPath, "03_Camera_CLAHE.png"));

                reportLines.Add($"Step 3: CLAHE Applied - {sw.ElapsedMilliseconds}ms");
                AnalyzeImage(claheMat, "CLAHE");
                reportLines.Add("");

                Debug.Log($"\n✓ Step 3: CLAHE Applied - {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                claheMat = grayMat.clone();
                reportLines.Add("Step 3: CLAHE disabled");
                reportLines.Add("");
                Debug.Log("\n✗ Step 3: CLAHE disabled");
            }

            // Step 4: Apply Denoising
            Mat denoisedMat = new Mat();
            if (cameraManager.enableDenoising)
            {
                sw.Restart();
                Photo.fastNlMeansDenoising(claheMat, denoisedMat, cameraManager.denoisingH, 7, 21);
                sw.Stop();
                timingData["Denoising"] = sw.ElapsedMilliseconds;
                SaveMatToFile(denoisedMat, Path.Combine(outputPath, "04_Camera_Denoised.png"));

                reportLines.Add($"Step 4: Denoising Applied - {sw.ElapsedMilliseconds}ms");
                AnalyzeImage(denoisedMat, "Denoised");
                reportLines.Add("");

                Debug.Log($"\n✓ Step 4: Denoising Applied - {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                denoisedMat = claheMat.clone();
                reportLines.Add("Step 4: Denoising disabled");
                reportLines.Add("");
                Debug.Log("\n✗ Step 4: Denoising disabled");
            }

            // Step 5: Apply Sharpening
            Mat sharpenedMat = new Mat();
            if (cameraManager.enableSharpening && cameraManager.sharpeningAmount > 0)
            {
                sw.Restart();
                Mat blurred = new Mat();
                Imgproc.GaussianBlur(denoisedMat, blurred, new Size(0, 0), 3);
                Core.addWeighted(denoisedMat, 1.0 + cameraManager.sharpeningAmount,
                               blurred, -cameraManager.sharpeningAmount, 0, sharpenedMat);
                blurred.Dispose();
                sw.Stop();
                timingData["Sharpening"] = sw.ElapsedMilliseconds;

                SaveMatToFile(sharpenedMat, Path.Combine(outputPath, "05_Camera_Sharpened.png"));

                reportLines.Add($"Step 5: Sharpening Applied - {sw.ElapsedMilliseconds}ms");
                AnalyzeImage(sharpenedMat, "Sharpened");
                reportLines.Add("");

                Debug.Log($"\n✓ Step 5: Sharpening Applied - {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                sharpenedMat = denoisedMat.clone();
                reportLines.Add("Step 5: Sharpening disabled");
                reportLines.Add("");
                Debug.Log("\n✗ Step 5: Sharpening disabled");
            }

            // Step 6: Detect SIFT keypoints
            Mat finalProcessed = sharpenedMat.clone();
            MatOfKeyPoint keypoints = new MatOfKeyPoint();
            Mat descriptors = new Mat();

            sw.Restart();
            siftDetector.detectAndCompute(finalProcessed, new Mat(), keypoints, descriptors);
            sw.Stop();
            timingData["SIFT"] = sw.ElapsedMilliseconds;

            reportLines.Add($"Step 6: SIFT Detection - {sw.ElapsedMilliseconds}ms");
            reportLines.Add($"  • Keypoints found: {keypoints.toArray().Length}");
            reportLines.Add($"  • Descriptor dimensions: {descriptors.rows()} x {descriptors.cols()}");

            Debug.Log($"\n✓ Step 6: SIFT Detection - {sw.ElapsedMilliseconds}ms");
            Debug.Log($"  • Keypoints found: {keypoints.toArray().Length}");
            Debug.Log($"  • Descriptor dimensions: {descriptors.rows()} x {descriptors.cols()}");

            // Draw and save keypoints
            Mat keypointMat = new Mat();
            Features2d.drawKeypoints(finalProcessed, keypoints, keypointMat,
                                   new Scalar(0, 255, 0),
                                   Features2d.DrawMatchesFlags_DRAW_RICH_KEYPOINTS);
            SaveMatToFile(keypointMat, Path.Combine(outputPath, "06_Camera_Keypoints.png"));

            // Analyze keypoint distribution
            AnalyzeKeypoints(keypoints);

            totalSW.Stop();
            timingData["Total"] = totalSW.ElapsedMilliseconds;
            reportLines.Add("");
            reportLines.Add($"⏱ TOTAL PROCESSING TIME: {totalSW.ElapsedMilliseconds}ms");
            reportLines.Add("");

            Debug.Log($"\n⏱ TOTAL PROCESSING TIME: {totalSW.ElapsedMilliseconds}ms");

            // Store for matching test
            StoreProcessedFrame(finalProcessed, keypoints, descriptors, outputPath);

            // Cleanup
            frameMat.Dispose();
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
            reportLines.Add($"ERROR: {e.Message}");
            Debug.LogError($"Error processing camera frame: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }

    // Store processed frame data for matching test
    private Mat storedFrameMat;
    private MatOfKeyPoint storedKeypoints;
    private Mat storedDescriptors;

    void StoreProcessedFrame(Mat frame, MatOfKeyPoint keypoints, Mat descriptors, string outputPath)
    {
        storedFrameMat = frame.clone();
        storedKeypoints = new MatOfKeyPoint(keypoints.toArray());
        storedDescriptors = descriptors.clone();
    }

    void TestMatchingWithMarkers(string outputPath)
    {
        if (storedDescriptors == null || storedDescriptors.empty())
        {
            reportLines.Add("WARNING: No frame descriptors available for matching test");
            Debug.LogWarning("No frame descriptors available for matching test");
            return;
        }

        reportLines.Add("===========================================");
        reportLines.Add("TESTING MATCHING WITH MARKERS");
        reportLines.Add("===========================================");
        reportLines.Add("");

        Debug.Log($"\n===========================================");
        Debug.Log($"TESTING MATCHING WITH MARKERS");
        Debug.Log($"===========================================");

        foreach (var target in cameraManager.markerTargets)
        {
            if (target.markerDescriptors == null || target.markerDescriptors.empty())
                continue;

            TestMatchWithSingleMarker(target, outputPath);
        }

        // Cleanup stored data
        storedFrameMat?.Dispose();
        storedKeypoints?.Dispose();
        storedDescriptors?.Dispose();
    }

    void TestMatchWithSingleMarker(OpenCVMarkerTarget target, string outputPath)
    {
        try
        {
            reportLines.Add($"Testing: {target.targetName}");
            reportLines.Add("-----------------------------------");

            Debug.Log($"\n--- Testing: {target.targetName} ---");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Match features using FLANN
            List<MatOfDMatch> knnMatches = new List<MatOfDMatch>();
            flannMatcher.knnMatch(target.markerDescriptors, storedDescriptors, knnMatches, 2);

            // Apply Lowe's ratio test
            List<DMatch> goodMatches = new List<DMatch>();

            Debug.Log($"Before ratio test: {knnMatches.Count} matches");
            foreach (MatOfDMatch knnMatch in knnMatches)
            {
                DMatch[] matches = knnMatch.toArray();
                if (matches.Length >= 2)
                {
                    if (matches[0].distance < target.ratioThreshold * matches[1].distance)
                    {
                        goodMatches.Add(matches[0]);
                    }
                }
                knnMatch.Dispose();
            }
                

            // Filter by absolute distance
            goodMatches = goodMatches.Where(m => m.distance < target.matchDistanceThreshold).ToList();

            sw.Stop();

            reportLines.Add($"  • Matching time: {sw.ElapsedMilliseconds}ms");
            reportLines.Add($"  • Initial matches: {knnMatches.Count}");
            reportLines.Add($"  • Good matches after ratio test: {goodMatches.Count}");
            reportLines.Add($"  • Minimum required: {target.minMatches}");

            Debug.Log($"  • Matching time: {sw.ElapsedMilliseconds}ms");
            Debug.Log($"  • Initial matches: {knnMatches.Count}");
            Debug.Log($"  • Good matches after ratio test: {goodMatches.Count}");
            Debug.Log($"  • Minimum required: {target.minMatches}");

            // Test homography if enough matches
            if (cameraManager.useHomographyVerification && goodMatches.Count >= 4)
            {
                sw.Restart();
                Mat homography = TestHomography(target, goodMatches);
                sw.Stop();

                if (homography != null)
                {
                    reportLines.Add($"  ✓ Homography found! - {sw.ElapsedMilliseconds}ms");
                    reportLines.Add($"  → MARKER DETECTED IN FRAME!");

                    Debug.Log($"  ✓ Homography found! - {sw.ElapsedMilliseconds}ms");
                    Debug.Log($"  → MARKER DETECTED IN FRAME!");
                    homography.Dispose();
                }
                else
                {
                    reportLines.Add($"  ✗ Homography verification failed");
                    Debug.Log($"  ✗ Homography verification failed");
                }
            }
            else if (goodMatches.Count >= target.minMatches)
            {
                reportLines.Add($"  ✓ Enough matches found (no homography check)");
                Debug.Log($"  ✓ Enough matches found (no homography check)");
            }
            else
            {
                reportLines.Add($"  ✗ Not enough matches");
                Debug.Log($"  ✗ Not enough matches");
            }

            reportLines.Add("");

            // Draw and save matches visualization
            if (goodMatches.Count > 0)
            {
                DrawAndSaveMatches(target, goodMatches, outputPath);
            }

        }
        catch (System.Exception e)
        {
            reportLines.Add($"ERROR testing {target.targetName}: {e.Message}");
            Debug.LogError($"Error testing match with {target.targetName}: {e.Message}");
        }
    }

    Mat TestHomography(OpenCVMarkerTarget target, List<DMatch> matches)
    {
        try
        {
            List<Point> markerPoints = new List<Point>();
            List<Point> scenePoints = new List<Point>();

            KeyPoint[] markerKp = target.markerKeypoints.toArray();
            KeyPoint[] sceneKp = storedKeypoints.toArray();

            foreach (DMatch match in matches)
            {
                if (match.queryIdx < markerKp.Length && match.trainIdx < sceneKp.Length)
                {
                    markerPoints.Add(markerKp[match.queryIdx].pt);
                    scenePoints.Add(sceneKp[match.trainIdx].pt);
                }
            }

            if (markerPoints.Count < 4)
                return null;

            MatOfPoint2f markerMat = new MatOfPoint2f();
            markerMat.fromList(markerPoints);
            MatOfPoint2f sceneMat = new MatOfPoint2f();
            sceneMat.fromList(scenePoints);

            Mat mask = new Mat();
            Mat homography = Calib3d.findHomography(
                markerMat, sceneMat, Calib3d.RANSAC,
                target.homographyReprojectionThreshold, mask
            );

            Mat result = null;

            if (!homography.empty())
            {
                byte[] maskArray = new byte[(int)mask.total()];
                mask.get(0, 0, maskArray);
                int inliers = maskArray.Count(b => b != 0);

                float inlierRatio = (float)inliers / matches.Count;

                reportLines.Add($"  • Homography inliers: {inliers}/{matches.Count} ({inlierRatio * 100:F1}%)");
                Debug.Log($"  • Homography inliers: {inliers}/{matches.Count} ({inlierRatio * 100:F1}%)");

                if (inlierRatio >= cameraManager.minHomographyInlierRatio)
                {
                    result = homography.clone();
                }
            }

            markerMat.Dispose();
            sceneMat.Dispose();
            mask.Dispose();
            if (result == null)
                homography.Dispose();

            return result;
        }
        catch (System.Exception e)
        {
            reportLines.Add($"  ERROR in homography: {e.Message}");
            Debug.LogError($"Homography test error: {e.Message}");
            return null;
        }
    }

    void DrawAndSaveMatches(OpenCVMarkerTarget target, List<DMatch> matches, string outputPath)
    {
        try
        {
            // Convert matches to MatOfDMatch
            MatOfDMatch matchesMat = new MatOfDMatch();
            matchesMat.fromList(matches);

            // Draw matches
            Mat output = new Mat();
            Features2d.drawMatches(target.grayMarker, target.markerKeypoints,
                                 storedFrameMat, storedKeypoints,
                                 matchesMat, output,
                                 new Scalar(0, 255, 0), new Scalar(255, 0, 0),
                                 new MatOfByte(), Features2d.DrawMatchesFlags_DEFAULT);

            string fileName = $"07_Matches_{target.targetName.Replace(" ", "_")}.png";
            SaveMatToFile(output, Path.Combine(outputPath, fileName));

            output.Dispose();
            matchesMat.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error drawing matches: {e.Message}");
        }
    }

    void SaveMatToFile(Mat mat, string filePath)
    {
        try
        {
            Mat saveableMat = new Mat();
            if (mat.channels() == 1)
                Imgproc.cvtColor(mat, saveableMat, Imgproc.COLOR_GRAY2RGBA);
            else if (mat.channels() == 3)
                Imgproc.cvtColor(mat, saveableMat, Imgproc.COLOR_RGB2RGBA);
            else
                saveableMat = mat.clone();

            Texture2D texture = new Texture2D(saveableMat.width(), saveableMat.height(),
                                             TextureFormat.RGBA32, false);
            Utils.matToTexture2D(saveableMat, texture);

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            Debug.Log($"  → Saved: {Path.GetFileName(filePath)}");

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
            MatOfDouble mean = new MatOfDouble();
            MatOfDouble stdDev = new MatOfDouble();
            Core.meanStdDev(grayImage, mean, stdDev);

            Core.MinMaxLocResult minMax = Core.minMaxLoc(grayImage);

            Debug.Log($"  • Mean: {mean.get(0, 0)[0]:F2}, StdDev: {stdDev.get(0, 0)[0]:F2}");
            Debug.Log($"  • Min: {minMax.minVal:F2}, Max: {minMax.maxVal:F2}");
            Debug.Log($"  • Contrast: {(minMax.maxVal - minMax.minVal):F2}");

            mean.Dispose();
            stdDev.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error analyzing image: {e.Message}");
        }
    }

    void AnalyzeKeypoints(MatOfKeyPoint keypoints)
    {
        KeyPoint[] kpArray = keypoints.toArray();

        if (kpArray.Length == 0)
        {
            Debug.LogWarning("  ⚠ No keypoints detected in camera frame!");
            return;
        }

        float minResponse = float.MaxValue;
        float maxResponse = float.MinValue;
        float avgResponse = 0;

        foreach (var kp in kpArray)
        {
            minResponse = Mathf.Min(minResponse, kp.response);
            maxResponse = Mathf.Max(maxResponse, kp.response);
            avgResponse += kp.response;
        }
        avgResponse /= kpArray.Length;

        Debug.Log($"  • Response - Min: {minResponse:F4}, Max: {maxResponse:F4}, Avg: {avgResponse:F4}");
    }

    void GenerateReport(string outputPath)
    {
        try
        {
            reportLines.Add("");
            reportLines.Add("===========================================");
            reportLines.Add("CONFIGURATION SETTINGS");
            reportLines.Add("===========================================");
            reportLines.Add("");
            reportLines.Add("Camera Settings:");
            reportLines.Add($"  • Requested Resolution: {cameraManager.requestedWidth}x{cameraManager.requestedHeight}");
            reportLines.Add($"  • Use Back Camera: {cameraManager.useBackCamera}");
            reportLines.Add("");

            reportLines.Add("SIFT Settings:");
            reportLines.Add($"  • nFeatures: {cameraManager.nFeatures}");
            reportLines.Add($"  • nOctaveLayers: {cameraManager.nOctaveLayers}");
            reportLines.Add($"  • contrastThreshold: {cameraManager.contrastThreshold}");
            reportLines.Add($"  • edgeThreshold: {cameraManager.edgeThreshold}");
            reportLines.Add($"  • sigma: {cameraManager.sigma}");
            reportLines.Add("");

            reportLines.Add("Performance Settings:");
            reportLines.Add($"  • Process Every Nth Frame: {cameraManager.processEveryNthFrame}");
            reportLines.Add($"  • Tracking Process Every Nth Frame: {cameraManager.trackingProcessEveryNthFrame}");
            reportLines.Add($"  • Detection Interval: {cameraManager.detectionInterval}s");
            reportLines.Add("");

            reportLines.Add("Preprocessing Settings:");
            reportLines.Add($"  • CLAHE Enabled: {cameraManager.enableCLAHE}");
            if (cameraManager.enableCLAHE)
                reportLines.Add($"  • CLAHE Clip Limit: {cameraManager.claheClipLimit}");
            reportLines.Add($"  • Denoising Enabled: {cameraManager.enableDenoising}");
            if (cameraManager.enableDenoising)
                reportLines.Add($"  • Denoising H: {cameraManager.denoisingH}");
            reportLines.Add($"  • Sharpening Enabled: {cameraManager.enableSharpening}");
            if (cameraManager.enableSharpening)
                reportLines.Add($"  • Sharpening Amount: {cameraManager.sharpeningAmount}");
            reportLines.Add("");

            reportLines.Add("Detection Thresholds:");
            reportLines.Add($"  • Min Consecutive Detections: {cameraManager.minConsecutiveDetections}");
            reportLines.Add($"  • Min Consecutive Losses: {cameraManager.minConsecutiveLosses}");
            reportLines.Add($"  • Use Homography Verification: {cameraManager.useHomographyVerification}");
            reportLines.Add($"  • Min Homography Inlier Ratio: {cameraManager.minHomographyInlierRatio}");
            reportLines.Add("");

            reportLines.Add("===========================================");
            reportLines.Add("PERFORMANCE SUMMARY");
            reportLines.Add("===========================================");
            reportLines.Add("");

            if (timingData.Count > 0)
            {
                reportLines.Add("Processing Times:");
                foreach (var kvp in timingData)
                {
                    reportLines.Add($"  • {kvp.Key}: {kvp.Value}ms");

                    // Add warnings for slow operations
                    if (kvp.Key == "SIFT" && kvp.Value > 100)
                        reportLines.Add($"    ⚠ WARNING: SIFT is slow! Consider reducing nFeatures");
                    if (kvp.Key == "Denoising" && kvp.Value > 50)
                        reportLines.Add($"    ⚠ WARNING: Denoising is slow! Consider disabling it");
                    if (kvp.Key == "Total" && kvp.Value > 150)
                        reportLines.Add($"    ⚠ WARNING: Total processing time too high for real-time!");
                }
                reportLines.Add("");
            }

            reportLines.Add("===========================================");
            reportLines.Add("RECOMMENDATIONS");
            reportLines.Add("===========================================");
            reportLines.Add("");

            // Generate recommendations based on analysis
            if (timingData.ContainsKey("Total") && timingData["Total"] > 150)
            {
                reportLines.Add("Performance Optimizations Needed:");
                if (timingData.ContainsKey("SIFT") && timingData["SIFT"] > 100)
                    reportLines.Add("  • Reduce nFeatures to 200 or less");
                if (timingData.ContainsKey("Denoising") && timingData["Denoising"] > 30)
                    reportLines.Add("  • Disable denoising (enableDenoising = false)");
                if (cameraManager.requestedWidth > 640)
                    reportLines.Add("  • Reduce camera resolution to 640x480");
                reportLines.Add("");
            }

            // Save report to file
            string reportPath = Path.Combine(outputPath, "processing_report.txt");
            File.WriteAllLines(reportPath, reportLines.ToArray());
            Debug.Log($"Report saved to: {reportPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error generating report: {e.Message}");
        }
    }

    void CleanupComponents()
    {
        siftDetector?.Dispose();
        flannMatcher?.Dispose();
        clahe?.Dispose();
    }

    void OnDestroy()
    {
        CleanupComponents();
    }
}