using UnityEngine;
using System.Collections;

public class MyBufferJoy : MonoBehaviour
{

    public ComputeShader _shader;
    public int texResolution = 1024;

    Renderer rend;
    RenderTexture outputTexture;

    int _circlesHandle;
    int clearHandle;

    public Color clearColor = new Color();
    public Color circleColor = new Color();

    struct Circle
    {
        public Vector2 origin;
        public Vector2 velocity;
        public float radius;
    }

    [SerializeField]
    private int _count = 10;
    Circle[] _circleData; // these are the CPU data
    ComputeBuffer _buffer; // these are the GPU data

    // Use this for initialization
    void Start()
    {
        outputTexture = new RenderTexture(texResolution, texResolution, 0);
        outputTexture.enableRandomWrite = true;
        outputTexture.Create();

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        InitData();

        InitShader();
    }

    private void InitData()
    {
        _circlesHandle = _shader.FindKernel("Circles");
        
        // prepare data on the cpu
        // get the number of thread from the compute shader
        uint threadGroupSizeX;
        _shader.GetKernelThreadGroupSizes(_circlesHandle,
            out threadGroupSizeX, out _, out _);

        int total = (int)threadGroupSizeX * _count;
        _circleData = new Circle[total];

        float speed = 100;
        float halfSpeed = speed * 0.5f;
        float radiusRange = 30.0f - 10.0f;

        for (int i = 0; i < total; i += 1)
        {
            Circle circle = _circleData[i];
            circle.origin.x = Random.value * texResolution;
            circle.origin.y = Random.value * texResolution;
            circle.velocity.x = (Random.value * speed) - halfSpeed;
            circle.velocity.y = (Random.value * speed) - halfSpeed;
            circle.radius = (Random.value * radiusRange) + 10.0f;
            _circleData[i] = circle;
        }
    }

    private void InitShader()
    {
        clearHandle = _shader.FindKernel("Clear");

        _shader.SetVector("clearColor", clearColor);
        _shader.SetVector("circleColor", circleColor);
        _shader.SetInt("texResolution", texResolution);

        _shader.SetTexture(clearHandle, "Result", outputTexture);
        _shader.SetTexture(_circlesHandle, "Result", outputTexture);

        // pass data to the compute shader using buffer
        // calcolate the buffer size of the circle struct
        int stride = (2 + 2 + 1) * sizeof(float);
        _buffer = new ComputeBuffer(_circleData.Length, stride);
        _buffer.SetData(_circleData);
        _shader.SetBuffer(_circlesHandle, "circlesBuffer", _buffer);

        rend.material.SetTexture("_MainTex", outputTexture);
    }

    private void DispatchKernels(int count)
    {
        _shader.Dispatch(clearHandle, texResolution / 8, texResolution / 8, 1);
        _shader.SetFloat("time", Time.time);
        _shader.Dispatch(_circlesHandle, count, 1, 1);
    }

    void Update()
    {
        DispatchKernels(_count);
    }
}

