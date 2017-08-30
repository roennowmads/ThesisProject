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

    struct PartInt : IComparable {
        public uint key;

        int IComparable.CompareTo(object obj) {
            PartInt p1 = this;
            PartInt p2 = (PartInt)obj;
            if (p1.key << 8 > p2.key << 8)
                return 1;
            if (p1.key << 8 < p2.key << 8)
                return -1;
            else
                return 0;
        }
    };

    /*struct Particle : IComparable {
        public uint key;
        public uint depth;

        public Particle (uint key, uint depth) {
            this.key = key;
            this.depth = depth;
        }

        int IComparable.CompareTo(object obj) {
            Particle p1 = this;
            Particle p2 = (Particle)obj;
            if (p1.depth > p2.depth)
                return 1;
            if (p1.depth < p2.depth)
                return -1;
            else
                return 0;
        }
    };*/

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

    void readIndicesAndValues(List<ComputeBuffer> computeBuffers)
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

            int bufferSize = 4096 * 4 * 4 * 2;

            uint[] zeroedBytes = new uint[bufferSize];

            Buffer.BlockCopy(bytes, 0, zeroedBytes, 0, /*bytes.Length*/ bufferSize*4);


            //uint i = BitConverter.ToUInt32(bytes, 64);
            //uint o = i >> 8;
            //uint v = i & 0xFF;

            /*uint[] newVals = new uint[bufferSize];

            for (int i = 0; i < zeroedBytes.Length; i++) {
                newVals[i] = zeroedBytes[i]; //>> 8;
            }*/

            //uint a = zeroedBytes[10] >> 8;
            //uint b = zeroedBytes[10] / 256;






            //try to cut the size to something like 4096
            
            ComputeBuffer indexComputeBuffer = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

            indexComputeBuffer.SetData(/*zeroedBytes*//*bytes*/zeroedBytes);

            computeBuffers.Add(indexComputeBuffer);

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

    void Start () {
        m_textureSideSize = 1 << m_textureSideSizePower;
        m_textureSize = m_textureSideSize * m_textureSideSize;

        //Set up mesh:
        float[] points = readPointsFile3Attribs();
        m_pointsCount = points.Length / 3;

         //Set up textures:
        Texture2D colorTexture = createColorLookupTexture();

        //We don't need more precision than the resolution of the colorTexture. 10 bits is sufficient for 1024 different color values.
        //That means we can pack 3 10bit integer values into a pixel 
        //Texture2D texture = new Texture2D(m_textureSize, m_textureSize, TextureFormat.RFloat, false, false);

        m_indexComputeBuffers = new List<ComputeBuffer>();

        readIndicesAndValues(m_indexComputeBuffers);

        //texture.anisoLevel = 1;
        //readPointsFile1Value(texture, texture2);
        pointRenderer = GetComponent<Renderer>();
        //pointRenderer.material.mainTexture = texture;
        //pointRenderer.material.SetTexture("_MainTex2", texture2);
        pointRenderer.material.SetTexture("_ColorTex", colorTexture);

        //float[] pointDepths = { 5.1f, 10.1f, 2.5f, 1.4f, 0.3f, 20.5f, 30.7f, 45.1f, 12.6f, 63.9f, 48.8f, 3.6f, 6.3f, 32.8f, 87.9f, 39.4f};

        //Vector3[] pointPositions = { new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f),
        //    new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f),
        //    new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f),
        //    new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)};

       /* Vector3[] pointPositions = new Vector3[512];

        for (int i = 0; i < pointPositions.Length; i++) {
            pointPositions[i] = new Vector3(0, 0, 0);
            pointPositions[i].x = pointPositions.Length*0.01f - i*0.01f;
        }

        m_pointsBuffer = new ComputeBuffer (pointPositions.Length, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
        m_pointsBuffer.SetData(pointPositions);
        pointRenderer.material.SetBuffer ("_Points", m_pointsBuffer);*/


        
        m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
        m_pointsBuffer.SetData(points);
        pointRenderer.material.SetBuffer("_Points", m_pointsBuffer);

        //pointRenderer.material.SetInt("_PointsCount", /*m_pointsCount*/pointPositions.Length);
        pointRenderer.material.SetInt("_PointsCount", m_pointsCount);
        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
        Vector4 trans = transform.position;
        pointRenderer.material.SetVector("trans", trans);
        pointRenderer.material.SetInt("_Magnitude", m_textureSideSizePower);
        pointRenderer.material.SetInt("_TextureSwitchFrameNumber", m_textureSwitchFrameNumber);

        m_kernel = m_radixShader.FindKernel("CSMain");

        m_myRadixSort = (ComputeShader)Resources.Load("MyRadixSort/localSort", typeof(ComputeShader));
        LocalPrefixSum = m_myRadixSort.FindKernel("LocalPrefixSum");
        GlobalPrefixSum = m_myRadixSort.FindKernel("GlobalPrefixSum");
        RadixReorder = m_myRadixSort.FindKernel("RadixReorder");

        uint x, y, z;
        m_myRadixSort.GetKernelThreadGroupSizes(LocalPrefixSum, out x, out y, out z);
        m_threadGroupSize = (int)x;

        int threadGroupsNeeded = 16 /** 16*/;
        inputSize = m_indexComputeBuffers[m_frameIndex].count;//m_threadGroupSize * threadGroupsNeeded;
        m_actualNumberOfThreadGroups = inputSize / m_threadGroupSize;

        /*uint[] bufInRadix = new uint[inputSize];

        //uint[] bufInRadix = { 5, 10, 2, 1, 0, 20, 30, 45, 12, 63, 48, 3, 6, 32, 87, 39 };

        for (uint i = 0; i < bufInRadix.Length; i++)
        {
            bufInRadix[i] = i % 16;
            bufInRadix[i] = bufInRadix[i] == 5 ? 6 : bufInRadix[i];
            if (i % 3 == 0) {
                bufInRadix[i] = 10;
            }
        }*/

        int Min = 0;
        int Max = 2047;
        System.Random randNum = new System.Random();

        /*PartInt[] bufInRadix = new PartInt[inputSize];
        uint[] bufInRadix = Enumerable
            .Repeat(0, inputSize)
            .Select(i => (uint)randNum.Next(Min, Max) << 8)
            .ToArray();*/

        /*for (int i = 0; i < inputSize; i++) {
            bufInRadix[i].key = (uint)i << 8;
        }*/


        /*Vector3[] myPoints = new Vector3[inputSize];
        for (int i = 0; i < myPoints.Length; i++) {
            myPoints[i] = new Vector3(1000 - i, 1000 - i, 1000 - i);
        }

        ComputeBuffer myPointsBuffer = new ComputeBuffer(inputSize, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
        myPointsBuffer.SetData(myPoints);*/

        uint[] bufOutRadix = new uint[m_actualNumberOfThreadGroups * 16];

        uint[] bufOutPrefixSum = new uint[16]; //the size represents the 16 possible values with 4 bits.

        PartInt[] bufOutFinal = new PartInt[inputSize];
        for (int i = 0; i < bufOutFinal.Length; i++) {
            bufOutFinal[i].key = 9999u;
        }

        m_inOutBuffers = new ComputeBuffer[2];
        m_inOutBuffers[0] = m_indexComputeBuffers[m_frameIndex];//new ComputeBuffer(bufInRadix.Length, Marshal.SizeOf(typeof(PartInt)), ComputeBufferType.Default);
        //m_inOutBuffers[0].SetData(bufInRadix);
        m_inOutBuffers[1] = new ComputeBuffer(m_inOutBuffers[0].count, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);
        m_inOutBuffers[1].SetData(bufOutFinal);

        ComputeBuffer m_computeBufferValueScans = new ComputeBuffer(inputSize, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

        ComputeBuffer m_computeBufferOut = new ComputeBuffer(bufOutRadix.Length / 16, Marshal.SizeOf(typeof(Matrix4x4)), ComputeBufferType.Default);
        m_computeBufferOut.SetData(bufOutRadix);

        ComputeBuffer computeBufferDigitPrefixSum = new ComputeBuffer(16, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);
        computeBufferDigitPrefixSum.SetData(bufOutPrefixSum);

        ComputeBuffer m_computeBufferGlobalPrefixSum = new ComputeBuffer(m_threadGroupSize, Marshal.SizeOf(typeof(Matrix4x4)), ComputeBufferType.Default);

        ComputeBuffer computeBufferDepthVals = new ComputeBuffer(m_inOutBuffers[0].count, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);

        m_myRadixSort.SetBuffer(LocalPrefixSum, "BucketsOut", m_computeBufferOut);
        m_myRadixSort.SetBuffer(LocalPrefixSum, "ValueScans", m_computeBufferValueScans);
        m_myRadixSort.SetBuffer(LocalPrefixSum, "DepthVals", computeBufferDepthVals);
        
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "GlobalDigitPrefixSumOut", computeBufferDigitPrefixSum);
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "BucketsOut", m_computeBufferOut);
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "DepthVals", computeBufferDepthVals);
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "GlobalPrefixSumOut", m_computeBufferGlobalPrefixSum);

        m_myRadixSort.SetBuffer(RadixReorder, "GlobalDigitPrefixSumIn", computeBufferDigitPrefixSum);
        m_myRadixSort.SetBuffer(RadixReorder, "ValueScansIn", m_computeBufferValueScans);
        m_myRadixSort.SetBuffer(RadixReorder, "DepthVals", computeBufferDepthVals);
        m_myRadixSort.SetBuffer(RadixReorder, "GlobalPrefixSumIn", m_computeBufferGlobalPrefixSum);

        m_myRadixSort.SetBuffer(LocalPrefixSum, "_Points", m_pointsBuffer);
        m_myRadixSort.SetBuffer(GlobalPrefixSum, "_Points", m_pointsBuffer);
        m_myRadixSort.SetBuffer(RadixReorder, "_Points", m_pointsBuffer);

        pointRenderer.material.SetBuffer("_IndicesValues", m_inOutBuffers[0]);


        /*m_computeBufferOutFinal.GetData(bufOutFinal);


        uint[] bufOutValueScans = new uint[inputSize*16];
        m_computeBufferValueScans.GetData(bufOutValueScans);

        List<uint> prefixSumOfAValueLocal = new List<uint>();
        for (int i = 0; i < bufOutValueScans.Length; i++) {
            if (i % 16 == 0) {
                prefixSumOfAValueLocal.Add(bufOutValueScans[i + 1]); // + 1 means the partial sums of 1's. 
            }   
        }*/

        //Array.Sort<PartInt>(bufInRadix);

        /* m_computeBufferOutPrefixSum.GetData(bufOutPrefixSum);
         m_computeBufferIn.GetData(bufOutFinal);*/

        /*bool theSame = true;
        for (int i = 0; i < inputSize; i++) {
	        if (bufOutFinal[i].key != bufInRadix[i].key) {
		        theSame = false;
		        break;
	        }
        }*/

        /*Vector3 viewDir = new Vector3(0.0f, 0.0f, 1.0f);
        radixSort = new RadixSort(256, 128);

        radixSort.sort(m_computeBufferIn, m_computeBufferOut, -viewDir, -2.0f, 2.0f);
        m_computeBufferIn.GetData(bufInRadix);
        m_computeBufferOut.GetData(bufOutRadix);

         m_radixShader.SetBuffer(m_kernel, "Input", m_computeBufferIn);
         m_radixShader.SetBuffer(m_kernel, "Result", m_computeBufferOut);
         m_radixShader.Dispatch(m_kernel, 2, 1, 1);
         m_radixShader.SetBuffer(m_kernel, "Input", m_computeBufferOut);
         m_radixShader.SetBuffer(m_kernel, "Result", m_computeBufferIn);
         m_radixShader.Dispatch(m_kernel, 2, 1, 1);

         m_computeBufferIn.GetData(bufOutRadix);*/

        /*uint[] bufIn = new uint[pointPositions.Length];
        //uint[] bufOut = new uint[16];

        for (uint i = 0; i < bufIn.Length; i++)
        {
            bufIn[i] = i;
        }

        //float[] bufIn = { 5, 10, 2, 1, 0, 20, 30, 45, 12, 63, 48, 3, 6, 32, 87, 39};
        uint[] bufOut = new uint[pointPositions.Length]; //{ 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 };*/


        uint[] bufOut = new uint[/*m_pointsCount*//*262144*/m_indexComputeBuffers[m_frameIndex].count];

        //m_kernel = m_computeShader.FindKernel("main");
        

        //m_computeBufferIn = new ComputeBuffer(m_pointsCount, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);
        //m_computeBufferIn.SetData(bufIn);
        //m_computeShader.SetBuffer(m_kernel, "Input", computeBufferIn);

        m_computeBufferTemp = new ComputeBuffer(bufOut.Length, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);
        m_computeBufferTemp.SetData(bufOut);
        //m_computeShader.SetBuffer(m_kernel, "Output", computeBufferOut);

        //m_computeShader.Dispatch(m_kernel, 1, 1, 1);

        //computeBufferOut.GetData(bufOut);

        //pointRenderer.material.SetBuffer("_IndicesValues", m_computeBufferIn);

        /*uint[] inArray = new uint[512 * 4];

        for (int i = 0; i < inArray.Length; i++)
            inArray[i] = (uint)(inArray.Length - 1 - i);
        
        inBuffer = new ComputeBuffer(inArray.Length, 4);
        tempBuffer = new ComputeBuffer(inArray.Length, 4);*/

        //inBuffer.SetData(bufIn);

        float timeBefore = Time.realtimeSinceStartup;        

        //GpuSort.BitonicSort32(m_indexComputeBuffers[2], m_computeBufferTemp, m_pointsBuffer, trans, transform.localToWorldMatrix);
        m_indexComputeBuffers[m_frameIndex].GetData(bufOut);

        float timeAfter = Time.realtimeSinceStartup; //be aware, the dispatch is probably async? it seems to be just the time it takes to go through the for loops and set the buffers.
        print("Time in milliseconds: " + (timeAfter - timeBefore) * 1000.0f);

        print(bufOut[10]);

        //One down-side to storing and loading a texture is that we are storing all channels, as well as any unused parts of the texture.
        //TextAsset ta = Resources.Load("fireAtriumTex", typeof(TextAsset)) as TextAsset;
        //byte[] texBytes = CompressionHelper.DecompressBytes(ta.bytes);
        //texture.LoadRawTextureData(texBytes);

        //Vector4[] pointsArr = points.ToArray();
        //var byteArray = new byte[pointsArr.Length * 4 * 4];
        //Buffer.BlockCopy(pointsArr, 0, byteArray, 0, byteArray.Length);

        //CompressionHelper.CompressMemToFile(byteArray, "fireAtriumPoints.lzf");       

        //CompressionHelper.CompressMemToFile(texture.GetRawTextureData(), "fireAtriumTex.lzf");       

        Debug.Log("Number of points: " + m_pointsCount);

    }
	
	// Update is called once per frame
	void Update () {
        //Debug.Log(Time.fixedTime);

        //m_computeShader.Dispatch(m_kernel, 1, 1, 1);

        //m_frameIndex = 2;//((int)(Time.fixedTime * m_frameSpeed)) % m_lastFrameIndex;

        //pointRenderer.material.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);

        //t = 29;

        //Debug.Log(count);

        /*int t = 0;

        m_currentTime += Time.deltaTime;
        if (m_currentTime > m_frameSpeed) {
            m_frameIndex = m_frameIndex + 1 % m_lastFrameIndex;
            m_currentTime = 0;
        }*/

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


        //GpuSort.BitonicSort32(m_indexComputeBuffers[m_frameIndex], m_computeBufferTemp, m_pointsBuffer, pointRenderer.localToWorldMatrix);

        //uint[] bufOut = new uint[/*m_pointsCount*//*262144*/m_indexComputeBuffers[m_frameIndex].count];
        //m_indexComputeBuffers[m_frameIndex].GetData(bufOut);
        //print(bufOut[10]);

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


        m_myRadixSort.SetVector("camPos", -Camera.main.transform.forward);     //camera view direction DOT point position == distance to camera.

        Matrix4x4 transMatrix = pointRenderer.localToWorldMatrix;
        m_myRadixSort.SetFloats("model", transMatrix[0], transMatrix[1], transMatrix[2], transMatrix[3],
                                  transMatrix[4], transMatrix[5], transMatrix[6], transMatrix[7],
                                  transMatrix[8], transMatrix[9], transMatrix[10], transMatrix[11],
                                  transMatrix[12], transMatrix[13], transMatrix[14], transMatrix[15]);

        int outSwapIndex = 1;
        int numberOfPasses = 8;
        for (int i = 0; i < numberOfPasses; i++) {
            int bitshift = 4 * i;
            m_myRadixSort.SetInt("bitshift", bitshift);
            int swapIndex0 = i % 2;
            outSwapIndex = (i + 1) % 2;

            m_myRadixSort.SetBuffer(LocalPrefixSum, "KeysIn", m_inOutBuffers[swapIndex0]);
            m_myRadixSort.SetBuffer(RadixReorder, "KeysIn", m_inOutBuffers[swapIndex0]);
            m_myRadixSort.SetBuffer(RadixReorder, "KeysOut", m_inOutBuffers[outSwapIndex]);

            m_myRadixSort.Dispatch(LocalPrefixSum, m_actualNumberOfThreadGroups, 1, 1);
            m_myRadixSort.Dispatch(GlobalPrefixSum, 1, 1, 1);
            m_myRadixSort.Dispatch(RadixReorder, m_actualNumberOfThreadGroups, 1, 1);
        }

        pointRenderer.material.SetPass(0);
        pointRenderer.material.SetMatrix("model", pointRenderer.localToWorldMatrix);

        //Debug.Log(m_indexComputeBuffers[m_frameIndex].count);

        //Graphics.DrawProcedural(MeshTopology.Points, 1, m_pointsCount);
        //Graphics.DrawProcedural(MeshTopology.Triangles, /*m_pointsCount*6*/m_indexComputeBuffers[m_frameIndex].count*6);  // index buffer.
        Graphics.DrawProcedural(MeshTopology.Triangles, /*m_pointsCount*6*/(m_indexComputeBuffers[m_frameIndex].count)*6 );  // index buffer.

        //Maybe this rendering stuff is supposed to be attached to the camera?
    }

    void OnDestroy() {
        m_pointsBuffer.Release();

        foreach (ComputeBuffer cb in m_indexComputeBuffers)
        {
            cb.Release();
        }
       
    }
}
