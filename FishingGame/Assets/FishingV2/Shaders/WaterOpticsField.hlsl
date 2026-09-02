#ifndef FISHING_V2_WATER_OPTICS_FIELD_INCLUDED
#define FISHING_V2_WATER_OPTICS_FIELD_INCLUDED

// Shared low-frequency surface field used by the final water composite and the fish-light
// response. The field is deliberately height-based: refraction comes from a finite-difference
// slope, while floor-light concentration comes from a soft curvature estimate.
float WaterOpticsHash21(float2 p)
{
    p = frac(p * float2(127.1, 311.7));
    p += dot(p, p + 41.23);
    return frac(p.x * p.y);
}

float WaterOpticsValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    local = local * local * (3.0 - 2.0 * local);
    float a = WaterOpticsHash21(cell);
    float b = WaterOpticsHash21(cell + float2(1.0, 0.0));
    float c = WaterOpticsHash21(cell + float2(0.0, 1.0));
    float d = WaterOpticsHash21(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
}

float WaterOpticsSurfaceHeight(float2 uv, float time, float scale, float speed, float phase)
{
    // Use continuous directional components for the surface itself. The existing organic
    // substrate can keep its material noise, but a cell-based noise gradient here would expose
    // grid-shaped blocks when refraction is increased above the calm default.
    float2 p = (uv - 0.5) * float2(4.70, 2.65) * max(scale, 0.05);
    float t = time * speed;
    float waveA = sin(dot(p, float2(0.86, 0.51)) + t * 0.92 + phase);
    float waveB = sin(dot(p, float2(-0.42, 0.91)) - t * 0.68 + phase * 1.37);
    float waveC = sin(dot(p, float2(0.63, -0.78)) + t * 0.43 - phase * 0.71);
    float waveD = sin(dot(p, float2(-0.77, -0.34)) - t * 0.31 + phase * 0.52);
    return saturate(0.5 + waveA * 0.20 + waveB * 0.13 + waveC * 0.08 + waveD * 0.05);
}

float WaterOpticsHeight(float2 uv, float time, float scale, float speed)
{
    // Two distinct scales and speeds keep the surface from behaving like one translating
    // scalar noise field while remaining slow enough for a calm observation scene.
    float fieldA = WaterOpticsSurfaceHeight(uv, time, max(scale * 0.72, 0.05), speed * 0.56, 0.70);
    float fieldB = WaterOpticsSurfaceHeight(uv, time, max(scale * 1.58, 0.05), speed * 1.28, -2.40);
    return saturate(fieldA * 0.64 + fieldB * 0.36);
}

float2 WaterOpticsGradient(float2 uv, float time, float scale, float speed)
{
    float epsilon = 0.0065;
    float left = WaterOpticsHeight(saturate(uv - float2(epsilon, 0.0)), time, scale, speed);
    float right = WaterOpticsHeight(saturate(uv + float2(epsilon, 0.0)), time, scale, speed);
    float down = WaterOpticsHeight(saturate(uv - float2(0.0, epsilon)), time, scale, speed);
    float up = WaterOpticsHeight(saturate(uv + float2(0.0, epsilon)), time, scale, speed);
    return float2((right - left) / (epsilon * 2.0), (up - down) / (epsilon * 2.0));
}

// Surface-readability response derived from the same low-frequency slope that drives
// refraction. This is a lighting response only: it never changes an object's transform or
// adds another UV displacement. The two broad lobes keep the result readable as a quiet
// above-water reflection/highlight instead of a full-screen noise layer.
float WaterOpticsSurfaceHighlight(float2 uv, float time, float scale, float speed)
{
    float2 gradient = clamp(WaterOpticsGradient(uv, time, scale, speed), -2.5, 2.5);
    float3 normal = normalize(float3(-gradient.x * 0.34, -gradient.y * 0.34, 1.0));
    float3 keyDirection = normalize(float3(-0.42, 0.58, 0.70));
    float3 fillDirection = normalize(float3(0.48, -0.34, 0.80));
    float key = saturate(dot(normal, keyDirection));
    float fill = saturate(dot(normal, fillDirection));
    float keyBand = smoothstep(0.64, 0.86, key);
    float keyLobe = pow(key, 6.0);
    float fillBand = smoothstep(0.70, 0.94, fill);
    float height = WaterOpticsHeight(uv, time, scale, speed);
    return saturate(keyBand * 0.50 + keyLobe * 0.24 + fillBand * 0.18 + height * 0.08);
}

float WaterOpticsCurvature(float2 uv, float time, float scale, float speed)
{
    float epsilon = 0.0105;
    float center = WaterOpticsHeight(uv, time, scale, speed);
    float left = WaterOpticsHeight(saturate(uv - float2(epsilon, 0.0)), time, scale, speed);
    float right = WaterOpticsHeight(saturate(uv + float2(epsilon, 0.0)), time, scale, speed);
    float down = WaterOpticsHeight(saturate(uv - float2(0.0, epsilon)), time, scale, speed);
    float up = WaterOpticsHeight(saturate(uv + float2(0.0, epsilon)), time, scale, speed);
    float laplacian = (left + right + down + up - center * 4.0) / (epsilon * epsilon);
    return saturate(0.5 - laplacian * 0.018);
}

float2 WaterOpticsRefraction(float2 uv, float time, float scale, float speed, float strength)
{
    float2 gradient = WaterOpticsGradient(uv, time, scale, speed);
    gradient = clamp(gradient, -2.5, 2.5);
    // At the current 1024x517 capture size this starts around a pixel of slow motion, with
    // enough headroom for the inspector strength to remain useful without full-screen wobble.
    return -gradient * (0.0035 * saturate(strength));
}

float WaterOpticsLight(float2 uv, float time, float scale, float speed)
{
    float height = WaterOpticsHeight(uv, time, scale, speed);
    float curvature = WaterOpticsCurvature(uv, time, scale, speed);
    float slope = saturate(length(WaterOpticsGradient(uv, time, scale, speed)) * 0.10);
    return saturate(0.5 + (curvature - 0.5) * 1.25 + (height - 0.5) * 0.12 + slope * 0.06);
}

#endif
