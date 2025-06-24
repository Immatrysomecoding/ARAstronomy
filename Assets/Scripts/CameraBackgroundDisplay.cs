using UnityEngine;

public class CameraBackgroundDisplay : MonoBehaviour
{
    [Header("Background Settings")]
    public Material backgroundMaterial;
    public bool autoCreateBackground = true;

    private GameObject backgroundQuad;
    private Camera arCamera;
    private OpenCVCameraManager cameraManager;
    private Renderer backgroundRenderer;

    void Start()
    {
        arCamera = Camera.main;
        cameraManager = GetComponent<OpenCVCameraManager>();

        if (autoCreateBackground)
        {
            CreateCameraBackground();
        }

        // Update background texture every frame
        StartCoroutine(UpdateBackgroundTexture());
    }

    void CreateCameraBackground()
    {
        // Create background quad
        backgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundQuad.name = "Camera Background";
        backgroundQuad.transform.parent = transform;

        // Position in front of camera
        backgroundQuad.transform.localPosition = new Vector3(0, 0, arCamera.nearClipPlane + 0.01f);
        backgroundQuad.transform.localRotation = Quaternion.identity;

        // Remove collider
        Collider quadCollider = backgroundQuad.GetComponent<Collider>();
        if (quadCollider != null) DestroyImmediate(quadCollider);

        // Scale to fit camera view
        float distance = Vector3.Distance(arCamera.transform.position, backgroundQuad.transform.position);
        float frustumHeight = 2.0f * distance * Mathf.Tan(arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * arCamera.aspect;
        backgroundQuad.transform.localScale = new Vector3(frustumWidth, frustumHeight, 1);

        // Setup material
        backgroundRenderer = backgroundQuad.GetComponent<Renderer>();
        if (backgroundMaterial != null)
        {
            backgroundRenderer.material = backgroundMaterial;
        }
        else
        {
            Material defaultMaterial = new Material(Shader.Find("Unlit/Texture"));
            defaultMaterial.name = "Camera Background Material";
            backgroundRenderer.material = defaultMaterial;
        }

        // Set render queue to render behind everything
        backgroundRenderer.material.renderQueue = 1000;

        Debug.Log("Camera background created");
    }

    System.Collections.IEnumerator UpdateBackgroundTexture()
    {
        // Wait for camera manager to initialize
        while (cameraManager == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Wait for camera to start
        yield return new WaitForSeconds(2f);

        // Continuously update background texture
        while (true)
        {
            if (backgroundRenderer != null && cameraManager != null)
            {
                WebCamTexture camTexture = cameraManager.GetCameraTexture();
                if (camTexture != null && camTexture.isPlaying)
                {
                    backgroundRenderer.material.mainTexture = camTexture;
                }
            }
            yield return new WaitForSeconds(0.1f); // Update 10 times per second
        }
    }

    void OnDestroy()
    {
        if (backgroundQuad != null)
        {
            DestroyImmediate(backgroundQuad);
        }
    }
}