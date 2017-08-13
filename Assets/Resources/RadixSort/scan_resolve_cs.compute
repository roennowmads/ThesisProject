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
 * Take a scan array which has been scanned per-block and sum array (inclusive scan for per-block results)
 * and resolve the result to get a complete inclusive scan result.
 * Needed if we need to do scan in multiple stages.
 */

layout(local_size_x = 32) in; // We work on 4 items at once, so this value should be BLOCK_SIZE / 4.
layout(binding = 0, std430) readonly buffer Data
{
    uvec4 buf[];
};

layout(binding = 1, std430) readonly buffer BlockSumData
{
    uvec4 blocksum[];
};

layout(binding = 2, std430) writeonly buffer OutData
{
    uvec4 outbuf[];
};

void main()
{
    uint ident = gl_GlobalInvocationID.x;
    uint wg_ident = gl_WorkGroupID.x;
    uvec4 miniblock0 = buf[4u * ident + 0u];
    uvec4 miniblock1 = buf[4u * ident + 1u];
    uvec4 miniblock2 = buf[4u * ident + 2u];
    uvec4 miniblock3 = buf[4u * ident + 3u];
    if (wg_ident != 0u) {
        uvec4 prev_sum = blocksum[wg_ident - 1u];
        miniblock0 += prev_sum;
        miniblock1 += prev_sum;
        miniblock2 += prev_sum;
        miniblock3 += prev_sum;
    }
    outbuf[4u * ident + 0u] = miniblock0;
    outbuf[4u * ident + 1u] = miniblock1;
    outbuf[4u * ident + 2u] = miniblock2;
    outbuf[4u * ident + 3u] = miniblock3;
}

