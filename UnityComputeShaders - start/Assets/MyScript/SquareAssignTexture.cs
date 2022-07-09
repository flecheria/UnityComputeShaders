using UnityEngine;

public class SquareAssignTexture : MonoBehaviour
{
    [SerializeField]
    private ComputeShader _shader;
    [SerializeField]
    private int _texResolution = 256;
    [SerializeField]
    private string _kernelsName;

    private Renderer _render;
    private RenderTexture _outputTex;
    private int _kernelHandle;

    // Start is called before the first frame update
    void Start()
    {
        _outputTex = new RenderTexture(_texResolution, _texResolution, 0);
        _outputTex.enableRandomWrite = true;
        _outputTex.Create();

        _render = GetComponent<Renderer>();
        _render.enabled = true;

        InitShader();
    }

    private void InitShader()
    {
        // choose kernel to use
        _kernelHandle = _shader.FindKernel(_kernelsName);

        // create info for rectangle
        int halfRes = _texResolution >> 1;
        int quarterRes = _texResolution >> 2;
        //                         x=x pos     y=y pos     z=witdh  w=height
        Vector4 rect = new Vector4(quarterRes, quarterRes, halfRes, halfRes);
        _shader.SetVector("rect", rect);

        _shader.SetInt("texResolution", _texResolution);
        _shader.SetTexture(_kernelHandle, "Result", _outputTex);
        _render.material.SetTexture("_MainTex", _outputTex);

        DispatchShader(_texResolution / 16, _texResolution / 16);
    }

    private void DispatchShader(int x, int y)
    {
        _shader.Dispatch(_kernelHandle, x, y, 1);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.U))
        {
            DispatchShader(_texResolution / 8, _texResolution / 8);
        }
    }

    private void OnDestroy()
    {
        _outputTex.Release(); // Remove the render texture from graphics memory
    }
}
