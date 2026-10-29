namespace CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering
{
    // Shader 源码集中维护，避免控制器同时承担 GPU 算法职责。
    /// <summary>OpenGL 3.3 Shader 源码；只描述 GPU 算法，不管理 OpenGL 对象生命周期。</summary>
    internal static class MPWVolumeShaderSources
    {
        internal const string VolumeVertex = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
out vec3 objectPos;
void main()
{
    objectPos = aPosition;
    gl_Position = vec4(aPosition, 1.0) * model * view * projection;
}";

        internal const string VolumeFragment = @"
#version 330 core
in vec3 objectPos;
uniform sampler3D volumeTex;
uniform sampler1D scalarTransferTex;
uniform sampler1D gradientOpacityTex;
uniform vec3 cameraObject;
uniform vec3 voxelStep;
uniform vec3 physicalScale;
uniform float rayStep;
uniform float opacityReferenceStep;
uniform float opacityScale;
uniform float materialAmbient;
uniform float materialDiffuse;
uniform float materialSpecular;
uniform float materialSpecularPower;
uniform int enableJitter;
out vec4 FragColor;

vec2 intersectBox(vec3 origin, vec3 dir)
{
    vec3 invDir = 1.0 / dir;
    vec3 t0 = (-0.5 - origin) * invDir;
    vec3 t1 = ( 0.5 - origin) * invDir;
    vec3 tMin = min(t0, t1);
    vec3 tMax = max(t0, t1);
    return vec2(max(max(tMin.x, tMin.y), tMin.z), min(min(tMax.x, tMax.y), tMax.z));
}

float screenHash(vec2 value)
{
    vec3 p = fract(vec3(value.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

void main()
{
    vec3 rayDir = normalize(objectPos - cameraObject);
    vec2 hit = intersectBox(cameraObject, rayDir);
    if (hit.x > hit.y) discard;

    float t = max(hit.x, 0.0);
    if (enableJitter != 0) t += screenHash(gl_FragCoord.xy) * rayStep;
    vec4 accum = vec4(0.0);

    for (int i = 0; i < 2048; i++)
    {
        if (t > hit.y || accum.a > 0.985) break;
        vec3 p = cameraObject + rayDir * t;
        vec3 texCoord = p + vec3(0.5);
        float density = texture(volumeTex, texCoord).r;
        vec4 transfer = texture(scalarTransferTex, density);
        if (transfer.a <= 0.000001)
        {
            t += rayStep;
            continue;
        }
        vec3 gx = vec3(voxelStep.x, 0.0, 0.0);
        vec3 gy = vec3(0.0, voxelStep.y, 0.0);
        vec3 gz = vec3(0.0, 0.0, voxelStep.z);
        vec3 gradient = vec3(
            texture(volumeTex, texCoord + gx).r - texture(volumeTex, texCoord - gx).r,
            texture(volumeTex, texCoord + gy).r - texture(volumeTex, texCoord - gy).r,
            texture(volumeTex, texCoord + gz).r - texture(volumeTex, texCoord - gz).r);
        float gradientValue = clamp(length(gradient) * 5.5, 0.0, 1.0);
        float gradientOpacity = texture(gradientOpacityTex, gradientValue).r;
        float baseAlpha = clamp(transfer.a * gradientOpacity * opacityScale, 0.0, 0.999);
        float alpha = 1.0 - pow(1.0 - baseAlpha, rayStep / max(opacityReferenceStep, 0.00001));

        if (alpha > 0.00001)
        {
            vec3 physicalGradient = gradient / max(physicalScale, vec3(0.0001));
            float gradientMagnitude = length(physicalGradient);
            vec3 viewDir = -rayDir;
            vec3 normal = gradientMagnitude > 0.00001 ? physicalGradient / gradientMagnitude : viewDir;
            if (dot(normal, viewDir) < 0.0) normal = -normal;
            vec3 lightDir = normalize(cameraObject - p);
            vec3 halfVector = normalize(lightDir + viewDir);
            float surfaceStrength = smoothstep(0.002, 0.08, gradientMagnitude);
            float diffuse = mix(0.45, max(dot(normal, lightDir), 0.0), surfaceStrength);
            float specular = surfaceStrength
                * pow(max(dot(normal, halfVector), 0.0), max(materialSpecularPower, 1.0));
            vec3 color = transfer.rgb * (materialAmbient + materialDiffuse * diffuse)
                + vec3(materialSpecular * specular);
            accum.rgb += (1.0 - accum.a) * alpha * color;
            accum.a += (1.0 - accum.a) * alpha;
        }
        t += rayStep;
    }
    FragColor = accum;
}";

        internal const string SliceVertex = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aTexCoord;
layout (location = 2) in vec3 aPlaneColor;
layout (location = 3) in vec2 aObliqueUv;
uniform mat4 mvp;
out vec3 texCoord;
out vec3 planeColor;
out vec2 obliqueUv;
void main()
{
    texCoord = aTexCoord;
    planeColor = aPlaneColor;
    obliqueUv = aObliqueUv;
    gl_Position = vec4(aPosition, 1.0) * mvp;
}";

        internal const string SliceFragment = @"
#version 330 core
in vec3 texCoord;
in vec3 planeColor;
in vec2 obliqueUv;
uniform sampler3D volumeTex;
uniform sampler2D obliqueTex;
uniform float sliceOpacity;
uniform int showSliceTexture;
uniform int showSlicePlanes;
uniform int useObliqueTexture;
out vec4 FragColor;
void main()
{
    bool outside = texCoord.x < 0.0 || texCoord.x > 1.0 || texCoord.y < 0.0 ||
        texCoord.y > 1.0 || texCoord.z < 0.0 || texCoord.z > 1.0;
    if (outside && showSlicePlanes == 0) discard;
    float density = outside ? 0.0 : texture(volumeTex, texCoord).r;
    if (useObliqueTexture != 0)
    {
        density = texture(obliqueTex, obliqueUv).r;
        outside = false;
    }
    vec3 color = planeColor;
    if (!outside && showSliceTexture != 0)
        color = showSlicePlanes != 0 ? planeColor * (0.18 + density * 0.82) : vec3(density);
    FragColor = vec4(color, outside ? sliceOpacity * 0.35 : sliceOpacity);
}";

        internal const string LineVertex = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
uniform mat4 mvp;
void main() { gl_Position = vec4(aPosition, 1.0) * mvp; }";

        internal const string LineFragment = @"
#version 330 core
uniform vec3 lineColor;
uniform float overlayOpacity;
out vec4 FragColor;
void main() { FragColor = vec4(lineColor, overlayOpacity); }";
    }
}
