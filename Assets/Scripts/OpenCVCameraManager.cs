using UnityEngine;
using UnityEngine.Events;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public class OpenCVMarkerTarget
{
    [Header("Target Setup")]
    public string targetName;
    public Texture2D markerImage;
    public GameObject targetObject; // The planet container (like EarthTarget)

    [Header("Detection Settings")]
    public int minMatches = 8;
    public float matchDistanceThreshold = 0.02f;

    [Header("Events")]
    public UnityEvent OnTargetFound;
    public UnityEvent OnTargetLost;

    [System.NonSerialized]
    public Mat grayMarker;
    [System.NonSerialized]
    public MatOfKeyPoint markerKeypoints;
    [System.NonSerialized]
    public Mat markerDescriptors;
    [System.NonSerialized]
    public bool isCurrentlyTracked = false;
    [System.NonSerialized]
    public float lastDetectionTime = 0f;
    [System.NonSerialized]
    public OpenCVTrackingBehaviour trackingBehaviour;
}

public class OpenCVCameraManager : MonoBehaviour
{
    [Header("Camera Settings")]
    public int cameraIndex = 0;
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public bool useBackCamera = true;

    [Header("Performance Settings")]
    public int processEveryNthFrame = 3;
    public int maxFeatures = 500;
    public float trackingTimeout = 0.5f;

    [Header("All Marker Targets")]
    public OpenCVMarkerTarget[] markerTargets;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Private components
    private WebCamTexture webCamTexture;
    private Mat currentFrame;
    private Mat grayFrame;
    private ORB detector;
    private BFMatcher matcher;

    // State tracking
    private int frameCounter = 0;
    private bool isInitialized = false;
    private Camera arCamera;

    void Start()
    {
        StartCoroutine(InitializeSystem());
    }

    IEnumerator InitializeSystem()
    {
        arCamera = Camera.main;

        // Initialize camera
        InitializeCamera();

        // Wait for camera to start
        yield return new WaitForSeconds(1f);

        // Initialize OpenCV
        InitializeOpenCV();

        // Initialize tracking behaviours
        InitializeTrackingBehaviours();

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log("OpenCV Camera Manager initialized successfully!");
        }
    }

    void InitializeCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > 0)
        {
            string deviceName = "";

            // Try to find back camera if preferred
            if (useBackCamera)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (!devices[i].isFrontFacing)
                    {
                        deviceName = devices[i].name;
                        break;
                    }
                }
            }

            // Fallback to any available camera
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

    void InitializeOpenCV()
    {
        detector = ORB.create(maxFeatures);
        matcher = BFMatcher.create();

        // Process all marker images
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
            Debug.Log($"OpenCV initialized with {validTargets} valid marker targets out of {markerTargets.Length}");
        }
    }

    void InitializeTrackingBehaviours()
    {
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (markerTargets[i].targetObject != null)
            {
                // Get or add OpenCVTrackingBehaviour
                markerTargets[i].trackingBehaviour = markerTargets[i].targetObject.GetComponent<OpenCVTrackingBehaviour>();
                if (markerTargets[i].trackingBehaviour == null)
                {
                    markerTargets[i].trackingBehaviour = markerTargets[i].targetObject.AddComponent<OpenCVTrackingBehaviour>();
                }

                // Initialize the behaviour
                markerTargets[i].trackingBehaviour.Initialize(markerTargets[i].targetName);
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
            // Convert marker image to OpenCV format
            Mat markerMat = new Mat(target.markerImage.height, target.markerImage.width, CvType.CV_8UC4);
            Utils.texture2DToMat(target.markerImage, markerMat);

            // Convert to grayscale
            target.grayMarker = new Mat();
            Imgproc.cvtColor(markerMat, target.grayMarker, Imgproc.COLOR_RGBA2GRAY);

            // Detect features
            target.markerKeypoints = new MatOfKeyPoint();
            target.markerDescriptors = new Mat();
            detector.detectAndCompute(target.grayMarker, new Mat(), target.markerKeypoints, target.markerDescriptors);

            if (showDebugInfo)
            {
                Debug.Log($"Processed marker {target.targetName}: {target.markerKeypoints.toArray().Length} features detected");
            }

            markerMat.Dispose();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing marker {target.targetName}: {e.Message}");
            return false;
        }
    }

    void Update()
    {
        if (!isInitialized || webCamTexture == null || !webCamTexture.isPlaying)
            return;

        frameCounter++;

        // Process frame at reduced frequency for performance
        if (frameCounter % processEveryNthFrame == 0)
        {
            ProcessCurrentFrame();
        }

        // Check for tracking timeouts
        CheckTrackingTimeouts();
    }

    void ProcessCurrentFrame()
    {
        try
        {
            // Initialize frame if needed
            if (currentFrame == null)
                currentFrame = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);

            // Convert webcam texture to OpenCV Mat
            Utils.webCamTextureToMat(webCamTexture, currentFrame);

            // Convert to grayscale
            if (grayFrame == null)
                grayFrame = new Mat();

            Imgproc.cvtColor(currentFrame, grayFrame, Imgproc.COLOR_RGBA2GRAY);

            // Detect features in current frame
            MatOfKeyPoint currentKeypoints = new MatOfKeyPoint();
            Mat currentDescriptors = new Mat();
            detector.detectAndCompute(grayFrame, new Mat(), currentKeypoints, currentDescriptors);

            if (currentDescriptors.rows() > 0)
            {
                // Check each marker target
                for (int i = 0; i < markerTargets.Length; i++)
                {
                    CheckMarkerTarget(markerTargets[i], currentDescriptors, currentKeypoints);
                }
            }

            // Cleanup
            currentKeypoints.Dispose();
            currentDescriptors.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing frame: {e.Message}");
        }
    }

    void CheckMarkerTarget(OpenCVMarkerTarget target, Mat currentDescriptors, MatOfKeyPoint currentKeypoints)
    {
        if (target.markerDescriptors == null || target.markerDescriptors.rows() == 0)
            return;

        try
        {
            // Match features
            MatOfDMatch matches = new MatOfDMatch();
            matcher.match(target.markerDescriptors, currentDescriptors, matches);

            DMatch[] matchArray = matches.toArray();

            if (matchArray.Length > 0)
            {
                // Filter good matches
                List<DMatch> goodMatches = FilterGoodMatches(matchArray, target.matchDistanceThreshold);

                if (goodMatches.Count >= target.minMatches)
                {
                    OnTargetDetected(target);
                    target.lastDetectionTime = Time.time;

                    if (showDebugInfo && frameCounter % (processEveryNthFrame * 20) == 0)
                    {
                        Debug.Log($"{target.targetName} - Good matches: {goodMatches.Count}");
                    }
                }
            }

            matches.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error checking marker {target.targetName}: {e.Message}");
        }
    }

    List<DMatch> FilterGoodMatches(DMatch[] matches, float distanceThreshold)
    {
        List<DMatch> goodMatches = new List<DMatch>();

        if (matches.Length == 0) return goodMatches;

        // Find min distance
        float minDist = float.MaxValue;
        foreach (DMatch match in matches)
        {
            if (match.distance < minDist)
                minDist = match.distance;
        }

        // Filter matches
        float threshold = Mathf.Max(2 * minDist, distanceThreshold);
        foreach (DMatch match in matches)
        {
            if (match.distance <= threshold)
            {
                goodMatches.Add(match);
            }
        }

        return goodMatches;
    }

    void OnTargetDetected(OpenCVMarkerTarget target)
    {
        if (!target.isCurrentlyTracked)
        {
            target.isCurrentlyTracked = true;

            // Update tracking behaviour status
            if (target.trackingBehaviour != null)
            {
                target.trackingBehaviour.HandleTargetFound();
            }

            // Fire Unity events
            target.OnTargetFound?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"Target found: {target.targetName}");
            }
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

    void OnTargetLost(OpenCVMarkerTarget target)
    {
        target.isCurrentlyTracked = false;

        // Update tracking behaviour status
        if (target.trackingBehaviour != null)
        {
            target.trackingBehaviour.HandleTargetLost();
        }

        // Fire Unity events
        target.OnTargetLost?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log($"Target lost: {target.targetName}");
        }
    }

    void OnDestroy()
    {
        // Cleanup camera
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            webCamTexture = null;
        }

        // Cleanup OpenCV resources
        currentFrame?.Dispose();
        grayFrame?.Dispose();
        detector?.Dispose();
        matcher?.Dispose();

        // Cleanup marker targets
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (markerTargets[i] != null)
            {
                markerTargets[i].grayMarker?.Dispose();
                markerTargets[i].markerDescriptors?.Dispose();
                markerTargets[i].markerKeypoints?.Dispose();
            }
        }
    }

    // Public methods for external access
    public bool IsTargetTracked(string targetName)
    {
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (markerTargets[i].targetName == targetName)
                return markerTargets[i].isCurrentlyTracked;
        }
        return false;
    }

    public OpenCVMarkerTarget GetTarget(string targetName)
    {
        for (int i = 0; i < markerTargets.Length; i++)
        {
            if (markerTargets[i].targetName == targetName)
                return markerTargets[i];
        }
        return null;
    }

    public WebCamTexture GetCameraTexture()
    {
        return webCamTexture;
    }
}