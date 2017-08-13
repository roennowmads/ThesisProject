#version 430 core

/*
 * This proprietary software may be used only as
 * authorised by a licensing agreement from ARM Limited
 * (C) COPYRIGHT 2014-2015 ARM Limited
 *     ALL RIGHTS RESERVED
 * The entire notice above must be reproduced on all authorised
 * copies and copies may only be made to the extent permitted
 * by a licensing agreement from ARM Limited.
 */

/*
 * See scan_first.cs for more detailed comments.
 * This shader is used for scan passes after the first one and is very similar.
 */

layout(local_size_x = 32) in; // We work on 4 items at once, so this value should be BLOCK_SIZE / 4.
#define NUM_STEPS 4u

layout(binding = 0, std430) readonly buffer Data
{
    uvec4 buf[];
};

layout(binding = 1, std430) writeonly buffer OutData
{
    uvec4 outbuf[];
};

layout(binding = 2, std430) writeonly buffer BlockSumData
{
    uvec4 blocksum[];
};

shared uvec4 sharedData[gl_WorkGroupSize.x];

void main()
{
    uint ident = gl_GlobalInvocationID.x;
    uint local_ident = gl_LocalInvocationID.x;

    // First we want to do the scan in registers to reduce shared memory pressure a bit.
    uvec4 miniblock0 = buf[4u * ident + 0u];
    uvec4 miniblock1 = buf[4u * ident + 1u];
    uvec4 miniblock2 = buf[4u * ident + 2u];
    uvec4 miniblock3 = buf[4u * ident + 3u];
    miniblock1 += miniblock0;
    miniblock2 += miniblock1;
    miniblock3 += miniblock2;

    // We have now done inclusive scan for our "miniblock".
    // We only share our accumulated sum (miniblock3) with other threads.

    // Share miniblock sum with other threads
    sharedData[local_ident] = miniblock3;
    memoryBarrierShared();
    barrier();

    // Now we have to start accumulating across threads.
    // We double the "block size" every iteration. Odd blocks accumulate the scan value from just before the block.

    for (uint step = 0u; step < NUM_STEPS; step++) {
        // Half the threads will have something useful to do every step.
        // Branching like this is a not an issue on Mali as long as we keep enough threads busy doing something useful.
        if ((local_ident & (1u << step)) != 0u) {
            // Get previous index. This value will be the same for every thread within this "block".
            uint prev = ((local_ident >> step) << step) - 1u;

            // Update our block. Always accumulate data in registers.
            uvec4 sum_prev = sharedData[prev];
            miniblock0 += sum_prev;
            miniblock1 += sum_prev;
            miniblock2 += sum_prev;
            miniblock3 += sum_prev;

            // Write out current value.
            sharedData[local_ident] = miniblock3;
        }
        memoryBarrierShared();
        barrier();
    }

    // We don't need barrier after last iteration, so unroll that manually.
    if ((local_ident & (1u << NUM_STEPS)) != 0u) {
        // Get previous index. This value will be the same for every thread within this "block".
        uint prev = ((local_ident >> NUM_STEPS) << NUM_STEPS) - 1u;

        // Update our block. Always accumulate data in registers.
        uvec4 sum_prev = sharedData[prev];
        miniblock0 += sum_prev;
        miniblock1 += sum_prev;
        miniblock2 += sum_prev;
        miniblock3 += sum_prev;
    }

    // Write out inclusive scan results.
    outbuf[4u * ident + 0u] = miniblock0;
    outbuf[4u * ident + 1u] = miniblock1;
    outbuf[4u * ident + 2u] = miniblock2;
    outbuf[4u * ident + 3u] = miniblock3;

    // Last thread knows the inclusive scan for this work group, so write out to blocksum.
    if (local_ident == (gl_WorkGroupSize.x - 1u))
        blocksum[gl_WorkGroupID.x] = miniblock3;
}

