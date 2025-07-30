using UnityEngine;
using UnityEngine.Events;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.Calib3dModule;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

[System.Serializable]
public class OpenCVMarkerTarget
{
    [Header("Target Setup")]
    public string targetName;
    public Texture2D markerImage;
    public GameObject targetObject;

    [Header("Detection Settings")]
    public int minMatches = 15;
    public float matchDistanceThreshold = 0.7f; 
    public float ratioThreshold = 0.75f; 
    public float homographyReprojectionThreshold = 3.0f;

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
    [System.NonSerialized]
    public int consecutiveDetections = 0; 
    [System.NonSerialized]
    public int consecutiveLosses = 0; 
}

public class OpenCVCameraManager : MonoBehaviour
{
    [Header("Camera Settings")]
    public int cameraIndex = 0;
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public bool useBackCamera = true;

    [Header("Performance Settings")]
    public int processEveryNthFrame = 2; 
    public int maxFeatures = 1000;
    public float trackingTimeout = 1.0f; 

    [Header("Detection Thresholds")]
    public int minConsecutiveDetections = 2; 
    public int minConsecutiveLosses = 3;
    public bool useHomographyVerification = true; 
    public float minHomographyInlierRatio = 0.4f;

    [Header("All Marker Targets")]
    public OpenCVMarkerTarget[] markerTargets;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showVisualDebug = false;

    private WebCamTexture webCamTexture;
    private Mat currentFrame;
    private Mat grayFrame;
    private ORB detector;
    private BFMatcher matcher;

    private int frameCounter = 0;
    private bool isInitialized = false;
    private Camera arCamera;

    private Texture2D debugTexture;

    void Start()
    {
        StartCoroutine(InitializeSystem());
    }

    IEnumerator InitializeSystem()
    {
        arCamera = Camera.main;

        InitializeCamera();

        yield return new WaitForSeconds(1f);

        InitializeOpenCV();

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


    //ORB + Brute-Force Matcher với Hamming distance
    //FAST + BRIEF
    //Check 15-16 pixel xung quanh nó
    //tìm các pixel có cường độ thay đổi mạnh(góc biên các kiểu) rồi cho ra phần brief số bit khác nhau.
    void InitializeOpenCV()
    {
        //ORB(Oriented FAST and Rotated BRIEF)
        detector = ORB.create(maxFeatures);

        //Norm Hamming để check
        matcher = BFMatcher.create(Core.NORM_HAMMING, crossCheck: false);

        // Check từng image target
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


    //Gọi bên tracking behavior
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

                //Này khởi tạo
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


    //xử lý ảnh marker và trích xuất đặc trưng dùng orb ở trên xong lưu
    bool ProcessMarkerImage(OpenCVMarkerTarget target)
    {
        if (target.markerImage == null)
        {
            Debug.LogWarning($"Marker image not assigned for target: {target.targetName}");
            return false;
        }

        try
        {
            // Chuyển thành opencv format
            Mat markerMat = new Mat(target.markerImage.height, target.markerImage.width, CvType.CV_8UC4);
            Utils.texture2DToMat(target.markerImage, markerMat);

            //grayscale
            target.grayMarker = new Mat();
            Imgproc.cvtColor(markerMat, target.grayMarker, Imgproc.COLOR_RGBA2GRAY);

            Mat processedMarker = new Mat();

            // histogram để tăng tương phản
            Imgproc.equalizeHist(target.grayMarker, processedMarker);

            // Gausian blur giảm nhiễu
            Imgproc.GaussianBlur(processedMarker, processedMarker, new Size(3, 3), 0);

            //ORB
            target.markerKeypoints = new MatOfKeyPoint();
            target.markerDescriptors = new Mat();
            detector.detectAndCompute(processedMarker, new Mat(), target.markerKeypoints, target.markerDescriptors);

            if (showDebugInfo)
            {
                Debug.Log($"Processed marker {target.targetName}: {target.markerKeypoints.toArray().Length} features detected");
            }

            markerMat.Dispose();
            processedMarker.Dispose();
            return true;
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

        // Tải trọng frame default là 3
        if (frameCounter % processEveryNthFrame == 0)
        {
            ProcessCurrentFrame();
        }
        CheckTrackingTimeouts();
    }


    //Lấy frame unity->opencv mat
    void ProcessCurrentFrame()
    {
        try
        {
            //Khởi tạo
            if (currentFrame == null)
                currentFrame = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);

            // Convert
            Utils.webCamTextureToMat(webCamTexture, currentFrame);

            // grayscale
            if (grayFrame == null)
                grayFrame = new Mat();

            Imgproc.cvtColor(currentFrame, grayFrame, Imgproc.COLOR_RGBA2GRAY);
            Mat processedFrame = new Mat();
            Imgproc.equalizeHist(grayFrame, processedFrame);

            // detect features ORB
            MatOfKeyPoint currentKeypoints = new MatOfKeyPoint();
            Mat currentDescriptors = new Mat();
            detector.detectAndCompute(processedFrame, new Mat(), currentKeypoints, currentDescriptors);

            if (currentDescriptors.rows() > 0)
            {
                for (int i = 0; i < markerTargets.Length; i++)
                {
                    CheckMarkerTarget(markerTargets[i], currentDescriptors, currentKeypoints);
                }
            }
            currentKeypoints.Dispose();
            currentDescriptors.Dispose();
            processedFrame.Dispose();
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
            // KNN k = 2
            List<MatOfDMatch> knnMatches = new List<MatOfDMatch>();
            matcher.knnMatch(target.markerDescriptors, currentDescriptors, knnMatches, 2);

            //Lowe's ratio test
            List<DMatch> goodMatches = new List<DMatch>();

            foreach (MatOfDMatch knnMatch in knnMatches)
            {
                DMatch[] matches = knnMatch.toArray();
                if (matches.Length >= 2)
                {
                    // Ratio test
                    if (matches[0].distance < target.ratioThreshold * matches[1].distance)
                    {
                        goodMatches.Add(matches[0]);
                    }
                }
                knnMatch.Dispose();
            }

            // Lọc distance hamming max 255 pixel
            goodMatches = goodMatches.Where(m => m.distance < target.matchDistanceThreshold * 255).ToList();

            bool detectionValid = false;

            // check matches
            if (goodMatches.Count >= target.minMatches)
            {
                if (useHomographyVerification)
                {
                    // check homo coi có đủ điểm đòng phẳng không
                    detectionValid = VerifyWithHomography(target, goodMatches, currentKeypoints);
                }
                else
                {
                    detectionValid = true;
                }
            }

            // check lập Hysteresis
            if (detectionValid)
            {
                target.consecutiveDetections++;
                target.consecutiveLosses = 0;
                //minConsecutiveDetections
                if (target.consecutiveDetections >= minConsecutiveDetections)
                {
                    OnTargetDetected(target);
                    target.lastDetectionTime = Time.time;
                }

                if (showDebugInfo && frameCounter % (processEveryNthFrame * 20) == 0)
                {
                    Debug.Log($"{target.targetName} - Good matches: {goodMatches.Count}, Consecutive: {target.consecutiveDetections}");
                }
            }
            else
            {
                target.consecutiveDetections = 0;
                target.consecutiveLosses++;
                //minConsecutiveLosses
                if (target.consecutiveLosses >= minConsecutiveLosses && target.isCurrentlyTracked)
                {
                    // xóa state tracking
                    if (Time.time - target.lastDetectionTime > trackingTimeout / 2)
                    {
                        OnTargetLost(target);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error checking marker {target.targetName}: {e.Message}");
        }
    }


    //Check homo
    //Lấy matches từ CheckMarkerTarget lấy knn matches -> MatofMatch
    bool VerifyWithHomography(OpenCVMarkerTarget target, List<DMatch> matches, MatOfKeyPoint currentKeypoints)
    {
        if (matches.Count < 4) //4 điểm homography, 8 phương trình cho ma trận 3x3
            return false;

        try
        {
            // Trích xuất điểm keypoint tương ứng từ marker và camera
            List<Point> markerPoints = new List<Point>();
            List<Point> scenePoints = new List<Point>();

            KeyPoint[] markerKp = target.markerKeypoints.toArray();
            KeyPoint[] sceneKp = currentKeypoints.toArray();

            //Chuyển MatOfKeyPoint sang mảng để dễ truy cập từng điểm
            foreach (DMatch match in matches)
            {
                markerPoints.Add(markerKp[match.queryIdx].pt); // điểm từ marker
                scenePoints.Add(sceneKp[match.trainIdx].pt); // điểm từ ảnh hiện tại
            }

            //Chuyển danh sách Point sang MatOfPoint2f
            MatOfPoint2f markerMat = new MatOfPoint2f();
            markerMat.fromList(markerPoints);
            MatOfPoint2f sceneMat = new MatOfPoint2f();
            sceneMat.fromList(scenePoints);

            // Tính Ransac
            Mat mask = new Mat();
            Mat homography = Calib3d.findHomography(markerMat, sceneMat, Calib3d.RANSAC, target.homographyReprojectionThreshold, mask);

            if (!homography.empty())
            {
                // Tính tỷ lệ inliers
                byte[] maskArray = new byte[mask.total()];
                mask.get(0, 0, maskArray);
                int inliers = maskArray.Count(b => b != 0);

                float inlierRatio = (float)inliers / matches.Count;

                //Clean
                markerMat.Dispose();
                sceneMat.Dispose();
                mask.Dispose();
                homography.Dispose();

                return inlierRatio >= minHomographyInlierRatio;
            }

            //Clean
            markerMat.Dispose();
            sceneMat.Dispose();
            mask.Dispose();

            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Homography verification error: {e.Message}");
            return false;
        }
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

    void OnTargetLost(OpenCVMarkerTarget target)
    {
        target.isCurrentlyTracked = false;
        target.consecutiveDetections = 0;
        target.consecutiveLosses = 0;

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
