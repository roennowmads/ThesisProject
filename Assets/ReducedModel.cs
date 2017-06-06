using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Text;
using System.IO;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System;

public class ReducedModel : MonoBehaviour {
    public string m_valueDataPath = "OilRigData";
    public int m_lastFrameIndex = 25;
    
    public float m_frameSpeed = 5.0f;
    public int m_textureSideSizePower = 14;

    private int m_pointsCount = 61440;
    private Renderer pointRenderer;

    private int m_textureSideSize;
    private int m_textureSize;
    private int m_lookupTextureSize = 256; //Since the values in the value texture are only 0-255, it doesn't make sense to have more values here.

    private int m_textureSwitchFrameNumber = -1;

    private ComputeBuffer positionComputebuffer, velocityComputebuffer;
    private int m_dimensionWidth, m_dimensionHeight, m_dimensionDepth;


    public ComputeShader computeShader;
    private int m_kernel;

    public int getPointCount () {
        return m_pointsCount;
    }

    public void setPointCount (int pointsCount) {
        m_pointsCount = pointsCount;
    }

    public void changePointsCount(int increment) {
        m_pointsCount += increment;
    }

    Texture2D createColorLookupTexture() {
        int numberOfValues = m_lookupTextureSize;

        Texture2D lookupTexture = new Texture2D(m_lookupTextureSize, 1, TextureFormat.RGB24, false, false);
        lookupTexture.filterMode = FilterMode.Point;
        lookupTexture.anisoLevel = 1;

        for (int i = 0; i < numberOfValues; i++) {
            float textureIndex = i;

            //0 - 255 --> 0.0 - 1.0
            float value = textureIndex / numberOfValues;

            var a = (1.0f - value) / 0.25f; //invert and group
            float X = Mathf.Floor(a);   //this is the integer part
            float Y = a - X; //fractional part from 0 to 255

            Color color;

            switch ((int)X) {
                case 0:
                    color = new Color(1.0f, Y, 0);
                    break;
                case 1:
                    color = new Color((1.0f - Y), 1.0f, 0);
                    break;
                case 2:
                    color = new Color(0, 1.0f, Y);
                    break;
                case 3:
                    color = new Color(0, (1.0f - Y), 1.0f);
                    break;
                case 4:
                    color = new Color(0, 0, 1.0f);
                    break;
                default:
                    color = new Color(1.0f, 0, 0);
                    break;
            }            
            lookupTexture.SetPixel(i, 0, color); 

            //alternatives: (necessary if I want to store only one component per pixel)
            //tex.LoadRawTextureData()
            //tex.SetPixels(x, y, width, height, colors.ToArray()); 
            //pixels are stored in rectangle blocks... maybe it would actually be better for caching anyway? problem is a frame's colors would need to fit in a rectangle.
        }

        lookupTexture.Apply();

        return lookupTexture;
    }

    IEnumerator readPointsFile1ValueAsync(int k, Texture2D tex2) {
        byte[] vals = new byte[m_textureSize];

        int index = 0;
        for (int i = k + 1; i < m_lastFrameIndex; i++) {

            TextAsset ta = Resources.Load(m_valueDataPath + "/frame" + i + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            byte[] bytes = ta.bytes;

            int frameSize = bytes.Length;

            Buffer.BlockCopy(bytes, 0, vals, index * frameSize, frameSize);
            index++;

        }
        tex2.LoadRawTextureData(vals);
        tex2.Apply();

        //pointRenderer = GetComponent<Renderer>();
        pointRenderer.material.SetTexture("_MainTex2", tex2);
        yield return 0;
    }

    void readPointsFile1Value(Texture2D tex, Texture2D tex2) {
        //CompressionHelper.CompressFile(path + "fireAtrium0." + fileIndex + ".bytes", "fireAtrium0." + fileIndex +".lzf");
        //byte[] bytes = CompressionHelper.DecompressFileToMem(path + "fireAtrium0." + fileIndex +".lzf");
        //float[] vals = new float[bytes.Length / 4];
        //Buffer.BlockCopy(bytes, 0, vals, 0, bytes.Length);

        bool loadingFromBytes = true;
        
        byte[] vals = new byte[m_textureSize];
        //byte[] vals2 = new byte[m_textureSize * m_textureSize];

        //int nextTexIndex = 0;

        if (loadingFromBytes) {
            for (int k = 0; k < m_lastFrameIndex; k++) {

                //http://answers.unity3d.com/questions/759469/why-doesnt-my-asynchronous-loading-work.html
                //StartCoroutine (LoadFrames ());

                //ResourceRequest bla = Resources.LoadAsync("AtriumData/frame" + k + "0.0", typeof(TextAsset));

                //while (!bla.isDone) {
                //    yield return 0;
                //}

                //TextAsset ta = Resources.Load("AtriumData/binaryDataFull/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
                TextAsset ta = Resources.Load(m_valueDataPath + "/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
                byte[] bytes = ta.bytes;

                int frameSize = bytes.Length;
                //Debug.Log(k * frameSize);
                /*if (k * frameSize < m_textureSize - frameSize) {
                    
                    Buffer.BlockCopy(bytes, 0, vals, k * frameSize, frameSize);
                    //nextTexIndex = k;
                }*/

                if (k * frameSize >= m_textureSize - frameSize) {
                    m_textureSwitchFrameNumber = k - 1;
                    StartCoroutine(readPointsFile1ValueAsync(k, tex2));
                    break;
                }
                Buffer.BlockCopy(bytes, 0, vals, k * frameSize, frameSize);
                


                //else {
                //    Debug.Log(k * frameSize - (nextTexIndex + 1) * frameSize);
                //    Buffer.BlockCopy(bytes, 0, vals2, k * frameSize - (nextTexIndex+1) * frameSize, frameSize);
                //}

                //Debug.Log(nextTexIndex);

                //times[k] = offset;
                //offset += m_pointsCount;
            }

            //CompressionHelper.CompressMemToFile(vals, "Assets/Resources/OilRigData/compressedValues.bytes");
        }
        else {
            TextAsset ta = Resources.Load(m_valueDataPath + "/compressedValues", typeof(TextAsset)) as TextAsset;
            vals = CompressionHelper.DecompressBytes(ta.bytes);

            for (int k = 0; k < m_lastFrameIndex; k++) {
                //times[k] = offset;
                //offset += m_pointsCount;
            }
        }

        //We will have to use textures for values because compute buffers require an element size of 4 bytes, hence we can't use bytes.
        //It could be done with bit shifting, but then any advantage would wear off.

        tex.LoadRawTextureData(vals);
        tex.Apply();
        //tex2.LoadRawTextureData(vals2);
        //tex2.Apply();


    } 

    float[] readPointsFile3Attribs()
    {
        TextAsset pointData = Resources.Load(m_valueDataPath + "/frame00.0.pos", typeof(TextAsset)) as TextAsset;
        byte[] bytes = pointData.bytes;
        float[] points = new float[(bytes.Length / 4)];
        Buffer.BlockCopy(bytes, 0, points, 0, bytes.Length);
        return points;
    }

    void Start () {
        //m_textureSideSize = 1 << m_textureSideSizePower;
        //m_textureSize = m_textureSideSize * m_textureSideSize;

        m_dimensionWidth = 512;
        m_dimensionHeight = 512;
        m_dimensionDepth = 1;
        Vector3[] points = new Vector3[m_dimensionWidth * m_dimensionHeight * m_dimensionDepth];
        Vector3 startingPosition = new Vector3(0.0f, 0.0f, 0.0f);
        
        float deltaPos = 0.05f;

        for (int i = 0; i < m_dimensionWidth; i++) {
            for (int j = 0; j < m_dimensionHeight; j++) {
                for (int k = 0; k < m_dimensionDepth; k++) {
                    int index = i + m_dimensionWidth * (j + m_dimensionDepth * k);
                    //int index = i + j * m_dimensionWidth;
                    points[index] = new Vector3();
                    float randomVal = UnityEngine.Random.Range(0.0f, 1.0f);
                    points[index].x = i * deltaPos - m_dimensionWidth*deltaPos + m_dimensionWidth*deltaPos*0.5f + randomVal*1.0f; // + randomVal - 0.5f + startingPosition.x;
                    randomVal = UnityEngine.Random.Range(0.0f, 1.0f);
                    points[index].y = j * deltaPos - m_dimensionHeight*deltaPos + m_dimensionHeight*deltaPos*0.5f + randomVal*1.0f; //- dimensionHeight*deltaPos + dimensionWidth*deltaPos*0.5f + randomVal - 0.5f + startingPosition.y;
                    //points[index].z = k * deltaPos - m_dimensionDepth*deltaPos + m_dimensionDepth*deltaPos*0.5f/* + randomVal*10.0f*/; //- dimensionHeight*deltaPos + dimensionWidth*deltaPos*0.5f + randomVal - 0.5f + startingPosition.y;
                    
                }
            }
        }

        Vector3[] velocities = new Vector3[points.Length];
        for (int i = 0; i < m_dimensionWidth; i++) {
            for (int j = 0; j < m_dimensionHeight; j++) {
                for (int k = 0; k < m_dimensionDepth; k++) {
                    int index = i + m_dimensionWidth * (j + m_dimensionDepth * k);//i + j * m_dimensionWidth;
                    velocities[index] = new Vector3();
                }
            }
        }

        //Set up mesh:
        m_pointsCount = points.Length;

         //Set up textures:
        /*Texture2D colorTexture = createColorLookupTexture();
        
        //We don't need more precision than the resolution of the colorTexture. 10 bits is sufficient for 1024 different color values.
        //That means we can pack 3 10bit integer values into a pixel 
        //Texture2D texture = new Texture2D(m_textureSize, m_textureSize, TextureFormat.RFloat, false, false);
        Texture2D texture = new Texture2D(m_textureSideSize, m_textureSideSize, TextureFormat.Alpha8, false, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;

        Texture2D texture2 = new Texture2D(m_textureSideSize, m_textureSideSize, TextureFormat.Alpha8, false, false);
        texture2.filterMode = FilterMode.Point;
        texture2.wrapMode = TextureWrapMode.Repeat;*/

        /*bool supportsTextureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R16); 
        if (supportsTextureFormat) {
            Debug.Log("");
        }*/

        /*texture.anisoLevel = 1;
        readPointsFile1Value(texture, texture2);
        pointRenderer = GetComponent<Renderer>();
        pointRenderer.material.mainTexture = texture;
        //pointRenderer.material.SetTexture("_MainTex2", texture2);
        pointRenderer.material.SetTexture("_ColorTex", colorTexture);*/

        m_kernel = computeShader.FindKernel("CSMain");

        pointRenderer = GetComponent<Renderer>();

        positionComputebuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.GPUMemory);
        positionComputebuffer.SetData(points);
        pointRenderer.material.SetBuffer ("_Positions", positionComputebuffer);

        velocityComputebuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.GPUMemory);
        velocityComputebuffer.SetData(velocities);
        pointRenderer.material.SetBuffer ("_Velocities", velocityComputebuffer);

        computeShader.SetBuffer(m_kernel, "_Positions", positionComputebuffer);
        computeShader.SetBuffer(m_kernel, "_Velocities", velocityComputebuffer);

        pointRenderer.material.SetInt("_PointsCount", m_pointsCount);
        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
        Vector4 trans = transform.position;
        pointRenderer.material.SetVector("trans", trans);
        pointRenderer.material.SetInt("_Magnitude", m_textureSideSizePower);
        pointRenderer.material.SetInt("_TextureSwitchFrameNumber", m_textureSwitchFrameNumber);   

        Debug.Log("Number of points: " + m_pointsCount);
    }
	
	// Update is called once per frame
	void Update () {
        //Debug.Log(Time.fixedTime);

        int t = ((int)(Time.fixedTime * m_frameSpeed)) % m_lastFrameIndex;

        //t = 29;

        //Debug.Log(count);

        //Debug.Log(t);
        pointRenderer.material.SetInt("_FrameTime", t);
        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);

        //Debug.Log("Support instancing: " + SystemInfo.supportsInstancing);

        //int a = pointRenderer.material.GetInt("_FrameTime");

        //Debug.Log(a);
        Dispatch();
    }

    private void OnRenderObject()
    {
        

        pointRenderer.material.SetPass(0);
        pointRenderer.material.SetMatrix("model", transform.localToWorldMatrix);
        //Graphics.DrawProcedural(MeshTopology.Points, 1, m_pointsCount);
        Graphics.DrawProcedural(MeshTopology.Points, m_pointsCount);  // index buffer.
    }

    /*void OnPostRender ()
    {
        Dispatch();
    }*/

    void OnDestroy() {
        positionComputebuffer.Release();
        velocityComputebuffer.Release();
    }
    private void Dispatch()
    {
        //constantBuffer.SetData(new[] { Time.time });
        
        //computeShader.SetBuffer(_kernel, "offsets", offsetBuffer);
        //computeShader.SetBuffer(_kernel, "output", outputBuffer);
 
        computeShader.Dispatch(m_kernel, m_dimensionWidth, m_dimensionHeight, m_dimensionDepth);
    }
}
