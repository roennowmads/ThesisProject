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

    static private ComputeShader sort32;
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
        sort32 = (ComputeShader) Resources.Load("GpuSort/GpuSort32", typeof(ComputeShader));
        sort64 = (ComputeShader) Resources.Load("GpuSort/GpuSort64", typeof(ComputeShader));

        // If they were not found, crash!
        if (sort32 == null) Debug.LogError("GpuSort32 not found.");
        if (sort64 == null) Debug.LogError("GpuSort64 not found.");

        // Find kernels
        kSort32 = sort32.FindKernel("BitonicSort");
        kSort64 = sort64.FindKernel("BitonicSort");
        kTranspose32 = sort32.FindKernel("MatrixTranspose");
        kTranspose64 = sort64.FindKernel("MatrixTranspose");

        // Done
        init = true;
    }

    static public void BitonicSort32(ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeBuffer pointsBuffer, Vector4 trans, Matrix4x4 transMatrix, Matrix4x4 viewMatrix)
    {
        if (!init) Init();
        BitonicSortGeneric(sort32, kSort32, kTranspose32, inBuffer, tmpBuffer, pointsBuffer, trans, transMatrix, viewMatrix);   
    }

    static public void BitonicSort64(ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeBuffer pointsBuffer, Vector4 trans, Matrix4x4 transMatrix, Matrix4x4 viewMatrix)
    {
        if (!init) Init();
        BitonicSortGeneric(sort64, kSort64, kTranspose64, inBuffer, tmpBuffer, pointsBuffer, trans, transMatrix, viewMatrix);
    }

    static private void BitonicSortGeneric(ComputeShader shader, int kSort, int kTranspose, ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeBuffer pointsBuffer, Vector4 trans, Matrix4x4 transMatrix, Matrix4x4 viewMatrix)
    {
        // Determine if valid.
        if ((inBuffer.count % BITONIC_BLOCK_SIZE) != 0)
            Debug.LogError("Input array size should be multiple of the Bitonic block size!");

        // Determine parameters.
        uint NUM_ELEMENTS = (uint) inBuffer.count;
        uint MATRIX_WIDTH = BITONIC_BLOCK_SIZE;
        uint MATRIX_HEIGHT = NUM_ELEMENTS / BITONIC_BLOCK_SIZE;        

        shader.SetVector("row0", transMatrix.GetRow(0));
        shader.SetVector("row1", transMatrix.GetRow(1));
        shader.SetVector("row2", transMatrix.GetRow(2));
        shader.SetVector("row3", transMatrix.GetRow(3));

        /*shader.SetVector("rowView0", viewMatrix.GetRow(0));
        shader.SetVector("rowView1", viewMatrix.GetRow(1));
        shader.SetVector("rowView2", viewMatrix.GetRow(2));
        shader.SetVector("rowView3", viewMatrix.GetRow(3));*/

        shader.SetVector("trans", trans);

        shader.SetVector("camPos", Camera.main.transform.position);

        /*shader.SetFloats("model", transMatrix[0], transMatrix[1], transMatrix[2], transMatrix[3], 
                                  transMatrix[4], transMatrix[5], transMatrix[6], transMatrix[7],
                                  transMatrix[8], transMatrix[9], transMatrix[10], transMatrix[11],
                                  transMatrix[12], transMatrix[13], transMatrix[14], transMatrix[15]);*/

        shader.SetBuffer(kSort, "_Points", pointsBuffer);

        // Sort the data
        // First sort the rows for the levels <= to the block size
        for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
        {
            SetConstants(shader, level, level, MATRIX_HEIGHT, MATRIX_WIDTH);
            
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
            SetConstants(shader, (level / BITONIC_BLOCK_SIZE), (level & ~NUM_ELEMENTS) / BITONIC_BLOCK_SIZE, MATRIX_WIDTH, MATRIX_HEIGHT);
            shader.SetBuffer(kTranspose, "Input", inBuffer);
            shader.SetBuffer(kTranspose, "Data", tmpBuffer);
            shader.Dispatch(kTranspose, (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), 1);

            shader.SetBuffer(kSort, "Data", tmpBuffer);
            shader.SetBuffer(kSort, "_Points", pointsBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);

            // Transpose the data from buffer 2 back into buffer 1
            SetConstants(shader, BITONIC_BLOCK_SIZE, level, MATRIX_HEIGHT, MATRIX_WIDTH);
            shader.SetBuffer(kTranspose, "Input", tmpBuffer);
            shader.SetBuffer(kTranspose, "Data", inBuffer);
            shader.Dispatch(kTranspose, (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), 1);

            shader.SetBuffer(kSort, "Data", inBuffer);
            shader.SetBuffer(kSort, "_Points", pointsBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
        }
    }

    static private void SetConstants(ComputeShader shader, uint iLevel, uint iLevelMask, uint iWidth, uint iHeight)
    {
        shader.SetInt("g_iLevel", (int) iLevel);
        shader.SetInt("g_iLevelMask", (int) iLevelMask);
        shader.SetInt("g_iWidth", (int) iWidth);
        shader.SetInt("g_iHeight", (int) iHeight);
    }
}
