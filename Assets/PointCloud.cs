using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Text;
using System.IO;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System;
using System.Linq;

public class PointCloud : MonoBehaviour {
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

    private ComputeBuffer m_pointsBuffer, /*m_computeBufferIn,*/ m_computeBufferTemp;

    private List<ComputeBuffer> m_indexComputeBuffers;

    private float m_currentTime = 0;
    private int m_frameIndex = 0;

    public ComputeShader m_radixShader;
    private int m_kernel;

    private RadixSort radixSort;

    private ComputeShader m_myRadixSort;
    private int LocalPrefixSum;
    private int GlobalPrefixSum;
    private int RadixReorder;
    private int WarpScanTest;
    private int inputSize;
    private int m_threadGroupSize;

    public int m_FPSLabelOffsetX = 15;
    public int m_FPSLabelOffsetY = 250;

    private float m_updateFrequency = 1.0f;
    private string m_fpsText;
    private int m_currentFPS;
    private int m_framesSinceUpdate;
    private float m_accumulation;

    private const string UI_FONT_SIZE = "<size=30>";
    private const float UI_FPS_LABEL_SIZE_X = 200.0f;
    private const float UI_FPS_LABEL_SIZE_Y = 200.0f;

    private ComputeBuffer[] m_inOutBuffers;
    private int m_actualNumberOfThreadGroups;
                                                    
    private int numberOfRadixSortPasses = 4;
    private int m_bitsPerPass = 2;
    private int m_passLengthMultiplier;
    private int m_elemsPerThread = 4;

    private float m_maxDistance;
    private Vector3 m_pointCloudCenter;
    private List<Vector3> m_ppoints;

    private CommandBuffer m_cmd;

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
        for (int i = k - 1; i < m_lastFrameIndex; i++) {

            TextAsset ta = Resources.Load(m_valueDataPath + "/frame" + i + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            byte[] bytes = ta.bytes;

            int frameSize = bytes.Length;

            Buffer.BlockCopy(bytes, 0, vals, index * frameSize, frameSize);
            index++;

        }
        tex2.LoadRawTextureData(vals);
        tex2.Apply();

        pointRenderer = GetComponent<Renderer>();
        pointRenderer.material.SetTexture("_MainTex2", tex2);
        yield return 0;
    }

    private List<List<uint>> partitionIndexBuffer (uint[] indexBuffer, float[] points, PointCloudInfo pointCloudInfo, int numberOfPartitions) {
        List<List<uint>> tiles = new List<List<uint>>();
        for (int i = 0; i < numberOfPartitions; i++) {
            tiles.Add(new List<uint>());
        }

        int tilesPerSide = (int)Mathf.Sqrt(numberOfPartitions);

        //need to have x min max, and z min max to find the right tile index.

        foreach (uint indexAndColor in indexBuffer) {
            uint index = (indexAndColor >> 8) * 3;
            float x = points[index];
            //float y = points[index + 1];
            float z = points[index + 2];

            float scaledX = (x - pointCloudInfo.minX) / (pointCloudInfo.maxX - pointCloudInfo.minX);    // float value 0.0 - 1.0
            float scaledZ = (z - pointCloudInfo.minZ) / (pointCloudInfo.maxZ - pointCloudInfo.minZ);    // float value 0.0 - 1.0

            int tileX = Mathf.FloorToInt(scaledX * tilesPerSide);
            int tileZ = Mathf.FloorToInt(scaledZ * tilesPerSide);

            int tileIndex = tileX + tileZ * tilesPerSide;

            tiles[tileIndex].Add(indexAndColor);
        }

        //tiles.RemoveAll(tile => tile.Count == 0);

        return tiles;
    }

    void readIndicesAndValues(List<ComputeBuffer> tileBuffers, float[] points, PointCloudInfo pointCloudInfo)
    {
        //byte[] vals = new byte[m_textureSize];
        //for (int k = 0; k < m_lastFrameIndex; k++)
        int k = 2;
        {
            
            
            //TextAsset ta = Resources.Load("AtriumData/binaryDataFull/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            TextAsset ta = Resources.Load(m_valueDataPath + "/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            byte[] bytes = ta.bytes;

            //int bufferSize = bytes.Length / 4; //131072; //4096;//bytes.Length / 4;
            //while(bufferSize % 512 != 0) {
            //    bufferSize--;
            //}

            int bufferSize = bytes.Length / 4;//4096 * 4 * 4 * 2;

            uint[] zeroedBytes = new uint[bufferSize];

            Buffer.BlockCopy(bytes, 0, zeroedBytes, 0, /*bytes.Length*/ bufferSize*4);

            //try to cut the size to something like 4096

            List<List<uint>> tiles = partitionIndexBuffer(zeroedBytes, points, pointCloudInfo, 100);  

            //List<ComputeBuffer> tileBuffers = new List<ComputeBuffer>();
            foreach (var tile in tiles) {
                if (tile.Count > 0) {
                    ComputeBuffer buf = new ComputeBuffer(tile.Count, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);
                    buf.SetData(tile.ToArray());
                    tileBuffers.Add(buf);
                }
            }
            
            
            
            
            //ComputeBuffer indexComputeBuffer = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

            //indexComputeBuffer.SetData(/*zeroedBytes*//*bytes*/zeroedBytes);

            //computeBuffers.Add(indexComputeBuffer);

            
            
            //int frameSize = bytes.Length;
            //Buffer.BlockCopy(bytes, 0, vals, k * frameSize, frameSize);
        }
        
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


    private Vector3 getMeanVector3(float[] points) {
        Vector3 mean = new Vector3(0, 0, 0);
        for (int i = 0; i < points.Length; i+=3) {  
            Vector3 p = new Vector3(points[i], points[i + 1], points[i + 2]);
            mean += p;
        }
        mean /= (points.Length / 3);
        return mean; 
    }

    private Vector3 getMeanVector3(List<Vector3> points) {
        Vector3 mean = new Vector3(0, 0, 0);
        foreach (Vector3 p in points) {
            mean += p;
        }
        mean /= points.Count;
        return mean; 
    }

    private Vector3 getPointCloudCenter (float[] points) {
        return getMeanVector3(points);
    }

    private Vector3 getPointCloudCenter (List<Vector3> points) {
        return getMeanVector3(points);
    }

    private float getMaxDistance(List<Vector3> points, Vector3 center) {
        float maxDistance = 0;                      
        foreach (Vector3 p in points) {
            float distance = Vector3.Distance(center, p);  
            if (distance > maxDistance) {
                maxDistance = distance;
            }
        }
        return maxDistance + maxDistance*0.01f; //adding a little more to make sure we're not cutting anything off with inprecision.
    }

    struct PointCloudInfo {
        public float maxX;
        public float minX;
        public float maxZ;
        public float minZ;
        public float maxDistance;
    }

    private PointCloudInfo getMaxDistance(float[] points, Vector3 center) {
        PointCloudInfo info;
        info.maxDistance = 0;

        info.maxX = float.NegativeInfinity;
        info.minX = float.PositiveInfinity;
        info.maxZ = float.NegativeInfinity;
        info.minZ = float.PositiveInfinity;

        for (int i = 0; i < points.Length; i+=3) {  
            Vector3 p = new Vector3(points[i], points[i + 1], points[i + 2]);
            float distance = Vector3.Distance(center, p);
            if (distance > info.maxDistance) {
                info.maxDistance = distance;
            }
            if (p.x > info.maxX) {
                info.maxX = p.x;
            }
            else if (p.x < info.minX) {
                info.minX = p.x;
            }

            if (p.z > info.maxZ) {
                info.maxZ = p.z;
            }
            else if (p.z < info.minZ) {
                info.minZ = p.z;
            }
        }
        info.maxDistance += info.maxDistance * 0.01f;
        return info;
    }

    void Start () {
        m_textureSideSize = 1 << m_textureSideSizePower;
        m_textureSize = m_textureSideSize * m_textureSideSize;

        Screen.SetResolution(1920, 1080, true);

        //Set up mesh:
        float[] points = readPointsFile3Attribs();
        m_pointsCount = points.Length / 3;

         //Set up textures:
        Texture2D colorTexture = createColorLookupTexture();

        //We don't need more precision than the resolution of the colorTexture. 10 bits is sufficient for 1024 different color values.
        //That means we can pack 3 10bit integer values into a pixel 
        //Texture2D texture = new Texture2D(m_textureSize, m_textureSize, TextureFormat.RFloat, false, false);

        m_pointCloudCenter = getPointCloudCenter(points);
        PointCloudInfo pointCloudInfo = getMaxDistance(points, m_pointCloudCenter);
        m_maxDistance = pointCloudInfo.maxDistance;

        m_indexComputeBuffers = new List<ComputeBuffer>();
        readIndicesAndValues(m_indexComputeBuffers, points, pointCloudInfo);




        //texture.anisoLevel = 1;
        //readPointsFile1Value(texture, texture2);
        pointRenderer = GetComponent<Renderer>();
        //pointRenderer.material.mainTexture = texture;
        //pointRenderer.material.SetTexture("_MainTex2", texture2);
        pointRenderer.material.SetTexture("_ColorTex", colorTexture);


        /*List<int> indices = new List<int>();

        int numberOfParticles = 32768;//131072;//65536;

        m_pointsCount = numberOfParticles;

        for (int i = 0; i < numberOfParticles; i++) {
            indices.Add((i << 8) + (i*4 % 64));
        }

        m_ppoints = new List<Vector3>();

        for (int i = 0; i < numberOfParticles; i++) {
            m_ppoints.Add(new Vector3(0.0f - i*0.01f ,0.0f ,0.0f ));
        }   */                                                           
        
        m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
        m_pointsBuffer.SetData(points);
        //m_pointsBuffer.SetData(m_ppoints.ToArray());
        pointRenderer.material.SetBuffer("_Points", m_pointsBuffer);

        //m_indexComputeBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Default);
        //m_indexComputeBuffer.SetData(indices.ToArray());

        //pointRenderer.material.SetInt("_PointsCount", /*m_pointsCount*/pointPositions.Length);
        pointRenderer.material.SetInt("_PointsCount", m_pointsCount);
        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
        Vector4 trans = transform.position;
        pointRenderer.material.SetVector("trans", trans);
        pointRenderer.material.SetInt("_Magnitude", m_textureSideSizePower);
        pointRenderer.material.SetInt("_TextureSwitchFrameNumber", m_textureSwitchFrameNumber);

        /*
        // m_kernel = m_radixShader.FindKernel("CSMain");
        m_passLengthMultiplier = m_bitsPerPass * m_bitsPerPass;
        if (m_bitsPerPass == 4) {
            m_myRadixSort = (ComputeShader)Resources.Load("MyRadixSort/localSort", typeof(ComputeShader));
        }
        else if (m_bitsPerPass == 2) {
            if (m_elemsPerThread == 4) {
                m_myRadixSort = (ComputeShader)Resources.Load("MyRadixSort/localSort2bits4PerThread", typeof(ComputeShader));
            }
            else {
                m_myRadixSort = (ComputeShader)Resources.Load("MyRadixSort/localSort2bits", typeof(ComputeShader));
            }
        }
        else {                                                                                                       
            m_myRadixSort = (ComputeShader)Resources.Load("MyRadixSort/localSort1bit", typeof(ComputeShader));
        }
        LocalPrefixSum = m_myRadixSort.FindKernel("LocalPrefixSum");
        GlobalPrefixSum = m_myRadixSort.FindKernel("GlobalPrefixSum");
        RadixReorder = m_myRadixSort.FindKernel("RadixReorder");

        uint x, y, z;
        m_myRadixSort.GetKernelThreadGroupSizes(LocalPrefixSum, out x, out y, out z);
        m_threadGroupSize = (int)x;

        inputSize = m_indexComputeBuffers[m_frameIndex].count; //m_indexComputeBuffer.count;
        m_actualNumberOfThreadGroups = inputSize / m_threadGroupSize;

        uint[] bufOutRadix = new uint[m_actualNumberOfThreadGroups * 2 * m_passLengthMultiplier];
        uint[] bufOutPrefixSum = new uint[2 * m_passLengthMultiplier]; //the size represents the 16 possible values with 4 bits.
        */
        // m_inOutBuffers = new ComputeBuffer[2];
        // m_inOutBuffers[0] = m_indexComputeBuffers[m_frameIndex]; // m_indexComputeBuffer;
        /*m_inOutBuffers[1] = new ComputeBuffer(m_inOutBuffers[0].count, Marshal.SizeOf(typeof(uint))*2, ComputeBufferType.Default);
        
        ComputeBuffer computeBufferOut = new ComputeBuffer(m_actualNumberOfThreadGroups, Marshal.SizeOf(typeof(Vector2))*m_passLengthMultiplier, ComputeBufferType.Default);
        computeBufferOut.SetData(bufOutRadix);

        ComputeBuffer computeBufferDigitPrefixSum = new ComputeBuffer(1, Marshal.SizeOf(typeof(Vector2))*m_passLengthMultiplier, ComputeBufferType.Default);
        computeBufferDigitPrefixSum.SetData(bufOutPrefixSum);

        ComputeBuffer computeBufferGlobalPrefixSum = new ComputeBuffer(m_threadGroupSize, Marshal.SizeOf(typeof(Vector2))*m_passLengthMultiplier, ComputeBufferType.Default);

        ComputeBuffer depthsAndValueScans = new ComputeBuffer(m_inOutBuffers[0].count, Marshal.SizeOf(typeof(uint))*2, ComputeBufferType.Default);


        m_myRadixSort.SetFloat("depthIndices", Mathf.Pow(2.0f, (float)m_bitsPerPass*numberOfRadixSortPasses));

        m_myRadixSort.SetBuffer(LocalPrefixSum, "BucketsOut", computeBufferOut);
        m_myRadixSort.SetBuffer(LocalPrefixSum, "DepthValueScanOut", depthsAndValueScans);
        m_myRadixSort.SetBuffer(LocalPrefixSum, "_Points", m_pointsBuffer);

        m_myRadixSort.SetBuffer(GlobalPrefixSum, "GlobalDigitPrefixSumOut", computeBufferDigitPrefixSum);
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "BucketsIn", computeBufferOut);
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "GlobalPrefixSumOut", computeBufferGlobalPrefixSum);

        m_myRadixSort.SetBuffer(RadixReorder, "GlobalDigitPrefixSumIn", computeBufferDigitPrefixSum);
        m_myRadixSort.SetBuffer(RadixReorder, "DepthValueScanIn", depthsAndValueScans);
        m_myRadixSort.SetBuffer(RadixReorder, "GlobalPrefixSumIn", computeBufferGlobalPrefixSum);
        
        */
        //pointRenderer.material.SetBuffer("_IndicesValues", m_inOutBuffers[0]);
        /*

        uint[] bufOut = new uint[m_indexComputeBuffers[m_frameIndex].count];

        m_computeBufferTemp = new ComputeBuffer(bufOut.Length, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);
        m_computeBufferTemp.SetData(bufOut);

        Debug.Log("Number of points: " + m_pointsCount);   */

        m_cmd = new CommandBuffer();
        for (int i = 0; i < m_indexComputeBuffers.Count; i++) {
            m_cmd.SetGlobalBuffer("_IndicesValues", m_indexComputeBuffers[i]);
            m_cmd.DrawProcedural(pointRenderer.localToWorldMatrix, pointRenderer.material, 0, MeshTopology.Triangles, m_indexComputeBuffers[i].count * 6);
        }

    }
	
	// Update is called once per frame
	void Update () {
        m_currentTime += Time.deltaTime;
        ++m_framesSinceUpdate;
        m_accumulation += Time.timeScale / Time.deltaTime;
        if (m_currentTime >= m_updateFrequency)
        {
            m_currentFPS = (int)(m_accumulation / m_framesSinceUpdate);
            m_currentTime = 0.0f;
            m_framesSinceUpdate = 0;
            m_accumulation = 0.0f;
            m_fpsText = "FPS: " + m_currentFPS;
        }

        //Debug.Log(t);
        pointRenderer.material.SetInt("_FrameTime", m_frameIndex);
        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
    }

    private void OnGUI()
    {
        {
            Color oldColor = GUI.color;
            GUI.color = Color.white;

            GUI.Label(new Rect(m_FPSLabelOffsetX, m_FPSLabelOffsetY,
                               UI_FPS_LABEL_SIZE_X,
                               UI_FPS_LABEL_SIZE_Y), UI_FONT_SIZE + m_fpsText + "</size>");
            GUI.color = oldColor;
        }
    }

    private void OnRenderObject()
    {
        //GpuSort.BitonicSort32(m_indexComputeBuffers[m_frameIndex], m_computeBufferTemp, m_pointsBuffer, pointRenderer.localToWorldMatrix);


        /*m_myRadixSort.SetVector("camPos", Camera.main.transform.forward);     //camera view direction DOT point position == distance to camera.


        

        Matrix4x4 transMatrix = pointRenderer.localToWorldMatrix;
        m_myRadixSort.SetFloats("model", transMatrix[0], transMatrix[1], transMatrix[2], transMatrix[3],
                                  transMatrix[4], transMatrix[5], transMatrix[6], transMatrix[7],
                                  transMatrix[8], transMatrix[9], transMatrix[10], transMatrix[11],
                                  transMatrix[12], transMatrix[13], transMatrix[14], transMatrix[15]);

        Vector3 zero = -m_pointCloudCenter;
        Vector3 transZero = transMatrix.MultiplyPoint(zero);

        float globalScale = transform.lossyScale.x;
        float scaledMaxDistance = m_maxDistance * globalScale;

        m_myRadixSort.SetVector("objectWorldPos", transZero);
        m_myRadixSort.SetFloat("scaledMaxDistance", scaledMaxDistance);
        

        int outSwapIndex = 1;
        for (int i = 0; i < numberOfRadixSortPasses; i++) {
            int bitshift = m_bitsPerPass * i;
            m_myRadixSort.SetInt("bitshift", bitshift);
            int swapIndex0 = i % 2;
            outSwapIndex = (i + 1) % 2;

            m_myRadixSort.SetBuffer(LocalPrefixSum, "KeysIn", m_inOutBuffers[swapIndex0]);
            m_myRadixSort.SetBuffer(RadixReorder, "KeysIn", m_inOutBuffers[swapIndex0]);
            m_myRadixSort.SetBuffer(RadixReorder, "KeysOut", m_inOutBuffers[outSwapIndex]);

            m_myRadixSort.Dispatch(LocalPrefixSum, m_actualNumberOfThreadGroups / m_elemsPerThread, 1, 1);
            m_myRadixSort.Dispatch(GlobalPrefixSum, 1, 1, 1);
            m_myRadixSort.Dispatch(RadixReorder, m_actualNumberOfThreadGroups / m_elemsPerThread, 1, 1);
        }             */                                     

        pointRenderer.material.SetPass(0);
        pointRenderer.material.SetMatrix("model", pointRenderer.localToWorldMatrix);

        //GL.MultMatrix(pointRenderer.localToWorldMatrix);
        
        //Debug.Log(m_indexComputeBuffers[m_frameIndex].count);

        Graphics.ExecuteCommandBuffer(m_cmd);

         //   Graphics.DrawProcedural(MeshTopology.Triangles, /*m_pointsCount*6*/buffer.count*6);  // index buffer.         
            //Graphics.DrawProcedural(MeshTopology.Points, /*m_pointsCount*6*/m_indexComputeBuffers[m_frameIndex].count);  // index buffer. 
        //}
        

                                                                             
                

        //Graphics.DrawProcedural(MeshTopology.Triangles, /*m_pointsCount*6*//*m_indexComputeBuffers[m_frameIndex].count*6*/m_indexComputeBuffer.count*6 );  // index buffer.
        //Graphics.DrawProcedural(MeshTopology.Points, /*m_pointsCount*6*//*m_indexComputeBuffers[m_frameIndex].count*/m_indexComputeBuffer.count);  // index buffer.

        //Maybe this rendering stuff is supposed to be attached to the camera?
    }

    void OnDestroy() {
        m_pointsBuffer.Release();

        /*foreach (ComputeBuffer cb in m_indexComputeBuffers)
        {
            cb.Release();
        }  */
       
    }
}
