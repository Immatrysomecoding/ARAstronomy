using System.Collections;
using UnityEngine;

public class Tracking : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform imageTarget; // Your planet object (like 03 earth)

    [Header("Behavior Settings")]
    public float hideDelay = 2f; // Same 2-second delay as before
    public bool showDebugLogs = false;

    // Private variables
    private bool isCurrentlyVisible = false;
    private Coroutine hideCoroutine;
    private OpenCVTrackingBehaviour openCVTracker;

    void Start()
    {
        // Get OpenCV tracker component
        openCVTracker = GetComponent<OpenCVTrackingBehaviour>();

        // Start with target hidden (like Vuforia)
        if (imageTarget != null)
        {
            imageTarget.gameObject.SetActive(false);

            if (showDebugLogs)
            {
                Debug.Log($"Tracking initialized for: {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"Image target not assigned for: {gameObject.name}");
        }
    }

    // Called by OpenCV when marker is detected
    public void OnTargetFound()
    {
        if (imageTarget == null) return;

        // Cancel any pending hide operation
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        // Show the target immediately (like Vuforia)
        if (!isCurrentlyVisible)
        {
            imageTarget.gameObject.SetActive(true);
            isCurrentlyVisible = true;

            if (showDebugLogs)
            {
                Debug.Log($"Target found - Showing: {gameObject.name}");
            }
        }
    }

    // Called by OpenCV when marker is lost
    public void OnTargetLost()
    {
        if (imageTarget == null) return;

        if (isCurrentlyVisible)
        {
            // Start the same 2-second delay as before
            hideCoroutine = StartCoroutine(waitAndHide());

            if (showDebugLogs)
            {
                Debug.Log($"Target lost - Starting hide timer for: {gameObject.name}");
            }
        }
    }

    // Original method - keep for compatibility
    public void Track()
    {
        if (showDebugLogs)
        {
            Debug.Log($"Track() called for: {gameObject.name}");
        }

        StartCoroutine(waitAndHide());
    }

    // Original logic - EXACTLY the same as before
    IEnumerator waitAndHide()
    {
        // Wait 2 seconds (same as original)
        yield return new WaitForSeconds(hideDelay);

        // Remove parent (same as original)
        transform.parent = null;

        // Hide the target (same as original)
        if (imageTarget != null)
        {
            imageTarget.gameObject.SetActive(false);
            isCurrentlyVisible = false;
        }

        if (showDebugLogs)
        {
            Debug.Log($"Target hidden after delay: {gameObject.name}");
        }

        hideCoroutine = null;
    }

    // Public methods for external control
    public bool IsVisible()
    {
        return isCurrentlyVisible;
    }

    public void ForceHide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (imageTarget != null)
        {
            imageTarget.gameObject.SetActive(false);
            isCurrentlyVisible = false;
        }

        transform.parent = null;
    }

    public void ForceShow()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (imageTarget != null)
        {
            imageTarget.gameObject.SetActive(true);
            isCurrentlyVisible = true;
        }
    }

    // Handle component being disabled
    void OnDisable()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }
}