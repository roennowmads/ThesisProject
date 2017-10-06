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

        Screen.SetResolution(950, 720, true);


        /*int width = 64;
        int height = 32;//128;//128;
        int depth = 64;*/

        int width = 1;
        int height = 1;//128;//128;
        int depth = 1;

        int numberOfPoints = width * height * depth;

        Vector3[] ppoints = new Vector3[numberOfPoints];

        //int sideLength = (int)Math.Sqrt(numberOfPoints);

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                for (int z = 0; z < depth; z++) {
                    int index = x + y * width + z * height * depth;

                    //ppoints[index] = new Vector3(j / 2.0f - 65.0f, 115.0f, i / 2.0f - 82.0f);
                    //ppoints[index] = new Vector3(j / 10.0f - 65.0f, 115.0f, i / 10.0f - 82.0f);

                    //ppoints[index] = new Vector3(x / 0.22f - 0.0f, y / 0.22f, z / 0.22f - 0.0f);
                    ppoints[index] = new Vector3(x / 0.10f - 0.0f, y / 0.22f, z / 0.10f - 0.0f);
                }
            }
        }
        m_pointsCount = ppoints.Length;



        m_indexComputeBuffers = new List<ComputeBuffer>();

        readIndicesAndValues(m_indexComputeBuffers);

        Texture2D colorTexture = createColorLookupTexture();

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
        m_pointsBuffer.SetData(/*points*/ ppoints);
        m_material.SetBuffer("_Points", m_pointsBuffer);
        m_accumMaterial.SetBuffer("_Points", m_pointsBuffer);
        m_revealageMaterial.SetBuffer("_Points", m_pointsBuffer);

        float aspect = Camera.main.GetComponent<Camera>().aspect;
        m_material.SetFloat("aspect", aspect);
        m_accumMaterial.SetFloat("aspect", aspect);
        m_revealageMaterial.SetFloat("aspect", aspect);


        Vector4 pentagonParams = new Vector4();
        float c1 = 0.25f * (Mathf.Sqrt(5.0f) - 1.0f);
        float c2 = 0.25f * (Mathf.Sqrt(5.0f) + 1.0f);

        float s1 = 0.25f * (Mathf.Sqrt(10.0f + 2.0f * Mathf.Sqrt(5.0f))) * 0.9f / aspect;
        float s2 = 0.25f * (Mathf.Sqrt(10.0f - 2.0f * Mathf.Sqrt(5.0f))) * 0.9f / aspect;

        pentagonParams.x = c1;
        pentagonParams.y = c2 * 0.89f;
        pentagonParams.z = s1 * 0.97f;
        pentagonParams.w = s2 * 0.95f;

        m_material.SetVector("pentagonParams", pentagonParams);

        m_material.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);
        m_accumMaterial.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);
        m_revealageMaterial.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);

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

        for (int ii = 0; ii < ppoints.Length; ++ii) {
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

    //void OnRenderImage(RenderTexture src, RenderTexture dest) {        
    //    Graphics.Blit(src, m_renderTex);
    //    Graphics.Blit(src, dest);
    //}

    private void OnPreRender() {
        //Make sure all opaque geometry (everything not rendered with drawProcedural) is rendered into opaqueTex.
        Camera.main.targetTexture = m_opaqueTex;
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
        Graphics.DrawProcedural(MeshTopology.Points, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount /** 6*/);
        //Graphics.DrawProcedural(MeshTopology.Triangles, /*(m_indexComputeBuffers[m_frameIndex].count)*/m_pointsCount * 6);
    }

    private void OnRenderObject() {
        if (Camera.current == Camera.main) {
            m_accumMaterial.SetVector("camPos", Camera.main.transform.position);
            GL.MultMatrix(m_pointCloudObj.transform.localToWorldMatrix);

            renderDirect();
            //renderToTextures();
            //renderToScreenSideBySide();

        }
    }
}
