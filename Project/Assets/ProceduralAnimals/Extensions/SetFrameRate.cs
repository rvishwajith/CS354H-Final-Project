using UnityEngine;

public class SetFrameRate : MonoBehaviour
{
    // The default frame rate to set. -1 means there is no frame rate.
    [SerializeField] int targetFrameRate = -1;

    void Start()
    {
        // Set the frame rate just before rendering starts.
        Application.targetFrameRate = targetFrameRate;
    }

    void Update()
    {
        if (Application.targetFrameRate == targetFrameRate)
            return;

        Application.targetFrameRate = targetFrameRate;
        if (targetFrameRate == -1)
            Debug.Log("Removed frame rate limiter.");
        else
            Debug.Log("Changed target frame rate to " + targetFrameRate + ".");
    }
}