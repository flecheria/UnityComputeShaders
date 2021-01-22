﻿// StableFluids - A GPU implementation of Jos Stam's Stable Fluids on Unity
// adapted from https://github.com/keijiro/StableFluids

using UnityEngine;


public class StableFluids : MonoBehaviour
{
    public int resolution = 512;
    public float viscosity = 1e-6f;
    public float force = 300;
    public float exponent = 200;
    public Texture2D initial;
    public ComputeShader compute;
    public Material material;

   
    Vector2 previousInput;

    int kernelAdvect;
    int kernelForce;
    int kernelPSetup;
    int kernelPFinish;
    int kernelJacobi1;
    int kernelJacobi2;

    int threadCountX { get { return (resolution + 7) / 8; } }
    int threadCountY { get { return (resolution * Screen.height / Screen.width + 7) / 8; } }

    int resolutionX { get { return threadCountX * 8; } }
    int resolutionY { get { return threadCountY * 8; } }

    // Vector field buffers
    RenderTexture vfbRTV1;
    RenderTexture vfbRTV2;
    RenderTexture vfbRTV3;
    RenderTexture vfbRTP1;
    RenderTexture vfbRTP2;

    // Color buffers (for double buffering)
    RenderTexture colorRT1;
    RenderTexture colorRT2;

    RenderTexture CreateRenderTexture(int componentCount, int width = 0, int height = 0)
    {
        var format = RenderTextureFormat.ARGBHalf;
        if (componentCount == 1) format = RenderTextureFormat.RHalf;
        if (componentCount == 2) format = RenderTextureFormat.RGHalf;

        if (width == 0) width = resolutionX;
        if (height == 0) height = resolutionY;

        var rt = new RenderTexture(width, height, 0, format);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    
    void OnValidate()
    {
        resolution = Mathf.Max(resolution, 8);
    }

    void Start()
    {
        InitBuffers();
        InitShader();

        Graphics.Blit(initial, colorRT1);
    }

    void InitBuffers()
    {
        vfbRTV1 = CreateRenderTexture(2);
        vfbRTV2 = CreateRenderTexture(2);
        vfbRTV3 = CreateRenderTexture(2);
        vfbRTP1 = CreateRenderTexture(1);
        vfbRTP2 = CreateRenderTexture(1);

        colorRT1 = CreateRenderTexture(4, Screen.width, Screen.height);
        colorRT2 = CreateRenderTexture(4, Screen.width, Screen.height);
    }

    void InitShader()
    {
        kernelAdvect = compute.FindKernel("Advect");
        kernelForce = compute.FindKernel("Force");
        kernelPSetup = compute.FindKernel("PSetup");
        kernelPFinish = compute.FindKernel("PFinish");
        kernelJacobi1 = compute.FindKernel("Jacobi1");
        kernelJacobi2 = compute.FindKernel("Jacobi2");
    }

    void OnDestroy()
    {
        Destroy(vfbRTV1);
        Destroy(vfbRTV2);
        Destroy(vfbRTV3);
        Destroy(vfbRTP1);
        Destroy(vfbRTP2);

        Destroy(colorRT1);
        Destroy(colorRT2);
    }

    void Update()
    {
        var dt = Time.deltaTime;
        var dx = 1.0f / resolutionY;

        // Input point
        var input = new Vector2(
            (Input.mousePosition.x - Screen.width * 0.5f) / Screen.height,
            (Input.mousePosition.y - Screen.height * 0.5f) / Screen.height
        );

        // Common variables
        compute.SetFloat("Time", Time.time);
        compute.SetFloat("DeltaTime", dt);

        // Advection
        compute.SetTexture(kernelAdvect, "U_in", vfbRTV1);
        compute.SetTexture(kernelAdvect, "W_out", vfbRTV2);
        compute.Dispatch(kernelAdvect, threadCountX, threadCountY, 1);

        // Diffuse setup
        var difalpha = dx * dx / (viscosity * dt);
        compute.SetFloat("Alpha", difalpha);
        compute.SetFloat("Beta", 4 + difalpha);
        Graphics.CopyTexture(vfbRTV2, vfbRTV1);
        compute.SetTexture(kernelJacobi2, "B2_in", vfbRTV1);

        // Jacobi iteration
        for (var i = 0; i < 20; i++)
        {
            compute.SetTexture(kernelJacobi2, "X2_in", vfbRTV2);
            compute.SetTexture(kernelJacobi2, "X2_out", vfbRTV3);
            compute.Dispatch(kernelJacobi2, threadCountX, threadCountY, 1);

            compute.SetTexture(kernelJacobi2, "X2_in", vfbRTV3);
            compute.SetTexture(kernelJacobi2, "X2_out", vfbRTV2);
            compute.Dispatch(kernelJacobi2, threadCountX, threadCountY, 1);
        }

        // Add external force
        compute.SetVector("ForceOrigin", input);
        compute.SetFloat("ForceExponent", exponent);
        compute.SetTexture(kernelForce, "W_in", vfbRTV2);
        compute.SetTexture(kernelForce, "W_out", vfbRTV3);

        if (Input.GetMouseButton(1))
            // Random push
            compute.SetVector("ForceVector", Random.insideUnitCircle * force * 0.025f);
        else if (Input.GetMouseButton(0))
            // Mouse drag
            compute.SetVector("ForceVector", (input - previousInput) * force);
        else
            compute.SetVector("ForceVector", Vector4.zero);

        compute.Dispatch(kernelForce, threadCountX, threadCountY, 1);

        // Projection setup
        compute.SetTexture(kernelPSetup, "W_in", vfbRTV3);
        compute.SetTexture(kernelPSetup, "DivW_out", vfbRTV2);
        compute.SetTexture(kernelPSetup, "P_out", vfbRTP1);
        compute.Dispatch(kernelPSetup, threadCountX, threadCountY, 1);

        // Jacobi iteration
        compute.SetFloat("Alpha", -dx * dx);
        compute.SetFloat("Beta", 4);
        compute.SetTexture(kernelJacobi1, "B1_in", vfbRTV2);

        for (var i = 0; i < 20; i++)
        {
            compute.SetTexture(kernelJacobi1, "X1_in", vfbRTP1);
            compute.SetTexture(kernelJacobi1, "X1_out", vfbRTP2);
            compute.Dispatch(kernelJacobi1, threadCountX, threadCountY, 1);

            compute.SetTexture(kernelJacobi1, "X1_in", vfbRTP2);
            compute.SetTexture(kernelJacobi1, "X1_out", vfbRTP1);
            compute.Dispatch(kernelJacobi1, threadCountX, threadCountY, 1);
        }

        // Projection finish
        compute.SetTexture(kernelPFinish, "W_in", vfbRTV3);
        compute.SetTexture(kernelPFinish, "P_in", vfbRTP1);
        compute.SetTexture(kernelPFinish, "U_out", vfbRTV1);
        compute.Dispatch(kernelPFinish, threadCountX, threadCountY, 1);

        // Apply the velocity field to the color buffer.
        var offs = Vector2.one * (Input.GetMouseButton(1) ? 0 : 1e+7f);
        material.SetVector("_ForceOrigin", input + offs);
        material.SetFloat("_ForceExponent", exponent);
        material.SetTexture("_VelocityField", vfbRTV1);
        Graphics.Blit(colorRT1, colorRT2, material, 0);

        // Swap the color buffers.
        var temp = colorRT1;
        colorRT1 = colorRT2;
        colorRT2 = temp;

        previousInput = input;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(colorRT1, destination, material, 1);
    }
}