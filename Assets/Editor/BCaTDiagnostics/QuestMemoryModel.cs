using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace BCaT.EditorTools.Diagnostics
{
    /// <summary>One accounted asset. Sizes are bytes.</summary>
    public sealed class AssetRecord
    {
        public string Key;              // guid:localId — the identity used for de-duplication
        public string Guid;
        public string Path;
        public string Name;
        public string Type;             // Unity object type
        public string Category;         // BCaT content category
        public long CpuBytes;
        public long GpuBytes;
        public string Detail;           // dimensions / format / load type — how the number was reached
        public string Confidence;

        public long TotalBytes => CpuBytes + GpuBytes;
    }

    /// <summary>
    /// Type-appropriate runtime size models. Every method returns the resident
    /// cost of the IMPORTED representation, never the source file size.
    /// </summary>
    public static class QuestMemoryModel
    {
        public const double Mb = 1024.0 * 1024.0;

        // ---- Textures -------------------------------------------------------

        /// <summary>
        /// Exact block-compression aware size: for each mip level, the number of
        /// compressed blocks times the block size, so ASTC 6x6 is never charged
        /// as width*height*4. Cubemaps multiply by 6 faces, arrays by depth.
        /// </summary>
        public static long TextureBytes(Texture texture, out string detail, out bool readable)
        {
            readable = false;
            detail = "unknown";
            if (texture == null)
                return 0;

            int width = texture.width;
            int height = texture.height;
            GraphicsFormat format = texture.graphicsFormat;
            int mipCount = 1;
            int slices = 1;
            string kind = texture.GetType().Name;

            switch (texture)
            {
                case Texture2D t2d:
                    mipCount = Mathf.Max(1, t2d.mipmapCount);
                    readable = t2d.isReadable;
                    break;
                case Cubemap cube:
                    mipCount = Mathf.Max(1, cube.mipmapCount);
                    slices = 6;
                    readable = cube.isReadable;
                    break;
                case Texture2DArray array:
                    mipCount = Mathf.Max(1, array.mipmapCount);
                    slices = array.depth;
                    readable = array.isReadable;
                    break;
                case CubemapArray cubeArray:
                    mipCount = Mathf.Max(1, cubeArray.mipmapCount);
                    slices = 6 * cubeArray.cubemapCount;
                    readable = cubeArray.isReadable;
                    break;
                case Texture3D volume:
                    mipCount = Mathf.Max(1, volume.mipmapCount);
                    slices = volume.depth;
                    readable = volume.isReadable;
                    break;
                case RenderTexture rt:
                    return RenderTextureBytes(rt, out detail);
                default:
                    mipCount = 1;
                    break;
            }

            long perSlice = MipChainBytes(width, height, mipCount, format);
            long total = perSlice * Math.Max(1, slices);

            detail = $"{kind} {width}x{height}" +
                     (slices > 1 ? $"x{slices}" : string.Empty) +
                     $" {format} mips={mipCount} readable={readable}";
            return total;
        }

        /// <summary>Sum of every mip level, each rounded up to whole compression blocks.</summary>
        public static long MipChainBytes(int width, int height, int mipCount, GraphicsFormat format)
        {
            int blockWidth = Math.Max(1, (int)GraphicsFormatUtility.GetBlockWidth(format));
            int blockHeight = Math.Max(1, (int)GraphicsFormatUtility.GetBlockHeight(format));
            int blockBytes = Math.Max(1, (int)GraphicsFormatUtility.GetBlockSize(format));

            long total = 0;
            int w = Math.Max(1, width);
            int h = Math.Max(1, height);
            for (int mip = 0; mip < Math.Max(1, mipCount); mip++)
            {
                long blocksX = (w + blockWidth - 1) / blockWidth;
                long blocksY = (h + blockHeight - 1) / blockHeight;
                total += blocksX * blocksY * blockBytes;
                if (w == 1 && h == 1)
                    break;
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }
            return total;
        }

        public static long RenderTextureBytes(RenderTexture rt, out string detail)
        {
            if (rt == null)
            {
                detail = "null";
                return 0;
            }

            long color = MipChainBytes(rt.width, rt.height, rt.useMipMap ? Mathf.Max(1, rt.mipmapCount) : 1,
                                       rt.graphicsFormat);
            long depth = 0;
            if (rt.depth > 0)
                depth = (long)rt.width * rt.height * (rt.depth >= 24 ? 4 : 2);

            long slices = rt.dimension == UnityEngine.Rendering.TextureDimension.Cube ? 6
                        : rt.dimension == UnityEngine.Rendering.TextureDimension.Tex2DArray ? Math.Max(1, rt.volumeDepth)
                        : 1;

            detail = $"RenderTexture {rt.width}x{rt.height} {rt.graphicsFormat} depth={rt.depth} " +
                     $"dim={rt.dimension} mips={rt.useMipMap}";
            return color * slices + depth;
        }

        // ---- Meshes ---------------------------------------------------------

        /// <summary>
        /// Vertex buffers from the real per-stream strides, index buffer from
        /// the real index count and format, plus blend-shape deltas. A readable
        /// mesh keeps a full CPU copy on top of the GPU buffers.
        /// </summary>
        public static long MeshBytes(Mesh mesh, out long cpuBytes, out string detail)
        {
            cpuBytes = 0;
            detail = "unknown";
            if (mesh == null)
                return 0;

            long vertexBytes = 0;
            int streams = Mathf.Max(1, mesh.vertexBufferCount);
            for (int stream = 0; stream < streams; stream++)
                vertexBytes += (long)mesh.GetVertexBufferStride(stream) * mesh.vertexCount;

            long indexCount = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                indexCount += mesh.GetIndexCount(sub);
            long indexBytes = indexCount * (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 ? 2 : 4);

            // Blend-shape frames hold position (+normal +tangent) deltas per
            // affected vertex; 40 bytes per vertex per frame is the full set.
            long blendBytes = 0;
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                blendBytes += (long)mesh.GetBlendShapeFrameCount(shape) * mesh.vertexCount * 40;

            long gpuBytes = vertexBytes + indexBytes + blendBytes;
            if (mesh.isReadable)
                cpuBytes = gpuBytes;

            detail = $"verts={mesh.vertexCount} indices={indexCount} streams={streams} " +
                     $"idx={mesh.indexFormat} sub={mesh.subMeshCount} blendShapes={mesh.blendShapeCount} " +
                     $"readable={mesh.isReadable}";
            return gpuBytes;
        }

        // ---- Audio ----------------------------------------------------------

        /// <summary>
        /// Resident audio depends entirely on Load Type, so this reads the
        /// Android importer override rather than the clip's source bytes.
        /// </summary>
        public static long AudioBytes(AudioClip clip, string assetPath, out string detail, out string confidence)
        {
            detail = "unknown";
            confidence = "ESTIMATED";
            if (clip == null)
                return 0;

            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            AudioClipLoadType loadType = clip.loadType;
            AudioCompressionFormat compression = AudioCompressionFormat.Vorbis;
            float quality = 1f;
            bool overridden = false;
            bool preload = clip.preloadAudioData;

            if (importer != null)
            {
                AudioImporterSampleSettings settings =
                    importer.ContainsSampleSettingsOverride(QuestPreflightConfig.AndroidPlatformName)
                        ? importer.GetOverrideSampleSettings(QuestPreflightConfig.AndroidPlatformName)
                        : importer.defaultSampleSettings;
                overridden = importer.ContainsSampleSettingsOverride(QuestPreflightConfig.AndroidPlatformName);
                loadType = settings.loadType;
                compression = settings.compressionFormat;
                quality = settings.quality;
                preload = settings.preloadAudioData;
            }

            long samples = (long)clip.samples * Math.Max(1, clip.channels);
            long bytes;
            string how;

            switch (loadType)
            {
                case AudioClipLoadType.DecompressOnLoad:
                    bytes = samples * QuestPreflightConfig.DecompressedBytesPerSample;
                    how = "decompressed PCM16 resident";
                    confidence = "CALCULATED";
                    break;

                case AudioClipLoadType.CompressedInMemory:
                    bytes = CompressedBytes(clip, compression, quality, samples, out how);
                    confidence = compression == AudioCompressionFormat.PCM ? "CALCULATED" : "ESTIMATED";
                    break;

                case AudioClipLoadType.Streaming:
                    bytes = (long)(QuestPreflightConfig.StreamingClipBufferMB * Mb);
                    how = "streaming: ring buffer + decoder only, clip not resident";
                    confidence = "ESTIMATED";
                    break;

                default:
                    bytes = samples * QuestPreflightConfig.DecompressedBytesPerSample;
                    how = "unknown load type, assumed decompressed";
                    break;
            }

            detail = $"{clip.length:0.0}s {clip.channels}ch {clip.frequency}Hz load={loadType} " +
                     $"fmt={compression} q={quality:0.00} preload={preload} " +
                     $"androidOverride={overridden} ({how})";
            return bytes;
        }

        static long CompressedBytes(AudioClip clip, AudioCompressionFormat compression, float quality,
                                    long samples, out string how)
        {
            switch (compression)
            {
                case AudioCompressionFormat.PCM:
                    how = "PCM16 in memory";
                    return samples * 2;
                case AudioCompressionFormat.ADPCM:
                    how = "ADPCM ~4 bits/sample in memory";
                    return samples / 2;
                default:
                    // Vorbis/AAC: duration x modelled bitrate.
                    double kbps = QuestPreflightConfig.VorbisKbpsPerChannelAtQuality1 *
                                  Mathf.Clamp01(quality) * Math.Max(1, clip.channels);
                    how = $"{compression} ~{kbps:0} kbit/s in memory";
                    return (long)(clip.length * kbps * 1000.0 / 8.0);
            }
        }

        // ---- Terrain --------------------------------------------------------

        /// <summary>
        /// Heightmap, splatmaps, detail layers and tree instances. Terrain LAYER
        /// textures are deliberately excluded: they are scene dependencies and
        /// are already counted once in the texture pass.
        /// </summary>
        public static long TerrainBytes(TerrainData terrain, out string detail, out List<string> breakdown)
        {
            breakdown = new List<string>();
            detail = "unknown";
            if (terrain == null)
                return 0;

            int hmRes = terrain.heightmapResolution;
            // R16 heightmap texture on the GPU plus the CPU height array Unity
            // keeps for collision and sampling.
            long heightmapGpu = (long)hmRes * hmRes * 2;
            long heightmapCpu = (long)hmRes * hmRes * 4;

            long alphamap = (long)terrain.alphamapWidth * terrain.alphamapHeight *
                            Math.Max(1, terrain.alphamapTextureCount) * 4;

            int detailRes = terrain.detailResolution;
            int detailLayers = terrain.detailPrototypes != null ? terrain.detailPrototypes.Length : 0;
            long detailBytes = (long)detailRes * detailRes * detailLayers * 2;

            long treeBytes = (long)terrain.treeInstanceCount * 44;

            breakdown.Add($"heightmap {hmRes}x{hmRes}: GPU {heightmapGpu / Mb:0.0} MB + CPU {heightmapCpu / Mb:0.0} MB");
            breakdown.Add($"alphamaps {terrain.alphamapWidth}x{terrain.alphamapHeight} x{terrain.alphamapTextureCount} RGBA: {alphamap / Mb:0.0} MB");
            breakdown.Add($"detail {detailRes}x{detailRes} x{detailLayers} layers: {detailBytes / Mb:0.0} MB");
            breakdown.Add($"trees {terrain.treeInstanceCount} instances: {treeBytes / Mb:0.0} MB");
            breakdown.Add($"terrain layers: {(terrain.terrainLayers != null ? terrain.terrainLayers.Length : 0)} " +
                          "(layer textures counted once in the texture pass, not here)");

            detail = $"heightmap={hmRes} alphamap={terrain.alphamapWidth}x{terrain.alphamapHeight}" +
                     $"x{terrain.alphamapTextureCount} detail={detailRes}x{detailLayers} " +
                     $"trees={terrain.treeInstanceCount} layers={(terrain.terrainLayers != null ? terrain.terrainLayers.Length : 0)}";

            return heightmapGpu + heightmapCpu + alphamap + detailBytes + treeBytes;
        }

        // ---- XR render targets ----------------------------------------------

        public static long EyeBufferBytes(float renderScale, int msaaSamples, out string detail)
        {
            int width = Mathf.RoundToInt(QuestPreflightConfig.QuestEyeWidth * Mathf.Max(0.1f, renderScale));
            int height = Mathf.RoundToInt(QuestPreflightConfig.QuestEyeHeight * Mathf.Max(0.1f, renderScale));

            int msaaFactor = QuestPreflightConfig.CountMsaaAsFullAllocation ? Mathf.Max(1, msaaSamples) : 1;

            long colorPerImage = (long)width * height * QuestPreflightConfig.EyeColorBytesPerPixel * msaaFactor;
            long color = colorPerImage * QuestPreflightConfig.EyeSwapchainImages * 2; // two eyes
            long depth = (long)width * height * QuestPreflightConfig.EyeDepthBytesPerPixel * 2 * msaaFactor;

            detail = $"{width}x{height} per eye x2 eyes, {QuestPreflightConfig.EyeSwapchainImages} swapchain images, " +
                     $"RGBA8 colour + D24S8 depth, MSAA={msaaSamples}" +
                     (QuestPreflightConfig.CountMsaaAsFullAllocation
                        ? " (counted at full allocation)"
                        : " (resolved from tile memory, not multiplied)");
            return color + depth;
        }
    }
}
