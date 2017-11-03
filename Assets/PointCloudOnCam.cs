using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;
using System;
using UnityEngine.Rendering;

public class PointCloudOnCam : MonoBehaviour {

private List<ComputeBuffer> m_indexComputeBuffers;
    public string m_valueDataPath = "OilRigDataIndexed";
    private int m_pointsCount = 61440;
    private int m_lookupTextureSize = 256;
    private ComputeBuffer m_pointsBuffer;
    private int m_frameIndex = 0;

    public RenderTexture m_renderTex;
    public RenderTexture m_opaqueTex;
    public RenderTexture m_accumTex;
    public RenderTexture m_revealageTex;
    public RenderTexture m_resultTex;

    private CommandBuffer m_commandBuf, m_commandBuf2;

    public Shader shader, m_accumShader, m_revealageShader, m_blendShader, m_textureShader;
    private Material m_material, m_accumMaterial, m_revealageMaterial, m_blendMaterial, m_textureMaterial;

    public Texture particleTexture;

    private GameObject m_pointCloudObj;

    private static System.Random rng = new System.Random();

    private RenderBuffer[] m_renderBuffers;

    private Color m_clear0s, m_clear1s;

    public ParticleSystem psys;
    private ParticleSystem.Particle[] cloud;

    private bool m_directRender = true;

    public static float m_pointSizeScale;
    public static float m_pointSizeScaleIndependent;

    private float m_timeSinceUpdate, m_updateInterval;

    private bool m_fixedParticleCountTests = false;
    private bool m_fixedParticleSizeTests = false;

    private int m_particelSizeTestsXSize;
    private ComputeBuffer m_indexComputeBuffer;

    public static void Shuffle(uint[] list) {
        int n = list.Length;
        while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            uint value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    void readIndicesAndValues(List<ComputeBuffer> computeBuffers)
    {
        int k = 2;
        {
            //TextAsset ta = Resources.Load("AtriumData/binaryDataFull/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            TextAsset ta = Resources.Load(m_valueDataPath + "/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            byte[] bytes = ta.bytes;

            int bufferSize = 4096 * 4 * 4 * 2 /*- 50000*/;

            uint[] zeroedBytes = new uint[bufferSize];

            Buffer.BlockCopy(bytes, 0/*50000*4*/, zeroedBytes, 0,  bufferSize*4);

            //Shuffle(zeroedBytes);

            ComputeBuffer indexComputeBuffer = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

            indexComputeBuffer.SetData(zeroedBytes);

            computeBuffers.Add(indexComputeBuffer);
        }
        
    } 

    float[] readPointsFile3Attribs()
    {
        TextAsset pointData = Resources.Load(m_valueDataPath + "/frame00.0.pos", typeof(TextAsset)) as TextAsset;
        byte[] bytes = pointData.bytes;
        float[] points = new float[(bytes.Length / 4)];
        Buffer.BlockCopy(bytes, 0, points, 0, bytes.Length);
        return points;
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

    private void Start() {
        float[] points = readPointsFile3Attribs();
        m_pointsCount = points.Length / 3;

        //Screen.SetResolution(950, 720, true);
        Screen.SetResolution(1920, 1080, true);

        //float pointSizeScale = .0625f;//1.0f;//1.0f;//0.125f;   //for one point: 12.0f

        m_pointSizeScale = 4.0f;//0.0625f;
        m_pointSizeScaleIndependent = 0.175f;
        m_timeSinceUpdate = 0.0f;
        m_updateInterval = 12.0f;

        /*int width = 64;
        int height = 32;//128;//128;
        int depth = 64;*/
        
        /*int width = 20;
        int height = 20;//20;//128;//128;
        int depth = 12;*/

        /*int width = 80;//20;
        int height = 20;//20;//128;//128;
        int depth = 48;//12;*/

        int width = (int)(12 / m_pointSizeScale);  //decrease decrease to get different particle count results
        int height = 100;//20;//20;//128;//128;
        int depth = (int)(4 / m_pointSizeScale);

        int numberOfPoints = width * height * depth;

        List<Vector3> ppoints = new List<Vector3>();
        List<uint> indices = new List<uint>();
        m_indexComputeBuffer = new ComputeBuffer(numberOfPoints, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

        //Vector3[] ppoints = new Vector3[numberOfPoints];

        //int sideLength = (int)Math.Sqrt(numberOfPoints);

        float xDelta = 1.0f / (0.015f / m_pointSizeScale);
        float xDeltaHalf = xDelta * 0.5f;
        float yDelta = 1.0f / (0.015f / m_pointSizeScale);
        float yDeltaHalf = yDelta * 0.5f;

        uint index = 0;
        for (int x = 0; x < width; x++) {
            for (int z = 0; z < depth; z++) {
                for (int y = 0; y < height; y++) {
                    Vector3 point = new Vector3(x * xDelta, y / 0.22f, z * yDelta);
                    ppoints.Add(point);

                    uint value = index;
                    value = (value << 8) + 127;

                    indices.Add(value);
                    index++;
                }
            }
        }
        m_pointsCount = ppoints.Count;

        m_indexComputeBuffer.SetData(indices.ToArray());


        m_indexComputeBuffers = new List<ComputeBuffer>();

        readIndicesAndValues(m_indexComputeBuffers);

        Texture2D colorTexture = createColorLookupTexture();

        if (m_fixedParticleCountTests) {
            Camera.main.transform.position = (new Vector3(-0.7f, 5.3f - m_pointSizeScale * 0.25f, 21.7f)); //5.05
        }
        else if (m_fixedParticleSizeTests) {
            m_particelSizeTestsXSize = 9;
            Camera.main.transform.position = (new Vector3(-0.7f, 5.45f - m_pointSizeScale * 0.25f, 21.7f)); //5.05
        }
        else {
            Camera.main.transform.position = (new Vector3(-1.5f + m_pointSizeScale * 0.25f, 6.0f - m_pointSizeScale * 0.3f, 21.7f));
        }

        //ComputeBuffer coords = new ComputeBuffer(6, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default);
       /* Texture2D coordsTex = new Texture2D(6, 1, TextureFormat.R8, false, false);
        byte[] quadCoordsAndTexCoords = {
                0, 0,
                1, 1,
                1, 0,

                0, 1,
                1, 1,
                0, 0
            };

        byte[] bitCoords = {
            0, 3, 2, 1, 3, 0
        };*/

        //coordsTex.LoadRawTextureData(bitCoords);

        /*Color[] quadCoordsAndTexCoords = {
                new Color(0, 0, 0, 0),
                new Color(1, 1, 1, 1),
                new Color(1, 0, 1, 0),

                new Color(0, 1, 0, 1),
                new Color(1, 1, 1, 1),
                new Color(0, 0, 0, 0)
            };*/

        //coordsTex.SetPixels(quadCoordsAndTexCoords);

        //coords.SetData(quadCoordsAndTexCoords);

        //byte[] quadCoordsAndTexCoordsBytes = new byte[quadCoordsAndTexCoords.Length*4];
        //Buffer.BlockCopy(quadCoordsAndTexCoords, 0, quadCoordsAndTexCoordsBytes, 0, quadCoordsAndTexCoordsBytes.Length);
        //coordsTex.LoadRawTextureData(quadCoordsAndTexCoordsBytes);
        //coordsTex.Apply();


        m_pointCloudObj = GameObject.Find("placeholder");

        m_material = new Material(shader);
        m_accumMaterial = new Material(m_accumShader);
        m_revealageMaterial = new Material(m_revealageShader);
        m_blendMaterial = new Material(m_blendShader);
        m_textureMaterial = new Material(m_textureShader);

        m_material.SetTexture("_AlbedoTex", particleTexture);
        m_accumMaterial.SetTexture("_AlbedoTex", particleTexture);
        m_revealageMaterial.SetTexture("_AlbedoTex", particleTexture);

        m_material.SetTexture("_ColorTex", colorTexture);
        m_accumMaterial.SetTexture("_ColorTex", colorTexture);
        m_revealageMaterial.SetTexture("_ColorTex", colorTexture);

        //m_material.SetTexture("_CoordsTex", coordsTex);
        //m_material.SetBuffer("_CoordsTex", coords);

        m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
        m_pointsBuffer.SetData(/*points*/ ppoints.ToArray());
        m_material.SetBuffer("_Points", m_pointsBuffer);
        m_accumMaterial.SetBuffer("_Points", m_pointsBuffer);
        m_revealageMaterial.SetBuffer("_Points", m_pointsBuffer);

        float aspect = Camera.main.GetComponent<Camera>().aspect;
        m_material.SetFloat("aspect", aspect);
        m_accumMaterial.SetFloat("aspect", aspect);
        m_revealageMaterial.SetFloat("aspect", aspect);


        //vertex coordinates found here: http://mathworld.wolfram.com/Pentagon.html
        Vector4 pentagonParams = new Vector4();
        float c1 = 0.25f * (Mathf.Sqrt(5.0f) - 1.0f);
        float c2 = 0.25f * (Mathf.Sqrt(5.0f) + 1.0f);

        float s1 = 0.25f * (Mathf.Sqrt(10.0f + 2.0f * Mathf.Sqrt(5.0f))) * 0.9f / aspect;
        float s2 = 0.25f * (Mathf.Sqrt(10.0f - 2.0f * Mathf.Sqrt(5.0f))) * 0.9f / aspect;

        pentagonParams.x = c1;
        pentagonParams.y = c2 * 0.87f;//0.89f;
        pentagonParams.z = s1 * 0.97f;
        pentagonParams.w = s2 * 0.95f;

        m_material.SetVector("pentagonParams", pentagonParams);

        m_material.SetFloat("pointSizeScale", m_pointSizeScale);
        m_material.SetFloat("pointSizeScaleIndependent", m_pointSizeScaleIndependent);

        /*m_material.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);
        m_accumMaterial.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);
        m_revealageMaterial.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);*/

        m_material.SetBuffer("_IndicesValues", m_indexComputeBuffer);
        m_accumMaterial.SetBuffer("_IndicesValues", m_indexComputeBuffer);
        m_revealageMaterial.SetBuffer("_IndicesValues", m_indexComputeBuffer);

        m_blendMaterial.SetTexture("_AccumTex", m_accumTex);
        m_blendMaterial.SetTexture("_RevealageTex", m_revealageTex);

        //m_accumTex = RenderTexture.GetTemporary(1024, 600, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //m_revealageTex = RenderTexture.GetTemporary(1024, 600, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //m_tempTex.Create();


        Camera.main.targetTexture = m_opaqueTex;

        //m_renderTex = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //m_renderTex.Create();

        m_renderBuffers = new RenderBuffer[]{ m_accumTex.colorBuffer, m_revealageTex.colorBuffer };

        m_clear0s = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        m_clear1s = new Color(1.0f, 0.0f, 0.0f, 0.0f);


        cloud = new ParticleSystem.Particle[m_pointsCount];

        for (int ii = 0; ii < ppoints.Count; ++ii) {
            cloud[ii].position = ppoints[ii];
            //cloud[ii].color = colors[ii];
            //cloud[ii].size = 0.1f;
        }

        psys.SetParticles(cloud, cloud.Length);

        /*Shader.EnableKeyword("_WEIGHTED_ON");
        Shader.DisableKeyword("_WEIGHTED0");
        Shader.DisableKeyword("_WEIGHTED1");
        Shader.EnableKeyword("_WEIGHTED2");*/
    }

    private void Update() {
        m_timeSinceUpdate += Time.deltaTime;

        if (m_fixedParticleCountTests) {
             if (m_timeSinceUpdate > m_updateInterval) {
                //m_pointSizeScaleIndependent *= 0.5f;
                m_pointSizeScaleIndependent -= 0.05f;
                if (m_pointSizeScaleIndependent < 0.05f) {
                    m_pointSizeScaleIndependent = 0.25f;
                }
                m_timeSinceUpdate = 0.0f;

                Camera.main.transform.position = (new Vector3(-0.7f, 5.3f - m_pointSizeScale * 0.25f, 21.7f));

                /*int width = (int)(48 / m_pointSizeScale);
                int height = 30;//20;//20;//128;//128;
                int depth = (int)(32 / m_pointSizeScale);

                List<Vector3> ppoints = new List<Vector3>();

                float xDelta = 1.0f / (0.015f / m_pointSizeScale);
                float xDeltaHalf = xDelta * 0.5f;
                float yDelta = 1.0f / (0.015f / m_pointSizeScale);
                float yDeltaHalf = yDelta * 0.5f;
        
                for (int x = 0; x < width; x++) {
                    for (int z = 0; z < depth; z++) {
                        for (int y = 0; y < height; y++) {
                            Vector3 point = new Vector3(x * xDelta, y / 0.22f, z * yDelta);
                            ppoints.Add(point);
                        }
                    }
                }
                m_pointsCount = ppoints.Count;
                m_pointsBuffer.Dispose();
                m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
                m_pointsBuffer.SetData(ppoints.ToArray());
                m_material.SetBuffer("_Points", m_pointsBuffer);*/
                //m_accumMaterial.SetBuffer("_Points", m_pointsBuffer);
                //m_revealageMaterial.SetBuffer("_Points", m_pointsBuffer);

                m_material.SetFloat("pointSizeScale", m_pointSizeScale);
                m_material.SetFloat("pointSizeScaleIndependent", m_pointSizeScaleIndependent);
            }
        }
        else if (m_fixedParticleSizeTests) {
             if (m_timeSinceUpdate > m_updateInterval) {
                m_particelSizeTestsXSize--;
                if (m_particelSizeTestsXSize <= 0) {
                    m_particelSizeTestsXSize = 9;
                }
                m_timeSinceUpdate = 0.0f;

                //Camera.main.transform.position = (new Vector3(-0.7f, 5.3f - m_pointSizeScale * 0.25f, 21.7f));

                int width = (int)(m_particelSizeTestsXSize / m_pointSizeScale);
                int height = 50;//20;//20;//128;//128;
                int depth = (int)(6.25f / m_pointSizeScale);

                int numberOfPoints = width * height * depth;

                List<Vector3> ppoints = new List<Vector3>();
                List<uint> indices = new List<uint>();
                m_indexComputeBuffer.Dispose();
                m_indexComputeBuffer = new ComputeBuffer(numberOfPoints, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

                float xDelta = 1.0f / (0.015f / m_pointSizeScale);
                float xDeltaHalf = xDelta * 0.5f;
                float yDelta = 1.0f / (0.015f / m_pointSizeScale);
                float yDeltaHalf = yDelta * 0.5f;

                uint index = 0;
                for (int x = 0; x < width; x++) {
                    for (int z = 0; z < depth; z++) {
                        for (int y = 0; y < height; y++) {
                            Vector3 point = new Vector3(x * xDelta, y / 0.22f, z * yDelta);
                            ppoints.Add(point);

                            uint value = index;
                            value = (value << 8) + 127;

                            indices.Add(value);
                            index++;
                        }
                    }
                }
                m_pointsCount = ppoints.Count;

                m_indexComputeBuffer.SetData(indices.ToArray());
                
                m_pointsBuffer.Dispose();
                m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
                m_pointsBuffer.SetData(ppoints.ToArray());

                m_material.SetBuffer("_IndicesValues", m_indexComputeBuffer);
                m_material.SetBuffer("_Points", m_pointsBuffer);

                m_material.SetFloat("pointSizeScale", m_pointSizeScale);
                m_material.SetFloat("pointSizeScaleIndependent", m_pointSizeScaleIndependent);
            }
        }
        else {
            //Camera.main.transform.position = (new Vector3(-1.5f + m_pointSizeScale * 0.25f, 6.0f - m_pointSizeScale * 0.3f, 21.7f));

            if (m_timeSinceUpdate > m_updateInterval) {
                if (m_pointSizeScale == 0.125f) {
                    m_pointSizeScale = 0.1f;
                }
                else {
                     m_pointSizeScale *= 0.5f;
                }
               
                if (m_pointSizeScale < 0.1f) {
                    m_pointSizeScale = 4.0f;
                }
                m_timeSinceUpdate = 0.0f;

                Camera.main.transform.position = (new Vector3(-1.5f + m_pointSizeScale * 0.25f, 6.0f - m_pointSizeScale * 0.3f, 21.7f));

                int width = (int)(12 / m_pointSizeScale);
                int height = 100;//20;//20;//128;//128;
                int depth = (int)(4 / m_pointSizeScale);

                int numberOfPoints = width * height * depth;

                List<Vector3> ppoints = new List<Vector3>();
                List<uint> indices = new List<uint>();
                m_indexComputeBuffer.Dispose();
                m_indexComputeBuffer = new ComputeBuffer(numberOfPoints, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

                float xDelta = 1.0f / (0.015f / m_pointSizeScale);
                float xDeltaHalf = xDelta * 0.5f;
                float yDelta = 1.0f / (0.015f / m_pointSizeScale);
                float yDeltaHalf = yDelta * 0.5f;
        
                uint index = 0;
                for (int x = 0; x < width; x++) {
                    for (int z = 0; z < depth; z++) {
                        for (int y = 0; y < height; y++) {
                            Vector3 point = new Vector3(x * xDelta, y / 0.22f, z * yDelta);
                            ppoints.Add(point);
                            
                            uint value = index;
                            value = (value << 8) + 127;

                            indices.Add(value);
                            index++;
                        }
                    }
                }
                m_pointsCount = ppoints.Count;

                m_indexComputeBuffer.SetData(indices.ToArray());

                m_pointsBuffer.Dispose();
                m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
                m_pointsBuffer.SetData(ppoints.ToArray());
                m_material.SetBuffer("_IndicesValues", m_indexComputeBuffer);
                m_material.SetBuffer("_Points", m_pointsBuffer);

                m_material.SetFloat("pointSizeScale", m_pointSizeScale);
                m_material.SetFloat("pointSizeScaleIndependent", m_pointSizeScaleIndependent);
            }        
        }
    }


    private void OnPreRender() {
        //Make sure all opaque geometry (everything not rendered with drawProcedural) is rendered into opaqueTex.
        if (m_directRender) {
            Graphics.SetRenderTarget(null);
            Camera.main.targetTexture = null;
        }
        else {
            Graphics.SetRenderTarget(m_opaqueTex);
            Camera.main.targetTexture = m_opaqueTex;
        }
        GL.Clear(true, true, m_clear0s);
    }

    private void OnPostRender() {
        Camera.main.targetTexture = null;
    }

    private void renderToScreenSideBySide () {
        Graphics.SetRenderTarget(m_renderTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, m_clear0s);
        m_material.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount * 6);

        //Clear the colorbuffers:
        Graphics.SetRenderTarget(m_accumTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, m_clear0s);
        Graphics.SetRenderTarget(m_revealageTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, m_clear1s);

        Graphics.SetRenderTarget(/*m_accumTex.colorBuffer*/m_renderBuffers, m_opaqueTex.depthBuffer);
        //GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
        m_accumMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount * 6);

        /*Graphics.SetRenderTarget(m_revealageTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, new Color(1.0f, 1.0f, 1.0f, 1.0f));
        GL.MultMatrix(m_pointCloudObj.transform.localToWorldMatrix);
        m_revealageMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, (m_indexComputeBuffers[m_frameIndex].count) * 6);*/

        Graphics.SetRenderTarget(null);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

        Graphics.DrawTexture(   // be aware that this call seems to fuck the particles up, if the texture is shown on a plane it looks different.
            new Rect(0, 0, Screen.width / 2, Screen.height),
            m_opaqueTex, m_blendMaterial); // m_revealageTex

        //Graphics.DrawTexture(   // be aware that this call seems to fuck the particles up, if the texture is shown on a plane it looks different.
        //    new Rect(0, 0, Screen.width / 2, Screen.height),
        //    m_resultTex); // m_revealageTex

        Graphics.DrawTexture(   // be aware that this call seems to fuck the particles up, if the texture is shown on a plane it looks different.
            new Rect(Screen.width / 2, 0, Screen.width / 2, Screen.height),
            m_renderTex, m_textureMaterial); // m_revealageTex

        GL.PopMatrix();
    }

    private void renderToTextures() {
        Graphics.SetRenderTarget(m_renderTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, m_clear0s);
        m_material.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount * 6);

        //Clear the colorbuffers:
        Graphics.SetRenderTarget(m_accumTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, m_clear0s);
        Graphics.SetRenderTarget(m_revealageTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, m_clear1s);

        Graphics.SetRenderTarget(/*m_accumTex.colorBuffer*/m_renderBuffers, m_opaqueTex.depthBuffer);
        m_accumMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount * 6);

        /*Graphics.SetRenderTarget(m_revealageTex.colorBuffer, m_opaqueTex.depthBuffer);
        GL.Clear(false, true, new Color(1.0f, 1.0f, 1.0f, 1.0f));
        GL.MultMatrix(m_pointCloudObj.transform.localToWorldMatrix);
        m_revealageMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, (m_indexComputeBuffers[m_frameIndex].count) * 6);*/

        Graphics.Blit(m_opaqueTex, m_resultTex, m_blendMaterial);
    }

    private void renderDirect () {
        Graphics.SetRenderTarget(null);
        m_material.SetPass(0);
        //Graphics.DrawProcedural(MeshTopology.Points, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount /** 6*/);

        //Graphics.DrawProcedural(MeshTopology.Triangles, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount * 6);
        Graphics.DrawProcedural(MeshTopology.Points, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount);
    }

    private void OnRenderObject() {
        if (Camera.current == Camera.main) {
            m_accumMaterial.SetVector("camPos", Camera.main.transform.position);
            GL.MultMatrix(m_pointCloudObj.transform.localToWorldMatrix);

            if (m_directRender) {
                renderDirect();
            }
            //renderToTextures();
            //renderToScreenSideBySide();

        }
    }
}
