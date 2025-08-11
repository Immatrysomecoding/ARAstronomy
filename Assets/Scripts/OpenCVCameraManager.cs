using UnityEngine;
using UnityEngine.Events;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.Calib3dModule;
using System;
using OpenCVForUnity.Xfeatures2dModule;
using OpenCVForUnity.VideoModule;
using OpenCVForUnity.PhotoModule;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEditorInternal;

public enum TrackingState
{
    NotTracking,
    Detecting,
    Tracking,
    Lost
}

[System.Serializable]
public class OpenCVMarkerTarget
{
    [Header("Target Setup")]
    public string targetName;
    public Texture2D markerImage;
    public GameObject targetObject;

    [Header("SIFT Detection Settings")]
    public int minMatches = 20;                        // Higher for SIFT
    public float matchDistanceThreshold = 200f;        // SIFT uses different scale
    public float ratioThreshold = 0.7f;                // Lowe's ratio
    public float homographyReprojectionThreshold = 3.0f;

    [Header("Tracking Settings")]
    public float opticalFlowQuality = 0.01f;           // Min quality for optical flow
    public int opticalFlowMinDistance = 30;            // Min distance between points
    public float trackingConfidenceThreshold = 0.6f;   // Min confidence to keep tracking

    [Header("Events")]
    public UnityEvent OnTargetFound;
    public UnityEvent OnTargetLost;

    // Internal state
    [System.NonSerialized] public Mat grayMarker;
    [System.NonSerialized] public MatOfKeyPoint markerKeypoints;
    [System.NonSerialized] public Mat markerDescriptors;
    [System.NonSerialized] public bool isCurrentlyTracked = false;
    [System.NonSerialized] public float lastDetectionTime = 0f;
    [System.NonSerialized] public OpenCVTrackingBehaviour trackingBehaviour;
    [System.NonSerialized] public int consecutiveDetections = 0;
    [System.NonSerialized] public int consecutiveLosses = 0;

    // Tracking state
    [System.NonSerialized] public TrackingState trackingState = TrackingState.NotTracking;
    [System.NonSerialized] public MatOfPoint2f trackedPoints;
    [System.NonSerialized] public MatOfPoint2f previousPoints;
    [System.NonSerialized] public Mat homographyMatrix;
    [System.NonSerialized] public float trackingConfidence = 0f;
    [System.NonSerialized] public int trackingFrameCount = 0;
}

public class OpenCVCameraManager : MonoBehaviour
{
    [Header("Camera Settings")]
    public int cameraIndex = 0;
    public int requestedWidth = 640;
    public int requestedHeight = 420;
    public bool useBackCamera = true;

    [Header("SIFT Feature Settings")]
    public int nFeatures = 200;                    // Number of best features to retain
    public int nOctaveLayers = 3;                  // Number of layers in each octave
    public float contrastThreshold = 0.04f;        // Lower = more features
    public float edgeThreshold = 10f;              // Filter out edge-like features
    public float sigma = 1.6f;                     // Gaussian sigma

    [Header("Performance Settings")]
    public int processEveryNthFrame = 3;           // Detection frequency
    public int trackingProcessEveryNthFrame = 2;   // Tracking frequency (faster)
    public float detectionInterval = 0.5f;         // Min time between detections
    public float trackingTimeout = 1.0f;           // Time before losing track

    [Header("Detection Thresholds")]
    public int minConsecutiveDetections = 2;
    public int minConsecutiveLosses = 3;
    public bool useHomographyVerification = true;
    public float minHomographyInlierRatio = 0.4f;

    [Header("Preprocessing")]
    public bool enableCLAHE = true;
    public float claheClipLimit = 2.0f;
    public bool enableDenoising = false;           // if slow turn off
    public float denoisingH = 3f;
    public bool enableSharpening = false;
    public float sharpeningAmount = 0.5f;

    [Header("Optical Flow Settings")]
    public int maxOpticalFlowPoints = 100;
    public int opticalFlowWinSize = 21;
    public int opticalFlowMaxLevel = 3;
    public float opticalFlowMinEigenThreshold = 0.001f;

    [Header("All Marker Targets")]
    public OpenCVMarkerTarget[] markerTargets;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool visualDebugMode = false;

    // Private components
    private WebCamTexture webCamTexture;
    private Mat currentFrame;
    private Mat grayFrame;
    private Mat previousGrayFrame;

    // SIFT detector and FLANN matcher
    private SIFT siftDetector;
    private FlannBasedMatcher flannMatcher;
    private CLAHE clahe;

    // State tracking
    private int frameCounter = 0;
    private bool isInitialized = false;
    private float lastDetectionTime = 0f;

    // Optical flow
    private MatOfByte opticalFlowStatus;
    private MatOfFloat opticalFlowErr;

    void Start()
    {
        StartCoroutine(InitializeSystem());
    }


    IEnumerator InitializeSystem()
    {
        InitializeCamera();
        yield return new WaitForSeconds(1f);
        InitializeOpenCV();
        InitializeTrackingBehaviours();

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log("Camera + Sift initialized");
        }
    }

    void InitializeCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            string deviceName = "";

            if (useBackCamera)
            {
                foreach (WebCamDevice device in devices)
                {
                    if (!device.isFrontFacing)
                    {
                        deviceName = device.name;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = devices[cameraIndex < devices.Length ? cameraIndex : 0].name;
            }

            webCamTexture = new WebCamTexture(deviceName, requestedWidth, requestedHeight);
            webCamTexture.Play();

            if (showDebugInfo)
            {
                Debug.Log($"Camera started: {deviceName}, Resolution: {webCamTexture.width}x{webCamTexture.height}");
            }
        }
        else
        {
            Debug.LogError("No camera devices found!");
        }
    }


    //Sift to filter keypoint
    void InitializeOpenCV()
    {
        siftDetector = SIFT.create(
            nfeatures: nFeatures,
            nOctaveLayers: nOctaveLayers,
            contrastThreshold: contrastThreshold,
            edgeThreshold: edgeThreshold,
            sigma: sigma
        );
        flannMatcher = new FlannBasedMatcher();
        if (enableCLAHE)
            clahe = Imgproc.createCLAHE(claheClipLimit, new Size(8, 8));

        //Optical Flow algo
        opticalFlowStatus = new MatOfByte();
        opticalFlowErr = new MatOfFloat();

        int validTargets = 0;
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (ProcessMarkerImage(markerTargets[i]))
            {
                validTargets++;
            }
        }
        if (showDebugInfo)
        {
            Debug.Log($"SIFT initialized with {validTargets} valid marker targets");
        }
    }

    void InitializeTrackingBehaviours()
    {
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (markerTargets[i].targetObject != null)
            {
                markerTargets[i].trackingBehaviour = markerTargets[i].targetObject.GetComponent<OpenCVTrackingBehaviour>();
                if (markerTargets[i].trackingBehaviour == null)
                {
                    markerTargets[i].trackingBehaviour = markerTargets[i].targetObject.AddComponent<OpenCVTrackingBehaviour>();
                }

                markerTargets[i].trackingBehaviour.Initialize(markerTargets[i].targetName);

                Tracking trackingScript = markerTargets[i].targetObject.GetComponent<Tracking>();
                if (trackingScript != null)
                {
                    markerTargets[i].OnTargetFound.AddListener(trackingScript.OnTargetFound);
                    markerTargets[i].OnTargetLost.AddListener(trackingScript.OnTargetLost);
                }
            }
        }
    }

    bool ProcessMarkerImage(OpenCVMarkerTarget target)
    {
        if (target.markerImage == null)
        {
            Debug.LogWarning($"Marker image not assigned for target: {target.targetName}");
            return false;
        }

        try
        {
            Mat markerMat = new Mat(target.markerImage.height, target.markerImage.width, CvType.CV_8UC4);
            Utils.texture2DToMat(target.markerImage, markerMat);

            // Convert to grayscale
            target.grayMarker = new Mat();
            Imgproc.cvtColor(markerMat, target.grayMarker, Imgproc.COLOR_RGBA2GRAY);


            Mat processedMarker = PreprocessImage(target.grayMarker);

            // Detect SIFT features
            target.markerKeypoints = new MatOfKeyPoint();
            target.markerDescriptors = new Mat();
            siftDetector.detectAndCompute(processedMarker, new Mat(), target.markerKeypoints, target.markerDescriptors);

            if (showDebugInfo)
            {
                Debug.Log($"SIFT processed marker {target.targetName}: {target.markerKeypoints.toArray().Length} features");
            }

            markerMat.Dispose();
            processedMarker.Dispose();

            return target.markerKeypoints.toArray().Length > 10;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing marker {target.targetName}: {e.Message}");
            return false;
        }
    }

    //Update Looping lại từng frame
    void Update()
    {
        if (!isInitialized || webCamTexture == null || !webCamTexture.isPlaying)
            return;

        frameCounter++;

        ProcessCurrentFrame();
        CheckTrackingTimeouts();
    }


    //Lấy frame unity->opencv mat
    void ProcessCurrentFrame()
    {
        try
        {
            // Convert camera to Mat
            if (currentFrame == null)
                currentFrame = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);

            Utils.webCamTextureToMat(webCamTexture, currentFrame);

            // Convert to grayscale
            if (grayFrame == null)
                grayFrame = new Mat();

            Imgproc.cvtColor(currentFrame, grayFrame, Imgproc.COLOR_RGBA2GRAY);

            // Process each target based on its state
            for (int i = 0; i < markerTargets.Length; i++)
            {
                OpenCVMarkerTarget target = markerTargets[i];

                switch (target.trackingState)
                {
                    case TrackingState.NotTracking:
                    case TrackingState.Lost:
                        // Try detection at intervals
                        if (frameCounter % processEveryNthFrame == 0 &&
                            Time.time - lastDetectionTime > detectionInterval)
                        {
                            TryDetectTarget(target);
                        }
                        break;

                    case TrackingState.Detecting:
                        // Continue detection until confident
                        if (frameCounter % processEveryNthFrame == 0)
                        {
                            TryDetectTarget(target);
                        }
                        break;

                    case TrackingState.Tracking:
                        // Use optical flow for fast tracking
                        if (frameCounter % trackingProcessEveryNthFrame == 0)
                        {
                            TrackTargetOpticalFlow(target);
                        }
                        break;
                }
            }

            // Store previous frame for optical flow
            if (previousGrayFrame == null)
                previousGrayFrame = new Mat();

            grayFrame.copyTo(previousGrayFrame);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing frame: {e.Message}");
        }
    }

    void TryDetectTarget(OpenCVMarkerTarget target)
    {
        try
        {
            // Preprocess current frame
            Mat processedFrame = PreprocessImage(grayFrame);

            // Detect SIFT features
            MatOfKeyPoint frameKeypoints = new MatOfKeyPoint();
            Mat frameDescriptors = new Mat();
            siftDetector.detectAndCompute(processedFrame, new Mat(), frameKeypoints, frameDescriptors);

            if (frameDescriptors.rows() == 0 || target.markerDescriptors.rows() == 0)
            {
                processedFrame.Dispose();
                frameKeypoints.Dispose();
                frameDescriptors.Dispose();
                return;
            }

            // Match features using FLANN
            List<MatOfDMatch> knnMatches = new List<MatOfDMatch>();
            flannMatcher.knnMatch(target.markerDescriptors, frameDescriptors, knnMatches, 2);

            // Apply Lowe's ratio test
            List<DMatch> goodMatches = new List<DMatch>();

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

            bool detectionValid = false;
            Mat homography = null;

            // Check if enough matches
            if (goodMatches.Count >= target.minMatches)
            {
                if (useHomographyVerification && goodMatches.Count >= 4)
                {
                    // Verify with homography
                    homography = VerifyWithHomographyAndGetMatrix(target, goodMatches, frameKeypoints);
                    detectionValid = (homography != null);
                }
                else
                {
                    detectionValid = true;
                }
            }

            // Update detection state
            if (detectionValid)
            {
                target.consecutiveDetections++;
                target.consecutiveLosses = 0;

                if (target.consecutiveDetections >= minConsecutiveDetections)
                {
                    // Switch to tracking mode
                    InitializeTracking(target, goodMatches, frameKeypoints, homography);
                    OnTargetDetected(target);
                    lastDetectionTime = Time.time;
                }
                else
                {
                    target.trackingState = TrackingState.Detecting;
                }

                if (showDebugInfo)
                {
                    Debug.Log($"SIFT Detection - {target.targetName}: {goodMatches.Count} matches, consecutive: {target.consecutiveDetections}");
                }
            }
            else
            {
                target.consecutiveDetections = 0;
                target.consecutiveLosses++;

                if (target.consecutiveLosses >= minConsecutiveLosses)
                {
                    target.trackingState = TrackingState.NotTracking;
                }
            }

            // Cleanup
            processedFrame.Dispose();
            frameKeypoints.Dispose();
            frameDescriptors.Dispose();
            homography?.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error detecting target {target.targetName}: {e.Message}");
        }
    }

    void InitializeTracking(OpenCVMarkerTarget target, List<DMatch> matches, MatOfKeyPoint frameKeypoints, Mat homography)
    {
        try
        {
            // Extract good points for tracking
            KeyPoint[] markerKp = target.markerKeypoints.toArray();
            KeyPoint[] frameKp = frameKeypoints.toArray();

            List<Point> trackingPoints = new List<Point>();

            // Get well-distributed points
            foreach (DMatch match in matches.Take(maxOpticalFlowPoints))
            {
                if (match.trainIdx < frameKp.Length)
                {
                    trackingPoints.Add(frameKp[match.trainIdx].pt);
                }
            }

            // Initialize tracking
            target.trackedPoints = new MatOfPoint2f();
            target.trackedPoints.fromList(trackingPoints);
            target.previousPoints = new MatOfPoint2f();
            target.previousPoints.fromList(trackingPoints);

            if (homography != null)
            {
                target.homographyMatrix = homography.clone();
            }

            target.trackingState = TrackingState.Tracking;
            target.trackingConfidence = 1.0f;
            target.trackingFrameCount = 0;

            if (showDebugInfo)
            {
                Debug.Log($"Initialized tracking for {target.targetName} with {trackingPoints.Count} points");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing tracking: {e.Message}");
        }
    }

    void TrackTargetOpticalFlow(OpenCVMarkerTarget target)
    {
        try
        {
            if (previousGrayFrame == null || target.previousPoints == null || target.previousPoints.empty())
            {
                target.trackingState = TrackingState.Lost;
                return;
            }

            // Calculate optical flow
            MatOfPoint2f nextPoints = new MatOfPoint2f();

            Video.calcOpticalFlowPyrLK(
                previousGrayFrame, grayFrame,
                target.previousPoints, nextPoints,
                opticalFlowStatus, opticalFlowErr,
                new Size(opticalFlowWinSize, opticalFlowWinSize),
                opticalFlowMaxLevel,
                new TermCriteria(TermCriteria.EPS | TermCriteria.COUNT, 30, 0.01)
            );

            // Filter good points
            byte[] status = opticalFlowStatus.toArray();
            float[] errors = opticalFlowErr.toArray();
            Point[] prevPts = target.previousPoints.toArray();
            Point[] nextPts = nextPoints.toArray();

            List<Point> goodPrevPoints = new List<Point>();
            List<Point> goodNextPoints = new List<Point>();

            for (int i = 0; i < status.Length; i++)
            {
                if (status[i] == 1 && errors[i] < 50f) // Good tracking
                {
                    goodPrevPoints.Add(prevPts[i]);
                    goodNextPoints.Add(nextPts[i]);
                }
            }

            // Update tracking confidence
            float pointRetentionRatio = (float)goodNextPoints.Count / prevPts.Length;
            target.trackingConfidence = target.trackingConfidence * 0.9f + pointRetentionRatio * 0.1f;
            target.trackingFrameCount++;

            // Check if we have enough points and confidence
            if (goodNextPoints.Count >= 10 && target.trackingConfidence > target.trackingConfidenceThreshold)
            {
                // Update tracked points
                target.previousPoints.fromList(goodNextPoints);
                target.trackedPoints = nextPoints;

                // Periodically verify with SIFT detection
                if (target.trackingFrameCount % 30 == 0) // Every second at 30fps
                {
                    TryDetectTarget(target); // This will reinitialize if needed
                }
            }
            else
            {
                // Lost tracking
                target.trackingState = TrackingState.Lost;
                target.consecutiveLosses++;

                if (target.consecutiveLosses >= minConsecutiveLosses && target.isCurrentlyTracked)
                {
                    OnTargetLost(target);
                }

                if (showDebugInfo)
                {
                    Debug.Log($"Lost tracking for {target.targetName}: confidence={target.trackingConfidence:F2}");
                }
            }

            // Cleanup
            nextPoints.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in optical flow tracking: {e.Message}");
            target.trackingState = TrackingState.Lost;
        }
    }

    Mat PreprocessImage(Mat input)
    {
        Mat result = input.clone();

        try
        {
            Mat equalized = new Mat();
            Imgproc.equalizeHist(input, equalized);
            // Apply CLAHE for better contrast
            if (enableCLAHE && clahe != null)
            {
                Mat enhanced = new Mat();
                clahe.apply(result, enhanced);
                result.Dispose();
                result = enhanced;
            }

            // Denoise (optional - can be slow)
            if (enableDenoising)
            {
                Mat denoised = new Mat();
                Photo.fastNlMeansDenoising(result, denoised, denoisingH, 7, 21);
                result.Dispose();
                result = denoised;
            }

            // Sharpen
            if (enableSharpening && sharpeningAmount > 0)
            {
                Mat sharpened = new Mat();
                Mat blurred = new Mat();
                Imgproc.GaussianBlur(result, blurred, new Size(0, 0), 3);
                Core.addWeighted(result, 1.0 + sharpeningAmount, blurred, -sharpeningAmount, 0, sharpened);
                result.Dispose();
                blurred.Dispose();
                result = sharpened;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error preprocessing image: {e.Message}");
        }

        return result;
    }

    Mat VerifyWithHomographyAndGetMatrix(OpenCVMarkerTarget target, List<DMatch> matches, MatOfKeyPoint currentKeypoints)
    {
        if (matches.Count < 4)
            return null;

        try
        {
            // Extract matched keypoints
            List<Point> markerPoints = new List<Point>();
            List<Point> scenePoints = new List<Point>();

            KeyPoint[] markerKp = target.markerKeypoints.toArray();
            KeyPoint[] sceneKp = currentKeypoints.toArray();

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

            // Find homography
            Mat mask = new Mat();
            Mat homography = Calib3d.findHomography(
                markerMat, sceneMat, Calib3d.RANSAC,
                target.homographyReprojectionThreshold, mask
            );

            Mat result = null;

            if (!homography.empty())
            {
                // Count inliers
                byte[] maskArray = new byte[(int)mask.total()];
                mask.get(0, 0, maskArray);
                int inliers = maskArray.Count(b => b != 0);

                float inlierRatio = (float)inliers / matches.Count;

                if (inlierRatio >= minHomographyInlierRatio)
                {
                    result = homography.clone();
                }
            }

            // Cleanup
            markerMat.Dispose();
            sceneMat.Dispose();
            mask.Dispose();
            if (result == null)
                homography.Dispose();

            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Homography verification error: {e.Message}");
            return null;
        }
    }

    void OnTargetDetected(OpenCVMarkerTarget target)
    {
        if (!target.isCurrentlyTracked)
        {
            target.isCurrentlyTracked = true;
            target.lastDetectionTime = Time.time;

            if (target.trackingBehaviour != null)
            {
                target.trackingBehaviour.HandleTargetFound();
            }

            target.OnTargetFound?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"Target found: {target.targetName} (State: {target.trackingState})");
            }
        }
    }

    void OnTargetLost(OpenCVMarkerTarget target)
    {
        target.isCurrentlyTracked = false;
        target.consecutiveDetections = 0;
        target.consecutiveLosses = 0;
        target.trackingState = TrackingState.NotTracking;

        // Clean up tracking data
        target.trackedPoints?.Dispose();
        target.previousPoints?.Dispose();
        target.homographyMatrix?.Dispose();
        target.trackedPoints = null;
        target.previousPoints = null;
        target.homographyMatrix = null;

        if (target.trackingBehaviour != null)
        {
            target.trackingBehaviour.HandleTargetLost();
        }

        target.OnTargetLost?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log($"Target lost: {target.targetName}");
        }
    }

    void CheckTrackingTimeouts()
    {
        for (int i = 0; i < markerTargets.Length; i++)
        {
            OpenCVMarkerTarget target = markerTargets[i];

            if (target.isCurrentlyTracked && Time.time - target.lastDetectionTime > trackingTimeout)
            {
                OnTargetLost(target);
            }
        }
    }

    void OnDestroy()
    {
        // Cleanup camera
        webCamTexture?.Stop();

        // Cleanup OpenCV
        currentFrame?.Dispose();
        grayFrame?.Dispose();
        previousGrayFrame?.Dispose();
        siftDetector?.Dispose();
        flannMatcher?.Dispose();
        clahe?.Dispose();
        opticalFlowStatus?.Dispose();
        opticalFlowErr?.Dispose();

        // Cleanup marker data
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (markerTargets[i] != null)
            {
                markerTargets[i].grayMarker?.Dispose();
                markerTargets[i].markerDescriptors?.Dispose();
                markerTargets[i].markerKeypoints?.Dispose();
                markerTargets[i].trackedPoints?.Dispose();
                markerTargets[i].previousPoints?.Dispose();
                markerTargets[i].homographyMatrix?.Dispose();
            }
        }
    }

    // Public methods
    public bool IsTargetTracked(string targetName)
    {
        var target = System.Array.Find(markerTargets, t => t.targetName == targetName);
        return target?.isCurrentlyTracked ?? false;
    }

    public TrackingState GetTrackingState(string targetName)
    {
        var target = System.Array.Find(markerTargets, t => t.targetName == targetName);
        return target?.trackingState ?? TrackingState.NotTracking;
    }

    public WebCamTexture GetCameraTexture()
    {
        return webCamTexture;
    }
}
