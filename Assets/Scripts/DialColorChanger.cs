using UnityEngine;

public class DialColorChanger : MonoBehaviour, I_DialInteractable
{
    public MeshRenderer targetRenderer;
    public Color startColor = Color.white;
    public Color endColor = Color.red;

    public void OnProgressChanged(float progress)
    {
        if (targetRenderer != null)
            targetRenderer.material.color = Color.Lerp(startColor, endColor, progress);
    }
}