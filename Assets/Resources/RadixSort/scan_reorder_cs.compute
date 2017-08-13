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
 * This program merges the block-wise prefix sums of the input into a
 * single prefix sum. It does this by adding the sum of elements in a previous
 * block to the next one, accumulating the sum as we go along. The sums to
 * add are located in the blocksum buffer.
 *
 * Finally, we shuffle the elements to their correct positions.
 * The new position is given by the prefix
 * sum value in the element's scan array.
 *
 * For example, if our keys (for the current stage) are as following:
 *     1 3 2 0 1 0 2 2
 *
 * We get these digit scan arrays
 *     0 0 0 0 1 1 2 2 <- 0
 *     2 3 3 3 3 4 4 4 <- 1
 *     4 4 4 5 5 5 5 6 <- 2
 *     7 7 8 8 8 8 8 8 <- 3
 *
 * And these flags
 *     0 0 0 1 0 1 0 0 <- 0
 *     1 0 0 0 1 0 0 0 <- 1
 *     0 0 1 0 0 0 1 1 <- 2
 *     0 1 0 0 0 0 0 0 <- 3
 *
 * (Note that the columns of these are stored as uvec4s in the buffers)
 *
 * In column 1 we have a flag in the second row, because the key is (1) in the first position.
 * Looking at the scan array belonging to (1) we find that the offset is 2.
 * So the first element should be reordered to position 2 in the sorted array.
 */

struct Particle {
    vec4 pos, rest;
};

layout(local_size_x = 32) in; // We work on 4 items at once, so this value should be BLOCK_SIZE / 4.
layout(binding = 0, std430) readonly buffer SortData
{
    Particle sort_buf[];
};

layout(binding = 1, std430) readonly buffer Data
{
    uvec4 buf[];
};

layout(binding = 2, std430) readonly buffer BlockSumData
{
    uvec4 blocksum[];
};

layout(binding = 3, std430) writeonly buffer OutSortData
{
    Particle out_sort_buf[];
};

layout(binding = 4, std430) readonly buffer FlagsData
{
    uvec4 flags[];
};

layout(location = 0) uniform int uShift;

void main()
{
    uint ident = gl_GlobalInvocationID.x;
    uint wg_ident = gl_WorkGroupID.x;

    // The last blocksum in our buffer contains the count of values which are 0, 1, 2, and 3 respectively
    // since it's an inclusive scan.
    uvec4 carry = blocksum[gl_NumWorkGroups.x - 1u];

    // Do an exclusive scan over the carry values.
    // We can now figure out the base offset for all the different values.
    carry = uvec4(0u, carry.x, carry.x + carry.y, carry.x + carry.y + carry.z);

    // Cached results computed in scan_first.cs.
    uvec4 lookups = flags[ident];

    // Swizzle the carry vector so we know the base offset for our data.
    // Subtract -1 on the carry to get exclusive scan instead of inclusive scan.
    // We're always going to add in 1 due to inclusive scan results so we have to compensate for it somewhere.
    carry = uvec4(carry[lookups.x], carry[lookups.y], carry[lookups.z], carry[lookups.w]) - 1u;

    // Add in per-digit inclusive scan results.
    carry.x += buf[4u * ident + 0u][lookups.x];
    carry.y += buf[4u * ident + 1u][lookups.y];
    carry.z += buf[4u * ident + 2u][lookups.z];
    carry.w += buf[4u * ident + 3u][lookups.w];

    // We also have to add in blocksum for the previous block.
    // If we're the first block, there is nothing to add.
    if (wg_ident != 0u) {
        uvec4 prev_sum = blocksum[wg_ident - 1u];
        carry.x += prev_sum[lookups.x];
        carry.y += prev_sum[lookups.y];
        carry.z += prev_sum[lookups.z];
        carry.w += prev_sum[lookups.w];
    }

    // Reorder data since we now know the output indices.
    // Not the most cache friendly operation.
    out_sort_buf[carry.x] = sort_buf[4u * ident + 0u];
    out_sort_buf[carry.y] = sort_buf[4u * ident + 1u];
    out_sort_buf[carry.z] = sort_buf[4u * ident + 2u];
    out_sort_buf[carry.w] = sort_buf[4u * ident + 3u];
}

