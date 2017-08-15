using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


//source:
//https://github.com/notlion/Splat

public class RadixSort  {

    ComputeShader scanProg, scanFirstProg, resolveProg, reorderProg;
    int m_elemCount, m_blockSize, m_scanLevelCount;
    ComputeBuffer sortedBuffer, flagsBuffer;

    List<ComputeBuffer> scanBuffers, sumBuffers;
    int scanKernel, scanFirstKernel, resolveKernel, reorderKernel;

    public RadixSort (int elemCount, int blockSize)
    {
        m_elemCount = elemCount;
        m_blockSize = blockSize;
        //auto fmt = gl::GlslProg::Format();

        scanProg = (ComputeShader)Resources.Load("RadixSort/scan_cs", typeof(ComputeShader));
        scanFirstProg = (ComputeShader)Resources.Load("RadixSort/scan_first_cs", typeof(ComputeShader));
        resolveProg = (ComputeShader)Resources.Load("RadixSort/scan_resolve_cs", typeof(ComputeShader));
        reorderProg = (ComputeShader)Resources.Load("RadixSort/scan_reorder_cs", typeof(ComputeShader));
        

        scanKernel = scanProg.FindKernel("scan");
        scanFirstKernel = scanFirstProg.FindKernel("scanFirst");
        resolveKernel = resolveProg.FindKernel("scanResolve");
        reorderKernel = reorderProg.FindKernel("scanReorder");

        {
            m_scanLevelCount = 0;

            int size = elemCount;
            while (size > 1)
            {
                m_scanLevelCount++;
                size = (size + blockSize - 1) / blockSize;
            }
        }

        sortedBuffer = new ComputeBuffer(elemCount, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Default);//gl::Ssbo::create(elemCount * sizeof(Particle), nullptr, GL_DYNAMIC_COPY);
        flagsBuffer = new ComputeBuffer(elemCount, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default);//gl::Ssbo::create(elemCount * sizeof(GLuint), nullptr, GL_DYNAMIC_COPY);

        {
            scanBuffers = new List<ComputeBuffer>(/*scanLevelCount*/);
            sumBuffers = new List<ComputeBuffer>(/*scanLevelCount*/);

            int blockCount = elemCount / blockSize;
            for (uint i = 0; i < m_scanLevelCount; ++i)
            {
                scanBuffers.Add(new ComputeBuffer(blockCount * blockSize, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default));
                blockCount = (blockCount + blockSize - 1) / blockSize;
                sumBuffers.Add(new ComputeBuffer(blockCount * blockSize, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default));
            }
        }
    }

    void sortBits(ComputeBuffer inputBuf, ComputeBuffer outputBuf, int bitOffset, Vector3 axis, float zMin, float zMax) {
      // Keep track of which dispatch sizes we used to make the resolve steps simpler.
      List<int> dispatchSizes = new List<int>();

      int blockCount = m_elemCount / m_blockSize;

      {
            // First pass. Compute 16-bit unsigned depth and apply first pass of scan algorithm.

            scanFirstProg.SetBuffer(scanFirstKernel, "IndicesIn", inputBuf);
            scanFirstProg.SetBuffer(scanFirstKernel, "OutData", scanBuffers[0]);
            scanFirstProg.SetBuffer(scanFirstKernel, "BlockSumData", sumBuffers[0]);
            scanFirstProg.SetBuffer(scanFirstKernel, "FlagsData", flagsBuffer);

            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 0, inputBufId);
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, scanBuffers.front()->getId());
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 2, sumBuffers.front()->getId());
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 3, flagsBuffer->getId());

            scanFirstProg.SetInt("bitOffset", bitOffset);
            scanFirstProg.SetVector("axis", axis);
            scanFirstProg.SetFloat("zMin", zMin);
            scanFirstProg.SetFloat("zMax", zMax);

            dispatchSizes.Add(blockCount);
            scanFirstProg.Dispatch(scanFirstKernel, blockCount, 1, 1);

            //scanFirstProg->bind();
            //scanFirstProg->uniform("bitOffset", bitOffset);
            //scanFirstProg->uniform("axis", axis);
            //scanFirstProg->uniform("zMin", zMin);
            //scanFirstProg->uniform("zMax", zMax);

            
            //glDispatchCompute(blockCount, 1, 1);
            //glMemoryBarrier(GL_SHADER_STORAGE_BARRIER_BIT);
        }

      {
            // If we processed more than one work group of data, we're not done, so scan buf_sums[0] and
            // keep scanning recursively like this until buf_sums[N] becomes a single value.

            //scanProg->bind();

            for (int i = 1; i < m_scanLevelCount; ++i) {
                blockCount = (blockCount + m_blockSize - 1) / m_blockSize;
                dispatchSizes.Add(blockCount);

                scanProg.SetBuffer(scanKernel, "Data", sumBuffers[i - 1]);

                if (blockCount <= 1)
                {
                    scanProg.SetBuffer(scanKernel, "OutData", sumBuffers[i - 1]);
                }
                else
                {
                    scanProg.SetBuffer(scanKernel, "OutData", sumBuffers[i]);
                }

                scanProg.SetBuffer(scanKernel, "BlockSumData", sumBuffers[i]);

                scanProg.Dispatch(scanKernel, blockCount, 1, 1);


                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 0, sumBuffers[i - 1]->getId());
                // If we only do one work group we don't need to resolve it later,
                // and we can update the scan buffer inplace.
                //if (blockCount <= 1) {
                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, sumBuffers[i - 1]->getId());
                //} else {
                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, scanBuffers[i]->getId());
                //}
                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 2, sumBuffers[i]->getId());

                //glDispatchCompute(blockCount, 1, 1);
                //glMemoryBarrier(GL_SHADER_STORAGE_BARRIER_BIT);
            }
      }

      {
            // Go backwards, we want to end up with a buf_sums[0] which has been properly scanned.
            // Once we have buf_scan[0] and buf_sums[0], we can do the reordering step.

            //resolveProg->bind();

            for (int i = m_scanLevelCount - 1; i > 0; --i) {
                if (dispatchSizes[i] <= 1) { // No need to resolve, buf_sums[i - 1] is already correct
                    continue;
                }

                resolveProg.SetBuffer(resolveKernel, "Data", scanBuffers[i]);
                resolveProg.SetBuffer(resolveKernel, "BlockSumData", sumBuffers[i]);
                resolveProg.SetBuffer(resolveKernel, "OutData", sumBuffers[i - 1]);

                resolveProg.Dispatch(resolveKernel, dispatchSizes[i], 1, 1);

                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 0, scanBuffers[i]->getId());
                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, sumBuffers[i]->getId());
                //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 2, sumBuffers[i - 1]->getId());

                //glDispatchCompute(dispatchSizes[i], 1, 1);
                //glMemoryBarrier(GL_SHADER_STORAGE_BARRIER_BIT);
            }
      }

      {
            // We can now reorder our input properly.

            //reorderProg->bind();

            /*uint[] scandat = new uint[scanBuffers[0].count*4];
            scanBuffers[0].GetData(scandat);
            uint[] dat = new uint[sumBuffers[0].count*4];
            sumBuffers[0].GetData(dat);*/

            reorderProg.SetBuffer(reorderKernel, "IndicesIn", inputBuf);
            reorderProg.SetBuffer(reorderKernel, "Data", scanBuffers[0]);
            reorderProg.SetBuffer(reorderKernel, "BlockSumData", sumBuffers[0]);
            reorderProg.SetBuffer(reorderKernel, "IndicesOut", outputBuf);
            reorderProg.SetBuffer(reorderKernel, "FlagsData", flagsBuffer);

            reorderProg.Dispatch(reorderKernel, m_elemCount / m_blockSize, 1, 1);

            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 0, inputBufId);
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, scanBuffers[0]->getId());
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 2, sumBuffers[0]->getId());
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 3, outputBufId);
            //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 4, flagsBuffer->getId());

            //glDispatchCompute(elemCount / blockSize, 1, 1);
            //glMemoryBarrier(GL_SHADER_STORAGE_BARRIER_BIT);
      }

      //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 0, 0);
      //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, 0);
      //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 2, 0);
      //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 3, 0);
      //glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 4, 0);
    }

    public void sort(ComputeBuffer inputBuf, ComputeBuffer outputBuf, Vector3 axis, float zMin, float zMax)
    {
        //uint sortedBufId = sortedBuffer->getId();

        sortBits(inputBuf, sortedBuffer, 0, axis, zMin, zMax);

        //std::swap(inputBuf, sortedBuffer);
        ComputeBuffer temp = inputBuf;
        inputBuf = sortedBuffer;
        sortedBuffer = temp;

        for (int i = 1; i < /*8*//*16*//*8*/8; ++i)
        {
            sortBits(inputBuf, outputBuf, i * 2, axis, zMin, zMax);
            //std::swap(inputBuf, outputBuf);
            temp = inputBuf;
            inputBuf = outputBuf;
            outputBuf = temp;
        }

        // We use the position data to draw the particles afterwards
        // Thus we need to ensure that the data is up to date
        //glMemoryBarrier(GL_VERTEX_ATTRIB_ARRAY_BARRIER_BIT);
    }
}
