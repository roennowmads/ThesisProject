using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Text;
using System.IO;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Reflection;

public class PointCloud : MonoBehaviour {

    public TextAsset pointData;
    public int m_numberOfFrames = 25;
    public float m_updateFrequency = 1.0f;
    
    private int m_pointsCount = 61440;
    private List<int> times;
    private Renderer pointRenderer;

    //public string m_FPSText;
    private int m_currentFPS;
    private int m_framesSinceUpdate;
    private float m_accumulation;
    private float m_currentTime;

    private int m_textureSize = 1024 * 8;

    private ComputeBuffer computebuffer;

    /*[System.Serializable]
    public class Vec4 {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vec4(float x, float y, float z, float w) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }*/


    /*public sealed class VersionDeserializationBinder : SerializationBinder {
        public override Type BindToType(string assemblyName, string typeName) {
            if (!string.IsNullOrEmpty(assemblyName) && !string.IsNullOrEmpty(typeName)) {
                Type typeToDeserialize = null;

                assemblyName = Assembly.GetExecutingAssembly().FullName;

                // The following line of code returns the type. 
                typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, assemblyName));

                return typeToDeserialize;
            }

            return null;
        }
    }*/

    /*public static void Save(List<Vec4> points, string filePath) 
    {
        FileStream stream = File.Open(filePath, FileMode.Create);
        try
        {
            BinaryFormatter bformatter = new BinaryFormatter();
            //bformatter.Binder = new VersionDeserializationBinder();
            bformatter.Serialize(stream, points);
        }
        catch (SerializationException e) {
            Console.WriteLine("Failed to serialize. Reason: " + e.Message);
            throw;
        }
        finally {
            stream.Close();
        }
    }

    public static void Load(List<Vec4> points, string filePath)
    {
        FileStream stream = File.Open(filePath, FileMode.Open);
        try
        {
            BinaryFormatter bformatter = new BinaryFormatter();
            //bformatter.Binder = new VersionDeserializationBinder();
            points = (List<Vec4>)bformatter.Deserialize(stream);
        }
        catch (SerializationException e) //many more exception might happen, check documentation
        {
            Console.WriteLine("Failed to deserialize. Reason: " + e.Message);
            throw;
        }
        finally {
            stream.Close();
        }
    }*/


    void readPointsFile1Value(int fileIndex, int offset, Texture2D tex) {
        //TextAsset pointValues = Resources.Load<UnityEngine.Object>("AtriumData/fireAtrium0." + index) as TextAsset;
        //StreamReader reader = new StreamReader(new MemoryStream(pointValues.bytes));
        //List<Color> colors = new List<Color>();

        //CompressionHelper.CompressFile("fireAtrium0.0.bytes");
        string path = "Assets/Resources/AtriumData/";

        //CompressionHelper.CompressFile(path + "fireAtrium0." + fileIndex + ".bytes", "fireAtrium0." + fileIndex +".lzf");
        byte[] bytes = CompressionHelper.DecompressFileToMem(path + "fireAtrium0." + fileIndex +".lzf");

        //TextAsset ta = Resources.Load("AtriumData/fireAtrium0." + index, typeof(TextAsset)) as TextAsset;
        //byte[] bytes = ta.bytes;
        
        float[] vals = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, vals, 0, bytes.Length);

        //foreach (float val in vals) {
        for (int i = 0; i< vals.Length; i++) {
            float maxMagnitude = 1000.0f;/*5.73f * 0.5f;*/
            float value = 1.0f - (vals[i] / maxMagnitude);

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

            color.a = value;

            int texIndex = i + offset;
            int x = texIndex % tex.width;
            int y = texIndex / tex.height;
            
            tex.SetPixel(x, y, color); 

            //alternatives: (necessary if I want to store only one component per pixel)
            //tex.LoadRawTextureData()
            //tex.SetPixels(x, y, width, height, colors.ToArray()); 
            //pixels are stored in rectangle blocks... maybe it would actually be better for caching anyway? problem is a frame's colors would need to fit in a rectangle.
        }
    } 

    List<Vector4> readPointsFile3Attribs()
    {
        StreamReader reader = new StreamReader(new MemoryStream(pointData.bytes));
        List<Vector4> points = new List<Vector4>();

        char[] delimiterChars = { ',' };

        string line;
        using (reader) {
            // While there's lines left in the text file, do this:
            do {
                line = reader.ReadLine();
                //Debug.Log(line);

                if (line != null) {
                    Vector4 point = new Vector4();
                    string[] parts = line.Split(delimiterChars);
                    point.x = float.Parse(parts[0]);
                    point.y = float.Parse(parts[1]);
                    point.z = float.Parse(parts[2]);

                    points.Add(point);
                }
            }
            while (line != null);
            // Done reading, close the reader and return true to broadcast success    
            reader.Close();
        }
        return points;
    }
    void Start () {
        //Set up mesh:
        List<Vector4> points = readPointsFile3Attribs();

        /*List<Vec4> ps = new List<Vec4>();
        foreach (Vector4 p in points) {
            ps.Add(new Vec4(p.x, p.y, p.z, p.w));
        }

        List<Vec4> ps2 = new List<Vec4>();
        foreach (Vec4 p in ps) {
            ps2.Add(new Vec4(0.0f, 0.0f, 0.0f, 0.0f));
        }*/

        //Save(ps, "here.bin");
        //Load(ps2, "here.bin");

        m_pointsCount = points.Count;

        //Set up texture:
        Texture2D texture = new Texture2D(m_textureSize, m_textureSize);
        pointRenderer = GetComponent<Renderer>();
        pointRenderer.material.mainTexture = texture;

        computebuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.GPUMemory);
        computebuffer.SetData(points.ToArray());
        pointRenderer.material.SetBuffer ("points", computebuffer);

        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
        Vector4 trans = transform.position;
        pointRenderer.material.SetVector("trans", trans);


        times = new List<int>();
        int offset = 0;

        //parallelize this:
        for (int k = 0; k < m_numberOfFrames; k++) {
            readPointsFile1Value(k, offset, texture);
            times.Add(offset);
            offset += m_pointsCount;
        }
        texture.Apply();

        m_currentFPS = 0;
        m_framesSinceUpdate = 0;
        m_currentTime = 0.0f;
    }
	
	// Update is called once per frame
	void Update () {
        //Debug.Log(Time.fixedTime);

        int t = ((int)(Time.fixedTime * 5.0)) % times.Count;

        int count = times[t];

        //Debug.Log(t);
        pointRenderer.material.SetInt("_FrameTime", count);

        //Debug.Log("Support instancing: " + SystemInfo.supportsInstancing);

        int a = pointRenderer.material.GetInt("_FrameTime");

        Debug.Log(a);
       
        m_currentTime += Time.deltaTime;
        ++m_framesSinceUpdate;
        m_accumulation += Time.timeScale / Time.deltaTime;
        if (m_currentTime >= m_updateFrequency)
        {
            m_currentFPS = (int)(m_accumulation / m_framesSinceUpdate);
            m_currentTime = 0.0f;
            m_framesSinceUpdate = 0;
            m_accumulation = 0.0f;
            //Debug.Log("FPS: " + m_currentFPS);
        }

        //Debug.Log("FPS: " + m_currentFPS);

        //Graphics.DrawProcedural(MeshTopology.Points, 100, 100000);   


        //Graphics.DrawProcedural(MeshTopology.Points, 100, 1023);

        //Graphics.DrawProcedural(MeshTopology.Points, 100, 100000);

        //Graphics.DrawMeshInstanced()

        //pointRenderer.material.SetPass(0);
        //Graphics.DrawProcedural(MeshTopology.Points, points4.Count, 1);
       // Graphics.DrawProcedural(MeshTopology.Triangles, 6, /*points4.Count*/10000);

        //Graphics.DrawMeshInstanced(m_mesh, 0, pointRenderer.material, matrices, 1023, matProb);

        //Graphics.DrawMeshInstanced(m_mesh, this.transform.localToWorldMatrix, pointRenderer.material, 0);

        //Graphics.DrawMesh(m_mesh, this.transform.localToWorldMatrix, pointRenderer.material, 0);
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
