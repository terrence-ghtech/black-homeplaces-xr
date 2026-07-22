#ifndef REALBLEND_SPOM_INCLUDED
#define REALBLEND_SPOM_INCLUDED

float4 _SPOM_Clip_Bounds;
float _SPOM_Clip_Softness;
float _SPOM_Clip_Grazing_Fade;
float _SPOM_Clip_Dither;

float3 RealBlendSPOMObjectScale_float()
{
    float3 objectScale = float3(1.0, 1.0, 1.0);
    float4x4 worldTransform = GetObjectToWorldMatrix();

    objectScale.x = length(float3(worldTransform._m00, worldTransform._m10, worldTransform._m20));
    objectScale.z = length(float3(worldTransform._m02, worldTransform._m12, worldTransform._m22));

    return objectScale;
}

half3 RealBlendSPOMObjectScale_half()
{
    return (half3)RealBlendSPOMObjectScale_float();
}

float RealBlendSPOMChannel_float(float4 value, float channel)
{
    int c = clamp((int)round(channel), 0, 3);
    return c == 0 ? value.r : (c == 1 ? value.g : (c == 2 ? value.b : value.a));
}

float RealBlendSPOMHeight_float(UnityTexture2D heightmap, UnitySamplerState heightmapSampler, float2 uv, float lod, float channel)
{
    return RealBlendSPOMChannel_float(heightmap.SampleLevel(heightmapSampler, uv, lod), channel);
}

float RealBlendSPOMHash12_float(float2 value)
{
    float3 p = frac(float3(value.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float RealBlendSPOMBoxInsideAA_float(float2 value, float2 minValue, float2 maxValue, float feather)
{
    float2 outside = max(minValue - value, value - maxValue);
    float signedDistance = -max(outside.x, outside.y);
    feather = max(feather, 0.000001);
    return smoothstep(-feather, feather, signedDistance);
}

float2 RealBlendSPOMTrace_float(
    UnityTexture2D heightmap,
    UnitySamplerState heightmapSampler,
    float2 baseUV,
    float lod,
    float steps,
    float3 viewDirUV,
    float stepSizeViewIndependence,
    float heightChannel,
    out float outHeight)
{
    int requestedSteps = clamp((int)round(steps), 1, 256);
    float ndotv = saturate(abs(viewDirUV.z));
    float grazingFactor = saturate(1.0 - ndotv);
    float viewDependentScale = max(0.25, grazingFactor);
    int stepCount = max(1, (int)ceil(requestedSteps * lerp(1.0, viewDependentScale, saturate(stepSizeViewIndependence))));
    float stepSize = rcp((float)stepCount);

    float safeZ = abs(viewDirUV.z) < 0.0001 ? (viewDirUV.z < 0.0 ? -0.0001 : 0.0001) : viewDirUV.z;
    float2 parallaxMaxOffset = viewDirUV.xy / -safeZ;
    float2 texOffsetPerStep = stepSize * parallaxMaxOffset;

    float2 texOffsetCurrent = float2(0.0, 0.0);
    float previousHeight = RealBlendSPOMHeight_float(heightmap, heightmapSampler, baseUV, lod, heightChannel);
    texOffsetCurrent += texOffsetPerStep;

    float currentHeight = RealBlendSPOMHeight_float(heightmap, heightmapSampler, baseUV + texOffsetCurrent, lod, heightChannel);
    float rayHeight = 1.0 - stepSize;

    [loop]
    for (int stepIndex = 0; stepIndex < 256; ++stepIndex)
    {
        if (stepIndex >= stepCount || currentHeight > rayHeight)
            break;

        previousHeight = currentHeight;
        rayHeight -= stepSize;
        texOffsetCurrent += texOffsetPerStep;
        currentHeight = RealBlendSPOMHeight_float(heightmap, heightmapSampler, baseUV + texOffsetCurrent, lod, heightChannel);
    }

    float pt0 = rayHeight + stepSize;
    float pt1 = rayHeight;
    float delta0 = pt0 - previousHeight;
    float delta1 = pt1 - currentHeight;
    float denom = delta1 - delta0;
    float intersectionHeight = abs(denom) > 0.00001 ? (pt0 * delta1 - pt1 * delta0) / denom : pt1;
    intersectionHeight = saturate(intersectionHeight);
    float2 offset = (1.0 - intersectionHeight) * texOffsetPerStep * stepCount;

    [unroll]
    for (int refineIndex = 0; refineIndex < 2; ++refineIndex)
    {
        currentHeight = RealBlendSPOMHeight_float(heightmap, heightmapSampler, baseUV + offset, lod, heightChannel);
        float delta = intersectionHeight - currentHeight;

        if (abs(delta) <= 0.01)
            break;

        if (delta < 0.0)
        {
            delta1 = delta;
            pt1 = intersectionHeight;
        }
        else
        {
            delta0 = delta;
            pt0 = intersectionHeight;
        }

        denom = delta1 - delta0;
        intersectionHeight = abs(denom) > 0.00001 ? (pt0 * delta1 - pt1 * delta0) / denom : intersectionHeight;
        intersectionHeight = saturate(intersectionHeight);
        offset = (1.0 - intersectionHeight) * texOffsetPerStep * stepCount;
    }

    outHeight = currentHeight;
    return offset;
}

float RealBlendSPOMSelfOcclusion_float(
    UnityTexture2D heightmap,
    UnitySamplerState heightmapSampler,
    float2 hitUV,
    float lod,
    float3 viewDirUV,
    float2 parallaxOffset,
    float hitHeight,
    float strength,
    float steps,
    float heightChannel)
{
    if (strength <= 0.0001 || steps < 1.0)
        return 1.0;

    int stepCount = clamp((int)round(steps), 1, 32);
    float2 texel = max(heightmap.texelSize.xy, float2(0.00001, 0.00001));
    float2 viewDirection = viewDirUV.xy;
    float viewDirectionLength = length(viewDirection);
    float horizonOcclusion = 0.0;

    if (viewDirectionLength > 0.0001)
    {
        float2 rayDirection = viewDirection / viewDirectionLength;
        float rayDistance = max(length(parallaxOffset), max(texel.x, texel.y) * 2.0);
        float2 rayStep = rayDirection * rayDistance / (float)stepCount;

        [loop]
        for (int stepIndex = 1; stepIndex <= 32; ++stepIndex)
        {
            if (stepIndex > stepCount)
                break;

            float travel = (float)stepIndex / (float)stepCount;
            float sampleHeight = RealBlendSPOMHeight_float(heightmap, heightmapSampler, hitUV + rayStep * stepIndex, lod, heightChannel);
            float heightBias = travel * 0.035;
            horizonOcclusion = max(horizonOcclusion, sampleHeight - hitHeight - heightBias);
        }
    }

    float neighborHeight = hitHeight;
    neighborHeight = max(neighborHeight, RealBlendSPOMHeight_float(heightmap, heightmapSampler, hitUV + float2(texel.x, 0.0), lod, heightChannel));
    neighborHeight = max(neighborHeight, RealBlendSPOMHeight_float(heightmap, heightmapSampler, hitUV - float2(texel.x, 0.0), lod, heightChannel));
    neighborHeight = max(neighborHeight, RealBlendSPOMHeight_float(heightmap, heightmapSampler, hitUV + float2(0.0, texel.y), lod, heightChannel));
    neighborHeight = max(neighborHeight, RealBlendSPOMHeight_float(heightmap, heightmapSampler, hitUV - float2(0.0, texel.y), lod, heightChannel));

    float cavityOcclusion = max(0.0, neighborHeight - hitHeight);
    float occlusion = saturate(max(horizonOcclusion * 6.0, cavityOcclusion * 4.0));
    return lerp(1.0, 1.0 - occlusion, saturate(strength));
}

void RealBlendSPOM_float(
    UnityTexture2D Heightmap,
    UnitySamplerState HeightmapSampler,
    float Amplitude,
    float Steps,
    float2 UVs,
    float2 Tiling,
    float2 Offset,
    float2 PrimitiveSize,
    float LOD,
    float LODThreshold,
    float3 ViewDirTS,
    float StepSizeViewIndependence,
    float SilhouetteClip,
    float HeightChannel,
    float SelfOcclusionStrength,
    float SelfOcclusionSteps,
    float LayerWeight,
    float2 EdgeUVs,
    out float PixelDepthOffset,
    out float2 ParallaxUVs,
    out float SelfOcclusion)
{
    float layerWeight = saturate(LayerWeight);
    float2 tiledUV = UVs * Tiling + Offset;
    float2 baseUV = Heightmap.GetTransformedUV(tiledUV);

    PixelDepthOffset = 0.0;
    ParallaxUVs = baseUV;
    SelfOcclusion = 1.0;

    if (Amplitude <= 0.0 || Steps < 1.0)
        return;

    float3 viewDirTS = ViewDirTS * RealBlendSPOMObjectScale_float().xzy;
    float ndotv = viewDirTS.z;

    float maxHeight = Amplitude * 0.01;
    maxHeight *= 2.0 / max(abs(Tiling.x) + abs(Tiling.y), 0.0001);

    float2 primitiveSize = max(abs(PrimitiveSize), float2(0.0001, 0.0001));
    float2 uvSpaceScale = maxHeight * Tiling / primitiveSize;
    float3 viewDirUV = normalize(float3(viewDirTS.xy * uvSpaceScale, viewDirTS.z));

    float outHeight = 1.0;
    float2 parallaxOffset = RealBlendSPOMTrace_float(
        Heightmap,
        HeightmapSampler,
        baseUV,
        LOD,
        Steps,
        viewDirUV,
        StepSizeViewIndependence,
        HeightChannel,
        outHeight);

    float lodFade = 1.0 - saturate(LOD - LODThreshold);
    parallaxOffset *= lodFade;
    ParallaxUVs = baseUV + parallaxOffset;
    PixelDepthOffset = ((maxHeight - outHeight * maxHeight) / max(abs(ndotv), 0.0001)) * layerWeight;
    float rawSelfOcclusion = RealBlendSPOMSelfOcclusion_float(
        Heightmap,
        HeightmapSampler,
        ParallaxUVs,
        LOD,
        viewDirUV,
        parallaxOffset,
        outHeight,
        SelfOcclusionStrength * lodFade,
        SelfOcclusionSteps,
        HeightChannel);
    SelfOcclusion = lerp(1.0, rawSelfOcclusion, layerWeight);

    float weightedSilhouetteClip = saturate(SilhouetteClip) * layerWeight;
    float hasClipTuning = step(0.00001, _SPOM_Clip_Softness + _SPOM_Clip_Grazing_Fade + _SPOM_Clip_Dither);
    float clipSoftness = lerp(0.35, max(_SPOM_Clip_Softness, 0.0), hasClipTuning);
    float grazingFade = lerp(0.25, max(_SPOM_Clip_Grazing_Fade, 0.0), hasClipTuning);
    float clipDither = lerp(0.06, max(_SPOM_Clip_Dither, 0.0), hasClipTuning);
    float viewFacing = saturate(abs(normalize(viewDirTS).z));
    float grazingStability = smoothstep(0.025, 0.025 + grazingFade, viewFacing);
    weightedSilhouetteClip *= lerp(1.0, grazingStability, 0.65);

    if (weightedSilhouetteClip > 0.001)
    {
        float2 rawScale = Tiling * Heightmap.scaleTranslate.xy;
        rawScale.x = abs(rawScale.x) < 0.0001 ? (rawScale.x < 0.0 ? -0.0001 : 0.0001) : rawScale.x;
        rawScale.y = abs(rawScale.y) < 0.0001 ? (rawScale.y < 0.0 ? -0.0001 : 0.0001) : rawScale.y;

        float2 rawClipOffset = parallaxOffset / rawScale;
        float2 textureScale = max(abs(Heightmap.scaleTranslate.xy), float2(0.0001, 0.0001));
        float2 maxPhysicalOffset = (maxHeight / primitiveSize) / textureScale;
        float maxClipReach = max(max(maxPhysicalOffset.x, maxPhysicalOffset.y) * 6.0, max(Heightmap.texelSize.x, Heightmap.texelSize.y) * 4.0);
        maxClipReach = max(maxClipReach, 0.001);

        float rawClipLength = length(rawClipOffset);
        rawClipOffset *= rawClipLength > maxClipReach ? maxClipReach / rawClipLength : 1.0;

        float2 rawDisplacedUV = UVs + rawClipOffset;
        float2 clipMin = min(_SPOM_Clip_Bounds.xy, _SPOM_Clip_Bounds.zw);
        float2 clipMax = max(_SPOM_Clip_Bounds.xy, _SPOM_Clip_Bounds.zw);

        if (clipMax.x - clipMin.x <= 0.0001 || clipMax.y - clipMin.y <= 0.0001)
        {
            clipMin = float2(-500000.0, -500000.0);
            clipMax = float2(500000.0, 500000.0);
        }

        float baseInside =
            step(clipMin.x, UVs.x) * step(UVs.x, clipMax.x) *
            step(clipMin.y, UVs.y) * step(UVs.y, clipMax.y);

        float rawUvFeather = max(max(fwidth(rawDisplacedUV.x), fwidth(rawDisplacedUV.y)) * (1.0 + clipSoftness * 6.0), maxClipReach * clipSoftness * 0.2);
        float displacedInside = RealBlendSPOMBoxInsideAA_float(rawDisplacedUV, clipMin, clipMax, rawUvFeather);

        float edgeDistance = min(
            min(UVs.x - clipMin.x, clipMax.x - UVs.x),
            min(UVs.y - clipMin.y, clipMax.y - UVs.y));
        float edgeWeight = 1.0 - smoothstep(maxClipReach, maxClipReach * 1.75, edgeDistance);
        float globalClipMask = lerp(1.0, displacedInside, weightedSilhouetteClip * baseInside * edgeWeight);

        float disableEdgeClip = step(0.5, max(-EdgeUVs.x, -EdgeUVs.y));
        float2 edgeDx = ddx(EdgeUVs);
        float2 edgeDy = ddy(EdgeUVs);
        float useLocalEdge = step(0.0000001, dot(abs(edgeDx) + abs(edgeDy), float2(1.0, 1.0)));

        float2 uvDx = ddx(UVs);
        float2 uvDy = ddy(UVs);
        float uvDet = uvDx.x * uvDy.y - uvDx.y * uvDy.x;
        float2 localClipOffset = float2(0.0, 0.0);

        if (abs(uvDet) > 0.00000001)
        {
            float2 screenDelta = float2(
                (rawClipOffset.x * uvDy.y - rawClipOffset.y * uvDy.x) / uvDet,
                (uvDx.x * rawClipOffset.y - uvDx.y * rawClipOffset.x) / uvDet);
            localClipOffset = screenDelta.x * edgeDx + screenDelta.y * edgeDy;
        }
        else if (rawClipLength > 0.000001)
        {
            localClipOffset = normalize(rawClipOffset) * min(rawClipLength, 0.05);
        }

        float localClipLength = length(localClipOffset);
        float localMaxReach = max(localClipLength, 0.0005);
        float2 localDisplacedUV = EdgeUVs + localClipOffset;

        float localBaseInside =
            step(0.0, EdgeUVs.x) * step(EdgeUVs.x, 1.0) *
            step(0.0, EdgeUVs.y) * step(EdgeUVs.y, 1.0);

        float localUvFeather = max(max(fwidth(localDisplacedUV.x), fwidth(localDisplacedUV.y)) * (1.0 + clipSoftness * 8.0), localMaxReach * clipSoftness * 0.35);
        float localDisplacedInside = RealBlendSPOMBoxInsideAA_float(localDisplacedUV, float2(0.0, 0.0), float2(1.0, 1.0), localUvFeather);

        float safeLocalClipLength = max(localClipLength, 0.000001);
        float2 localClipDirection = localClipOffset / safeLocalClipLength;
        float leftEdgeWeight = (1.0 - smoothstep(localMaxReach, localMaxReach * 1.75, EdgeUVs.x)) * step(0.001, -localClipDirection.x);
        float rightEdgeWeight = (1.0 - smoothstep(localMaxReach, localMaxReach * 1.75, 1.0 - EdgeUVs.x)) * step(0.001, localClipDirection.x);
        float bottomEdgeWeight = (1.0 - smoothstep(localMaxReach, localMaxReach * 1.75, EdgeUVs.y)) * step(0.001, -localClipDirection.y);
        float topEdgeWeight = (1.0 - smoothstep(localMaxReach, localMaxReach * 1.75, 1.0 - EdgeUVs.y)) * step(0.001, localClipDirection.y);
        float localEdgeWeight = max(max(leftEdgeWeight, rightEdgeWeight), max(bottomEdgeWeight, topEdgeWeight));
        float localClipMask = lerp(1.0, localDisplacedInside, weightedSilhouetteClip * localBaseInside * localEdgeWeight);

        float reliefReach = clamp(max(localMaxReach * 3.0, 0.015), 0.001, 0.18);
        float reliefLeftWeight = (1.0 - smoothstep(reliefReach, reliefReach * 1.35, EdgeUVs.x)) * step(0.001, -localClipDirection.x);
        float reliefRightWeight = (1.0 - smoothstep(reliefReach, reliefReach * 1.35, 1.0 - EdgeUVs.x)) * step(0.001, localClipDirection.x);
        float reliefBottomWeight = (1.0 - smoothstep(reliefReach, reliefReach * 1.35, EdgeUVs.y)) * step(0.001, -localClipDirection.y);
        float reliefTopWeight = (1.0 - smoothstep(reliefReach, reliefReach * 1.35, 1.0 - EdgeUVs.y)) * step(0.001, localClipDirection.y);
        float reliefEdgeWeight = max(max(reliefLeftWeight, reliefRightWeight), max(reliefBottomWeight, reliefTopWeight));
        float localEdgeDistance = min(
            min(EdgeUVs.x, 1.0 - EdgeUVs.x),
            min(EdgeUVs.y, 1.0 - EdgeUVs.y));
        float reliefThresholdAtEdge = lerp(0.30, 0.82, weightedSilhouetteClip);
        float reliefThreshold = reliefThresholdAtEdge * saturate(1.0 - localEdgeDistance / reliefReach);
        float heightFeather = max(fwidth(outHeight) * (1.0 + clipSoftness * 8.0), clipSoftness * 0.018);
        float reliefKeep = smoothstep(reliefThreshold - heightFeather, reliefThreshold + heightFeather, saturate(outHeight));
        float reliefClipMask = lerp(1.0, reliefKeep, weightedSilhouetteClip * localBaseInside * reliefEdgeWeight);
        localClipMask = min(localClipMask, reliefClipMask);

        float clipMask = lerp(globalClipMask, localClipMask, useLocalEdge);
        clipMask = lerp(clipMask, 1.0, disableEdgeClip);
        float ditherSeed = RealBlendSPOMHash12_float((UVs + ParallaxUVs) * 4096.0);
        float clipThreshold = 0.5 + (ditherSeed - 0.5) * saturate(clipDither) * 0.35;
        clip(clipMask - clipThreshold);
    }
}

void RealBlendSPOMPaintWeights_float(
    float4 VertexColorMasks,
    float L1Opacity,
    float L2Opacity,
    out float BaseWeight,
    out float L1Weight,
    out float L2Weight)
{
    float l1Raw = saturate(VertexColorMasks.g * L1Opacity);
    float l2Raw = saturate(VertexColorMasks.b * L2Opacity);

    L2Weight = l2Raw;
    L1Weight = l1Raw * (1.0 - L2Weight);
    BaseWeight = saturate((1.0 - l1Raw) * (1.0 - L2Weight));
}

void RealBlendSPOMPaintWeights_half(
    half4 VertexColorMasks,
    half L1Opacity,
    half L2Opacity,
    out half BaseWeight,
    out half L1Weight,
    out half L2Weight)
{
    float baseWeight;
    float l1Weight;
    float l2Weight;

    RealBlendSPOMPaintWeights_float(
        VertexColorMasks,
        L1Opacity,
        L2Opacity,
        baseWeight,
        l1Weight,
        l2Weight);

    BaseWeight = (half)baseWeight;
    L1Weight = (half)l1Weight;
    L2Weight = (half)l2Weight;
}

void RealBlendSPOMDepthBlend_float(
    float BaseDepth,
    float L1Depth,
    float L2Depth,
    out float DepthOffset)
{
    DepthOffset = BaseDepth + L1Depth + L2Depth;
}

void RealBlendSPOMDepthBlend_half(
    half BaseDepth,
    half L1Depth,
    half L2Depth,
    out half DepthOffset)
{
    DepthOffset = BaseDepth + L1Depth + L2Depth;
}

void RealBlendSPOM_half(
    UnityTexture2D Heightmap,
    UnitySamplerState HeightmapSampler,
    half Amplitude,
    half Steps,
    half2 UVs,
    half2 Tiling,
    half2 Offset,
    half2 PrimitiveSize,
    half LOD,
    half LODThreshold,
    half3 ViewDirTS,
    half StepSizeViewIndependence,
    half SilhouetteClip,
    half HeightChannel,
    half SelfOcclusionStrength,
    half SelfOcclusionSteps,
    half LayerWeight,
    half2 EdgeUVs,
    out half PixelDepthOffset,
    out half2 ParallaxUVs,
    out half SelfOcclusion)
{
    float pixelDepthOffset;
    float2 parallaxUVs;
    float selfOcclusion;

    RealBlendSPOM_float(
        Heightmap,
        HeightmapSampler,
        Amplitude,
        Steps,
        UVs,
        Tiling,
        Offset,
        PrimitiveSize,
        LOD,
        LODThreshold,
        ViewDirTS,
        StepSizeViewIndependence,
        SilhouetteClip,
        HeightChannel,
        SelfOcclusionStrength,
        SelfOcclusionSteps,
        LayerWeight,
        EdgeUVs,
        pixelDepthOffset,
        parallaxUVs,
        selfOcclusion);

    PixelDepthOffset = (half)pixelDepthOffset;
    ParallaxUVs = (half2)parallaxUVs;
    SelfOcclusion = (half)selfOcclusion;
}

#endif
