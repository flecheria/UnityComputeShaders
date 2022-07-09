using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// https://www.ronja-tutorials.com/post/050-compute-shader/
// https://www.ronja-tutorials.com/post/024-white-noise/

public class PlayWithSpheres : MonoBehaviour
{
    [SerializeField]
    private int _sphereAmount = 17;
    [SerializeField]
    private ComputeShader _shader;
    [SerializeField]
    private GameObject _prefab;
    [SerializeField]
    private string[] _kernelNames;
    [SerializeField]
    [Range(0, 1)]
    private int _kernelId;

    ComputeBuffer _resultBuffer;
    int _kernel;
    uint _threadGroupSize;
    Vector3[] _output;
    private Transform[] _instances;

    // Start is called before the first frame update
    void Start()
    {
        //program we're executing
        _kernel = _shader.FindKernel(_kernelNames[_kernelId]);

        _shader.GetKernelThreadGroupSizes(_kernel, out _threadGroupSize, out _, out _);

        //buffer on the gpu in the ram
        _resultBuffer = new ComputeBuffer(_sphereAmount, sizeof(float) * 3);
        _output = new Vector3[_sphereAmount];

        //spheres we use for visualisation
        _instances = new Transform[_sphereAmount];
        for (int i = 0; i < _sphereAmount; i++)
        {
            _instances[i] = Instantiate(_prefab, transform).transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        _shader.SetFloat("time", Time.time);
        _shader.SetBuffer(_kernel, "Result", _resultBuffer);
        int threadGroups = (int)((_sphereAmount + (_threadGroupSize - 1)) / _threadGroupSize);
        _shader.Dispatch(_kernel, threadGroups, 1, 1);
        _resultBuffer.GetData(_output);

        // update sphere position
        for (int i = 0; i < _instances.Length; i++)
            _instances[i].localPosition = _output[i];
    }

    private void OnDestroy()
    {
        _resultBuffer.Dispose();
    }
}
