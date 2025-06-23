using UnityEngine;
using UnityEngine.Events;

public enum OpenCVTrackingStatus
{
    NotTracked,
    Limited,
    Tracked,
    ExtendedTracked
}

public class OpenCVTrackingBehaviour : MonoBehaviour
{
    [Header("Tracking Settings")]
    public OpenCVTrackingStatus currentStatus = OpenCVTrackingStatus.NotTracked;
    public bool usePoseSmoothing = false;
    public AnimationCurve smoothingCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);

    [Header("Events")]
    public UnityEvent OnTargetFound;
    public UnityEvent OnTargetLost;

    [Header("Debug")]
    public bool showDebugInfo = false;

    private string targetName;
    private bool isTracking = false;
    private float smoothingTime = 0f;
    private Vector3 originalScale;

    public void Initialize(string name)
    {
        targetName = name;
        originalScale = transform.localScale;

        // Start hidden
        gameObject.SetActive(false);

        if (showDebugInfo)
        {
            Debug.Log($"OpenCV Tracking Behaviour initialized for: {targetName}");
        }
    }

    public void HandleTargetFound()
    {
        if (!isTracking)
        {
            isTracking = true;
            currentStatus = OpenCVTrackingStatus.Tracked;

            // Show the target
            gameObject.SetActive(true);

            // Reset smoothing
            if (usePoseSmoothing)
            {
                smoothingTime = 0f;
                StartCoroutine(SmoothAppearance());
            }

            // Fire events
            OnTargetFound?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"OpenCV Target Found: {targetName}");
            }
        }
    }

    public void HandleTargetLost()
    {
        if (isTracking)
        {
            isTracking = false;
            currentStatus = OpenCVTrackingStatus.NotTracked;

            // Fire events
            OnTargetLost?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"OpenCV Target Lost: {targetName}");
            }
        }
    }

    System.Collections.IEnumerator SmoothAppearance()
    {
        Vector3 startScale = Vector3.zero;

        while (smoothingTime < smoothingCurve.keys[smoothingCurve.length - 1].time)
        {
            smoothingTime += Time.deltaTime;
            float curveValue = smoothingCurve.Evaluate(smoothingTime);
            transform.localScale = Vector3.Lerp(startScale, originalScale, curveValue);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    // Public methods to replicate Vuforia functionality
    public bool IsTracked()
    {
        return isTracking && currentStatus == OpenCVTrackingStatus.Tracked;
    }

    public bool IsExtendedTracked()
    {
        return isTracking && currentStatus == OpenCVTrackingStatus.ExtendedTracked;
    }

    public void SetTrackingStatus(OpenCVTrackingStatus status)
    {
        currentStatus = status;
    }
}