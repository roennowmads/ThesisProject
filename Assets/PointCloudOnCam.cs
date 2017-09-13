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
    private Renderer pointRenderer;
    private ComputeBuffer m_pointsBuffer;
    private int m_frameIndex = 0;

    public RenderTexture m_renderTex;
    private RenderTexture m_accumTex, m_revealageTex;

    private CommandBuffer m_commandBuf;
    private GameObject pointCloudObj;

    void readIndicesAndValues(List<ComputeBuffer> computeBuffers)
    {
        int k = 2;
        {
            //TextAsset ta = Resources.Load("AtriumData/binaryDataFull/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            TextAsset ta = Resources.Load(m_valueDataPath + "/frame" + k + "0.0", typeof(TextAsset)) as TextAsset; //LoadAsync
            byte[] bytes = ta.bytes;

            int bufferSize = 4096 * 4 * 4 * 2;

            uint[] zeroedBytes = new uint[bufferSize];

            Buffer.BlockCopy(bytes, 0, zeroedBytes, 0,  bufferSize*4);
            
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

        m_indexComputeBuffers = new List<ComputeBuffer>();

        readIndicesAndValues(m_indexComputeBuffers);

        Texture2D colorTexture = createColorLookupTexture();

        pointCloudObj = GameObject.Find("placeholder");

        pointRenderer = pointCloudObj.GetComponent<Renderer>();
        pointRenderer.material.SetTexture("_ColorTex", colorTexture);

        m_pointsBuffer = new ComputeBuffer (m_pointsCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Default);
        m_pointsBuffer.SetData(points);
        pointRenderer.material.SetBuffer("_Points", m_pointsBuffer);
        
        float aspect = Camera.main.GetComponent<Camera>().aspect;
        pointRenderer.material.SetFloat("aspect", aspect);
        Vector4 trans = pointCloudObj.transform.position;
        pointRenderer.material.SetVector("trans", trans);

        pointRenderer.material.SetBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);

        m_accumTex = RenderTexture.GetTemporary(1024, 600, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        m_revealageTex = RenderTexture.GetTemporary(1024, 600, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //m_tempTex.Create();

        //m_renderTex = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //m_renderTex.Create();

        //int tempBuf = 0;
        //m_commandBuf = new CommandBuffer();

        //m_commandBuf.GetTemporaryRT(tempBuf, -1, -1, 24, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

        //m_commandBuf.SetGlobalBuffer("_IndicesValues", m_indexComputeBuffers[m_frameIndex]);
        //m_commandBuf.SetGlobalVector("trans", trans);
        //m_commandBuf.SetGlobalFloat("aspect", aspect);
        //m_commandBuf.SetGlobalBuffer("_Points", m_pointsBuffer);
        //m_commandBuf.SetGlobalTexture("_ColorTex", colorTexture);
        //m_commandBuf.SetRenderTarget(tempBuf);
        //m_commandBuf.ClearRenderTarget(true, true, new Color(0.0f, 0.5f, 0.0f));
        //m_commandBuf.Blit(Texture2D.blackTexture, m_renderTex);

        //m_commandBuf.SetRenderTarget(m_renderTex);
        //m_commandBuf.ClearRenderTarget(true, true, new Color(0.0f, 0.5f, 0.0f)); //should use the camera's depth, or something like it?

        //m_commandBuf.SetRenderTarget( BuiltinRenderTextureType.CameraTarget);
        //m_commandBuf.DrawProcedural(/*Matrix4x4.identity*/pointRenderer.localToWorldMatrix, pointRenderer.material, 0, MeshTopology.Triangles, (m_indexComputeBuffers[m_frameIndex].count) * 6);
        //m_commandBuf.Blit(BuiltinRenderTextureType.CameraTarget, m_renderTex);  //it could be that the plane that we see in unity gets rendered before the particles and therefore they do not show up, but then why is the blit executed?

        //m_commandBuf.Blit(tempBuf, m_renderTex);

        //m_commandBuf.ReleaseTemporaryRT(tempBuf);

        //Camera cam = Camera.main;
        //if (!cam)
        //	return;
        //cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, m_commandBuf); //AfterImageEffects //BeforeImageEffectsOpaque //AfterForwardOpaque

    }

    //void OnRenderImage(RenderTexture src, RenderTexture dest) {        
    //    Graphics.Blit(src, m_renderTex);
    //    Graphics.Blit(src, dest);
    //}

    private void OnRenderObject() {
        //Graphics.ExecuteCommandBuffer(m_commandBuf);

        //pointRenderer.sharedMaterial.SetInt("_FrameTime", m_frameIndex);

        RenderBuffer originalColorBuffer = Graphics.activeColorBuffer;
        RenderBuffer originalDepthBuffer = Graphics.activeDepthBuffer;

        if (Camera.current == Camera.main) {
            //Graphics.SetRenderTarget(m_tempTex.colorBuffer, Graphics.activeDepthBuffer /*m_tempTex.depthBuffer*/);
            //Graphics.SetRenderTarget(m_accumTex);
            Graphics.SetRenderTarget(m_accumTex.colorBuffer, originalDepthBuffer);
            //Graphics.SetRenderTarget(m_renderTex);
            GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));  //only clear color, we bind the depth from the opaque geometry render as depth render target
            pointRenderer.sharedMaterial.SetPass(0);
            pointRenderer.sharedMaterial.SetMatrix("model", pointRenderer.localToWorldMatrix);
            Graphics.DrawProcedural(MeshTopology.Triangles, (m_indexComputeBuffers[m_frameIndex].count)*6 );  // index buffer.

            //this step i believe could be done in one pass with multiple render targets:
            Graphics.SetRenderTarget(m_revealageTex.colorBuffer, originalDepthBuffer);
            GL.Clear(false, true, new Color(1.0f, 1.0f, 1.0f, 1.0f));

            //(draw transparent geometry using the revealage shader) (need to change material or something, probably easier just to do this in a commandbuffer)
            Graphics.DrawProcedural(MeshTopology.Triangles, (m_indexComputeBuffers[m_frameIndex].count)*6,); // (draw transparent geometry using the revealage shader)
            //Graphics.Draw 

            //Graphics.DrawTexture (this is probably a good candidate for the last step of rendering it to screen)


            Graphics.SetRenderTarget(originalColorBuffer, originalDepthBuffer);
            Graphics.Blit(m_accumTex, m_renderTex);
        }

         //m_commandBuf.SetGlobalMatrix("model", pointRenderer.localToWorldMatrix);
        //m_commandBuf.GetTemporaryRT (normalsID, -1, -1);
        //m_commandBuf.Blit (BuiltinRenderTextureType.GBuffer2, normalsID);
        //m_commandBuf.SetRenderTarget(BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.CameraTarget);

        //m_commandBuf.DrawProcedural(pointRenderer.localToWorldMatrix, pointRenderer.material, 0, MeshTopology.Triangles, (m_indexComputeBuffers[m_frameIndex].count) * 6);
        
       //m_commandBuf.SetGlobalMatrix("model", pointRenderer.localToWorldMatrix);
       
    }



}
