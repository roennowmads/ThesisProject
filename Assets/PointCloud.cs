using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Text;
using System.IO;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System;

public class PointCloud : MonoBehaviour {

    public TextAsset pointData;
    public int m_numberOfFrames = 25;
    public float m_FPSUpdateFrequency = 1.0f;
    public float m_frameSpeed = 5.0f;
    
    private int m_pointsCount = 61440;
    private int[] times;
    private Renderer pointRenderer;

    //public string m_FPSText;
    private int m_currentFPS;
    private int m_framesSinceUpdate;
    private float m_accumulation;
    private float m_currentTime;

    private int m_textureSize = 1024 * 4;
    private int m_lookupTextureSize = 256; //Since the values in the value texture are only 0-255, it doesn't make sense to have more values here.

    private ComputeBuffer computebuffer;

    Texture2D createColorLookupTexture() {
        int numberOfValues = m_lookupTextureSize;

        Texture2D lookupTexture = new Texture2D(m_lookupTextureSize, 1, TextureFormat.RGB24, false, false);
        lookupTexture.filterMode = FilterMode.Point;
        lookupTexture.anisoLevel = 1;

        for (int i = 0; i < numberOfValues; i++) {
            //float maxMagnitude = 1000.0f;/*5.73f * 0.5f;*/
            float textureIndex = i;


            //0 - 1023 --> 1.0 - 0.0
            float value = 1.0f - (textureIndex / numberOfValues);

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

            //color.a = 0.0f;//value;
            
            lookupTexture.SetPixel(i, 0, color); 

            //alternatives: (necessary if I want to store only one component per pixel)
            //tex.LoadRawTextureData()
            //tex.SetPixels(x, y, width, height, colors.ToArray()); 
            //pixels are stored in rectangle blocks... maybe it would actually be better for caching anyway? problem is a frame's colors would need to fit in a rectangle.
        }

        lookupTexture.Apply();

        return lookupTexture;
    }

    void readPointsFile1Value(Texture2D tex) {
        //CompressionHelper.CompressFile(path + "fireAtrium0." + fileIndex + ".bytes", "fireAtrium0." + fileIndex +".lzf");
        //byte[] bytes = CompressionHelper.DecompressFileToMem(path + "fireAtrium0." + fileIndex +".lzf");
        //float[] vals = new float[bytes.Length / 4];
        //Buffer.BlockCopy(bytes, 0, vals, 0, bytes.Length);

        byte[] vals = new byte[m_textureSize * m_textureSize];//new byte[m_textureSize * m_textureSize * 4];

        times = new int[m_numberOfFrames];
        int offset = 0;
        int frameSize = 0;
        for (int k = 0; k < m_numberOfFrames; k++) {
            TextAsset ta = Resources.Load("AtriumData/fireAtrium0." + k, typeof(TextAsset)) as TextAsset;
            byte[] bytes = CompressionHelper.DecompressBytes(ta.bytes);


            float[] gottenFloats = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, gottenFloats, 0, bytes.Length);

            byte[] processedBytes = new byte[gottenFloats.Length];

            float maxMagnitude = 1000.0f;
            for (int i = 0; i < processedBytes.Length; i++) {
                byte val = (byte)((gottenFloats[i] / maxMagnitude) * 255.0f);
                processedBytes[i] = val;
            }

            frameSize = gottenFloats.Length;//bytes.Length;
            Buffer.BlockCopy(processedBytes, 0, vals, k*frameSize, frameSize);

            times[k] = offset;
            offset += m_pointsCount;
        }
        //Fill in the rest of the texture will 0 values:
        int restSize = vals.Length - frameSize*m_numberOfFrames;
        byte[] restBytes = new byte[restSize];
        for (int i = 0; i < restSize; i++) {
            //initialize the values:
            restBytes[i] = 0;
        }
        Buffer.BlockCopy(restBytes, 0, vals, frameSize*m_numberOfFrames, restSize);

        tex.LoadRawTextureData(vals);
        tex.Apply();


        /*byte[] gottenBytes = tex.GetRawTextureData();
        float[] gottenFloats = new float[gottenBytes.Length / 4];
        Buffer.BlockCopy(gottenBytes, 0, gottenFloats, 0, gottenBytes.Length);

        for (int i = 0; i < gottenFloats.Length; i++) {
            if (gottenFloats[i] != 1000.0f && gottenFloats[i] != 0f) {
                int x = 0;
            }
        }*/

    } 

    List<Vector3> readPointsFile3Attribs()
    {
        List<Vector3> points = new List<Vector3>();

        char[] delimiterChars = { ',' };

        string line;
        using (StreamReader reader = new StreamReader(new MemoryStream(pointData.bytes))) {
            // While there's lines left in the text file, do this:
            do {
                line = reader.ReadLine();
                //Debug.Log(line);

                if (line != null) {
                    Vector3 point = new Vector3();
                    string[] parts = line.Split(delimiterChars);
                    point.x = float.Parse(parts[0]);
                    point.y = float.Parse(parts[1]);
                    point.z = float.Parse(parts[2]);

                    points.Add(point);
                }
            }
            while (line != null);
        }
        return points;
    }
    void Start () {
        //Set up mesh:
        List<Vector3> points = readPointsFile3Attribs();
        m_pointsCount = points.Count;

         //Set up textures:
        Texture2D colorTexture = createColorLookupTexture();
        
        //We don't need more precision than the resolution of the colorTexture. 10 bits is sufficient for 1024 different color values.
        //That means we can pack 3 10bit integer values into a pixel 
        //Texture2D texture = new Texture2D(m_textureSize, m_textureSize, TextureFormat.RFloat, false, false);
        Texture2D texture = new Texture2D(m_textureSize, m_textureSize, TextureFormat.Alpha8, false, false);
        texture.filterMode = FilterMode.Point;

        /*bool supportsTextureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R16); 
        if (supportsTextureFormat) {
            Debug.Log("");
        }*/

        texture.anisoLevel = 1;
        readPointsFile1Value(texture);
        pointRenderer = GetComponent<Renderer>();
        pointRenderer.material.mainTexture = texture;
        pointRenderer.material.SetTexture("_ColorTex", colorTexture);

        computebuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.GPUMemory);
        computebuffer.SetData(points.ToArray());
        pointRenderer.material.SetBuffer ("points", computebuffer);

        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
        Vector4 trans = transform.position;
        pointRenderer.material.SetVector("trans", trans);


        //One down-side to storing and loading a texture is that we are storing all channels, as well as any unused parts of the texture.
        //TextAsset ta = Resources.Load("fireAtriumTex", typeof(TextAsset)) as TextAsset;
        //byte[] texBytes = CompressionHelper.DecompressBytes(ta.bytes);
        //texture.LoadRawTextureData(texBytes);

        //Vector4[] pointsArr = points.ToArray();
        //var byteArray = new byte[pointsArr.Length * 4 * 4];
        //Buffer.BlockCopy(pointsArr, 0, byteArray, 0, byteArray.Length);

        //CompressionHelper.CompressMemToFile(byteArray, "fireAtriumPoints.lzf");       

        //CompressionHelper.CompressMemToFile(texture.GetRawTextureData(), "fireAtriumTex.lzf");       

        m_currentFPS = 0;
        m_framesSinceUpdate = 0;
        m_currentTime = 0.0f;
    }
	
	// Update is called once per frame
	void Update () {
        //Debug.Log(Time.fixedTime);

        int t = ((int)(Time.fixedTime * m_frameSpeed)) % times.Length;

        int count = times[t];

        //Debug.Log(t);
        pointRenderer.material.SetInt("_FrameTime", count);

        //Debug.Log("Support instancing: " + SystemInfo.supportsInstancing);

        int a = pointRenderer.material.GetInt("_FrameTime");

        Debug.Log(a);
       
        m_currentTime += Time.deltaTime;
        ++m_framesSinceUpdate;
        m_accumulation += Time.timeScale / Time.deltaTime;
        if (m_currentTime >= m_FPSUpdateFrequency)
        {
            m_currentFPS = (int)(m_accumulation / m_framesSinceUpdate);
            m_currentTime = 0.0f;
            m_framesSinceUpdate = 0;
            m_accumulation = 0.0f;
            //Debug.Log("FPS: " + m_currentFPS);
        }

        Debug.Log("FPS: " + m_currentFPS);
    }

    private void OnRenderObject()
    {
        pointRenderer.material.SetPass(0);
        pointRenderer.material.SetMatrix("model", transform.localToWorldMatrix);
        Graphics.DrawProcedural(MeshTopology.Triangles, 6, m_pointsCount);
    }

    void OnDestroy() {
        computebuffer.Release();
    }
}
