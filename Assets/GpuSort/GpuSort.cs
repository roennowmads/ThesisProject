//--------------------------------------------------------------------------------------
// Imports
//--------------------------------------------------------------------------------------

using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

//--------------------------------------------------------------------------------------
// Classes
//--------------------------------------------------------------------------------------
   
static public class GpuSort
{
    // ---- Constants ----

    private const uint BITONIC_BLOCK_SIZE = 512;
    private const uint TRANSPOSE_BLOCK_SIZE = 32;

    // ---- Members ----

    static private ComputeShader sort32, sort32GLSL;
    static private ComputeShader sort64;
    static private int kSort32;
    static private int kSort64;
    static private int kTranspose32;
    static private int kTranspose64;
    static private bool init;

    // ---- Structures ----


    // ---- Methods ----

    static private void Init()
    {
        // Acquire compute shaders.
        sort32GLSL = (ComputeShader) Resources.Load("GpuSort/GLSLGpuSort32", typeof(ComputeShader));
        sort32 = (ComputeShader)Resources.Load("GpuSort/GpuSort32", typeof(ComputeShader));
        sort64 = (ComputeShader) Resources.Load("GpuSort/GpuSort64", typeof(ComputeShader));

        // If they were not found, crash!
        if (sort32GLSL == null) Debug.LogError("GpuSort32 not found.");
        if (sort32 == null) Debug.LogError("GpuSort32 not found.");
        if (sort64 == null) Debug.LogError("GpuSort64 not found.");

        // Find kernels
        kSort32 = sort32GLSL.FindKernel("GLSLBitonicSort");
        kSort64 = sort64.FindKernel("BitonicSort");
        kTranspose32 = sort32.FindKernel("MatrixTranspose");
        kTranspose64 = sort64.FindKernel("MatrixTranspose");

        // Done
        init = true;
    }

    static public void BitonicSort32(ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeBuffer pointsBuffer, Matrix4x4 transMatrix)
    {
        if (!init) Init();
        BitonicSortGeneric(sort32GLSL, sort32, kSort32, kTranspose32, inBuffer, tmpBuffer, pointsBuffer, transMatrix);   
    }

    static public void BitonicSort64(ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeBuffer pointsBuffer, Matrix4x4 transMatrix)
    {
        if (!init) Init();
        BitonicSortGeneric(sort64, sort64, kSort64, kTranspose64, inBuffer, tmpBuffer, pointsBuffer, transMatrix);
    }

    static private void BitonicSortGeneric(ComputeShader shader, ComputeShader shader2, int kSort, int kTranspose, ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeBuffer pointsBuffer, Matrix4x4 transMatrix)
    {
        // Determine if valid.
        if ((inBuffer.count % BITONIC_BLOCK_SIZE) != 0)
            Debug.LogError("Input array size should be multiple of the Bitonic block size!");

        // Determine parameters.
        uint NUM_ELEMENTS = (uint) inBuffer.count;
        uint MATRIX_WIDTH = BITONIC_BLOCK_SIZE;
        uint MATRIX_HEIGHT = NUM_ELEMENTS / BITONIC_BLOCK_SIZE;        

        /*shader.SetVector("row0", transMatrix.GetRow(0));
        shader.SetVector("row1", transMatrix.GetRow(1));
        shader.SetVector("row2", transMatrix.GetRow(2));
        shader.SetVector("row3", transMatrix.GetRow(3));*/

        shader.SetVector("camPos", Camera.main.transform.position);

        shader.SetFloats("model", transMatrix[0], transMatrix[1], transMatrix[2], transMatrix[3], 
                                  transMatrix[4], transMatrix[5], transMatrix[6], transMatrix[7],
                                  transMatrix[8], transMatrix[9], transMatrix[10], transMatrix[11],
                                  transMatrix[12], transMatrix[13], transMatrix[14], transMatrix[15]);

        shader.SetBuffer(kSort, "_Points", pointsBuffer);

        // Sort the data
        // First sort the rows for the levels <= to the block size
        for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
        {
            SetConstants(shader, shader2, level, level, MATRIX_HEIGHT, MATRIX_WIDTH);
            
            // Sort the row data
            shader.SetBuffer(kSort, "Data", inBuffer);
            shader.SetBuffer(kSort, "_Points", pointsBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
        }

        // Then sort the rows and columns for the levels > than the block size
        // Transpose. Sort the Columns. Transpose. Sort the Rows.
        for (uint level = (BITONIC_BLOCK_SIZE << 1); level <= NUM_ELEMENTS; level <<= 1)
        {
            // Transpose the data from buffer 1 into buffer 2
            SetConstants(shader, shader2, (level / BITONIC_BLOCK_SIZE), (level & ~NUM_ELEMENTS) / BITONIC_BLOCK_SIZE, MATRIX_WIDTH, MATRIX_HEIGHT);
            shader2.SetBuffer(kTranspose, "Input", inBuffer);
            shader2.SetBuffer(kTranspose, "Data", tmpBuffer);
            shader2.Dispatch(kTranspose, (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), 1);

            shader.SetBuffer(kSort, "Data", tmpBuffer);
            shader.SetBuffer(kSort, "_Points", pointsBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);

            // Transpose the data from buffer 2 back into buffer 1
            SetConstants(shader, shader2, BITONIC_BLOCK_SIZE, level, MATRIX_HEIGHT, MATRIX_WIDTH);
            shader2.SetBuffer(kTranspose, "Input", tmpBuffer);
            shader2.SetBuffer(kTranspose, "Data", inBuffer);
            shader2.Dispatch(kTranspose, (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), 1);

            shader.SetBuffer(kSort, "Data", inBuffer);
            shader.SetBuffer(kSort, "_Points", pointsBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
        }
    }

    static private void SetConstants(ComputeShader shader, ComputeShader shader2, uint iLevel, uint iLevelMask, uint iWidth, uint iHeight)
    {
        shader.SetInt("g_iLevel", (int) iLevel);
        shader.SetInt("g_iLevelMask", (int) iLevelMask);
        shader.SetInt("g_iWidth", (int) iWidth);
        shader.SetInt("g_iHeight", (int) iHeight);

        shader2.SetInt("g_iLevel", (int)iLevel);
        shader2.SetInt("g_iLevelMask", (int)iLevelMask);
        shader2.SetInt("g_iWidth", (int)iWidth);
        shader2.SetInt("g_iHeight", (int)iHeight);
    }
}
