using UnityEngine;
using System.Collections;

public class PassData : MonoBehaviour
{

    public ComputeShader shader;
    public int texResolution = 1024;

    Renderer rend;
    RenderTexture outputTexture;

    int circlesHandle;
    int clearHandle;

    public Color clearColor = new Color();
    public Color circleColor = new Color();

    // Use this for initialization
    void Start()
    {
        outputTexture = new RenderTexture(texResolution, texResolution, 0);
        outputTexture.enableRandomWrite = true;
        outputTexture.Create();

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        InitShader();
    }

    private void InitShader()
    {
        circlesHandle = shader.FindKernel("Circles");
        clearHandle = shader.FindKernel("Clear");

        shader.SetInt( "texResolution", texResolution);
        shader.SetVector("clearColor", clearColor);
        shader.SetVector("circleColor", circleColor);

        shader.SetTexture(circlesHandle, "Result", outputTexture);
        shader.SetTexture(clearHandle, "Result", outputTexture);

        rend.material.SetTexture("_MainTex", outputTexture);
    }
 
    private void DispatchKernels(int count)
    {
        shader.Dispatch(clearHandle, texResolution / 8, texResolution / 8, 1);

        // set time every single running phase of the shader to get animation
        shader.SetFloat("time", Time.time);
        shader.Dispatch(circlesHandle, count, 1, 1);

        // how many times the sjader kernel run?
        // Dispatch GroupID (1,1,1) = 1 * 1 * 1 = 1
        // Threads (1, 1, 1) = 1 * 1 * 1 = 1
        // Total run = Dispatch Group * Threads = 1 * 1 = 1
    }

    void Update()
    {
        // the integer value decide how many time the shader run
        DispatchKernels(10);
    }

    private void OnDestroy()
    {
        outputTexture.Release();
    }
}

