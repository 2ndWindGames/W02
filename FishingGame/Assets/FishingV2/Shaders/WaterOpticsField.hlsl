#ifndef FISHING_V2_WATER_OPTICS_FIELD_INCLUDED
#define FISHING_V2_WATER_OPTICS_FIELD_INCLUDED

// Shared low-frequency surface field used by the final water composite and the fish-light
// response. The field is deliberately height-based: refraction comes from a finite-difference
// slope, while floor-light concentration comes from a soft curvature estimate.
//
// Every visible water response in this project - refraction, surface highlight, the visible
// ripple ridge, specular, the reflection hint, and transmitted floor light - is derived from
// this one height field. That is what keeps the surface above and the distorted scene below
// reading as a single water surface instead of several unrelated animated layers.
//
// TIME IS AN ACCUMULATED PHASE, NOT A CLOCK.
// These functions take `phase`, which the session integrates as `speed * dt`. They must never
// go back to multiplying an absolute clock by a per-profile speed. `_OpticalTime` is already
// thousands of seconds when a session starts, so lerping speed from 0.30 to 0.18 across a
// transition moved the phase by hundreds of radians - the surface did not slow down, it
// scrubbed. That is the whole reason the dive read as fast-forwarded video.
//
// The `shape` parameter is the presentation control. At shape == 0 the ripple layer is absent
// and the field is the two broad base octaves the approved gameplay water has always used.
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

// A sum of pure tones at fixed rates marches at a constant velocity, and the eye reads that
// immediately as a machine. Every animated term below therefore carries a bounded phase
// wander whose periods share no small common factor with each other or with the carrier, so
// crests speed up, stall and re-form instead of translating.
float WaterOpticsWander(float phase, float seed)
{
    return sin(phase * 0.2713 + seed) * 0.78
         + sin(phase * 0.1109 - seed * 1.63) * 1.02
         + sin(phase * 0.0487 + seed * 0.41) * 0.63;
}

float WaterOpticsSurfaceHeight(float2 uv, float phase, float scale, float rate, float offset)
{
    // Use continuous directional components for the surface itself. The existing organic
    // substrate can keep its material noise, but a cell-based noise gradient here would expose
    // grid-shaped blocks when refraction is increased above the calm default.
    float2 p = (uv - 0.5) * float2(4.70, 2.65) * max(scale, 0.05);
    float t = phase * rate;
    float wander = WaterOpticsWander(t, offset);
    float waveA = sin(dot(p, float2(0.86, 0.51)) + t * 0.92 + offset + wander * 0.42);
    float waveB = sin(dot(p, float2(-0.42, 0.91)) - t * 0.68 + offset * 1.37 - wander * 0.31);
    float waveC = sin(dot(p, float2(0.63, -0.78)) + t * 0.43 - offset * 0.71 + wander * 0.24);
    float waveD = sin(dot(p, float2(-0.77, -0.34)) - t * 0.31 + offset * 0.52 - wander * 0.18);
    return saturate(0.5 + waveA * 0.20 + waveB * 0.13 + waveC * 0.08 + waveD * 0.05);
}

// The ripple scale. The two base octaves above are extremely low frequency - well under two
// crests across the screen - which is why the approved gameplay water reads as a smooth
// gradient with no legible surface at all. This field is the one that carries crest spacing.
//
// It is deliberately broad: about two crests across the width. An earlier pass ran this three
// to four times finer, which did make the surface unmistakable but read as draped fabric
// rather than water. Clear water seen from above has almost no visible crest structure - the
// surface announces itself through reflected light and reduced clarity, not through ridges.
//
// Its amplitude is kept small on purpose. Frequency is what makes a ridge visible; amplitude
// is what makes the slope steep, and a steep high-frequency field turns refraction into the
// fast shimmer this water is not allowed to have.
float WaterOpticsSwell(float2 uv, float phase, float scale)
{
    float2 p = (uv - 0.5) * float2(11.5, 6.5) * max(scale, 0.05);
    float t = phase;

    // The wave vector swings slowly. A fixed direction is the single loudest tell that this
    // is generated: real ripples are pushed by wind that keeps changing its mind.
    float swing = sin(t * 0.1303) * 0.17 + sin(t * 0.0709 + 1.70) * 0.11;
    float2 travel = normalize(float2(-0.59 + swing, 0.81 - swing * 0.43));
    float2 across = float2(-travel.y, travel.x);

    float wander = WaterOpticsWander(t, 0.90);
    float warp = sin(dot(p, across) * 0.42 + t * 0.23 + wander * 0.30) * 1.15;
    float crest = sin(dot(p, travel) + t * 1.35 + warp + wander);
    // Second harmonic along the same direction: sharpens the crest and flattens the trough,
    // which is the asymmetry that separates a wave from a sine.
    float harmonic = sin(dot(p, travel) * 2.07 + t * 2.05 + warp * 1.4 + wander * 1.7);
    float cross = sin(dot(p, across) * 0.61 - t * 0.72 + 1.90 - wander * 0.5);

    // Gust mask. The surface has to be busy in some places and nearly flat in others, and the
    // busy places have to appear and fade on their own schedule. The periods below share no
    // small common factor - a patch mask built from two matched sines is itself perfectly
    // periodic, which is exactly the "it ripples too evenly" complaint.
    float patchA = sin(dot(p, float2(0.115, 0.078)) + t * 0.1301);
    float patchB = sin(dot(p, float2(-0.061, 0.132)) - t * 0.0907 + 1.30);
    float patchC = sin(dot(p, float2(0.043, -0.089)) + t * 0.0563 - 0.70);
    float gust = sin(t * 0.0834) * 0.50 + sin(t * 0.0491 + 2.40) * 0.34 + sin(t * 0.1613 - 1.10) * 0.16;
    float patch = 0.38 + 0.62 * saturate(0.5 + patchA * 0.28 + patchB * 0.22 + patchC * 0.17 + gust * 0.30);

    return (crest * 0.74 + harmonic * 0.14 + cross * 0.20) * patch;
}

float WaterOpticsRippleAmplitude(float shape)
{
    return 0.058 * max(shape, 0.0);
}

float WaterOpticsHeight(float2 uv, float phase, float scale, float shape)
{
    // Two distinct scales and rates keep the surface from behaving like one translating
    // scalar noise field while remaining slow enough for a calm observation scene.
    float fieldA = WaterOpticsSurfaceHeight(uv, phase, max(scale * 0.72, 0.05), 0.56, 0.70);
    float fieldB = WaterOpticsSurfaceHeight(uv, phase, max(scale * 1.58, 0.05), 1.28, -2.40);
    float height = fieldA * 0.64 + fieldB * 0.36;

    // A small additive amplitude, not a renormalized blend: the ripple has to sit on top of
    // the broad swell rather than replace it, and at this frequency even 0.05 of height is a
    // slope comparable to the whole base field.
    float amplitude = WaterOpticsRippleAmplitude(shape);
    if (amplitude > 0.0001)
    {
        height += WaterOpticsSwell(uv, phase, scale) * amplitude;
    }

    return saturate(height);
}

// The gradient clamp exists to stop a pathological slope from tearing the refraction. The
// ripple field legitimately produces steeper slopes than the two broad base octaves, so the
// ceiling has to rise with it - but it is exactly 2.5 at shape == 0, which is the approved
// gameplay value.
float WaterOpticsSlopeLimit(float shape)
{
    return 2.5 + 3.5 * max(shape, 0.0);
}

float2 WaterOpticsGradient(float2 uv, float phase, float scale, float shape)
{
    float epsilon = 0.0065;
    float left = WaterOpticsHeight(uv - float2(epsilon, 0.0), phase, scale, shape);
    float right = WaterOpticsHeight(uv + float2(epsilon, 0.0), phase, scale, shape);
    float down = WaterOpticsHeight(uv - float2(0.0, epsilon), phase, scale, shape);
    float up = WaterOpticsHeight(uv + float2(0.0, epsilon), phase, scale, shape);
    return float2((right - left) / (epsilon * 2.0), (up - down) / (epsilon * 2.0));
}

// The ripple layer on its own. The two base octaves are so low frequency that any response
// built on the combined normal is dominated by them - which is what turned an early
// presentation pass into drifting fog blobs instead of wave crests. The crest responses
// (ridge band, specular) therefore read the ripple in isolation, while refraction and the
// reflection hint keep using the full surface so nothing decouples optically.
float WaterOpticsRippleHeight(float2 uv, float phase, float scale, float shape)
{
    return WaterOpticsSwell(uv, phase, scale) * WaterOpticsRippleAmplitude(shape);
}

float2 WaterOpticsRippleGradient(float2 uv, float phase, float scale, float shape)
{
    float epsilon = 0.0045;
    float left = WaterOpticsRippleHeight(uv - float2(epsilon, 0.0), phase, scale, shape);
    float right = WaterOpticsRippleHeight(uv + float2(epsilon, 0.0), phase, scale, shape);
    float down = WaterOpticsRippleHeight(uv - float2(0.0, epsilon), phase, scale, shape);
    float up = WaterOpticsRippleHeight(uv + float2(0.0, epsilon), phase, scale, shape);
    return float2((right - left) / (epsilon * 2.0), (up - down) / (epsilon * 2.0));
}

// One shared normal. Highlight, refraction and the reflection hint all read this, so a crest
// that refracts the scene below is the same crest that catches the light above.
float3 WaterOpticsNormal(float2 uv, float phase, float scale, float shape)
{
    float limit = WaterOpticsSlopeLimit(shape);
    float2 gradient = clamp(WaterOpticsGradient(uv, phase, scale, shape), -limit, limit);
    return normalize(float3(-gradient.x * 0.34, -gradient.y * 0.34, 1.0));
}

// Surface-readability response derived from the same low-frequency slope that drives
// refraction. This is a lighting response only: it never changes an object's transform or
// adds another UV displacement. The two broad lobes keep the result readable as a quiet
// above-water reflection/highlight instead of a full-screen noise layer.
float WaterOpticsSurfaceHighlight(float2 uv, float phase, float scale, float shape)
{
    float3 normal = WaterOpticsNormal(uv, phase, scale, shape);
    float3 keyDirection = normalize(float3(-0.42, 0.58, 0.70));
    float3 fillDirection = normalize(float3(0.48, -0.34, 0.80));
    float key = saturate(dot(normal, keyDirection));
    float fill = saturate(dot(normal, fillDirection));
    float keyBand = smoothstep(0.64, 0.86, key);
    float keyLobe = pow(key, 6.0);
    float fillBand = smoothstep(0.70, 0.94, fill);
    float height = WaterOpticsHeight(uv, phase, scale, shape);
    return saturate(keyBand * 0.50 + keyLobe * 0.24 + fillBand * 0.18 + height * 0.08);
}

// Signed ridge response along the light azimuth. The sign is the whole point: a magnitude
// would give a symmetric wash that reads as noise, while a signed slope gives the lit flank
// and the shaded flank of the same crest, which is what makes the surface shape legible.
// Returns [-1, 1].
float WaterOpticsRidgeBand(float2 uv, float phase, float scale, float shape)
{
    float2 gradient = WaterOpticsRippleGradient(uv, phase, scale, shape);
    float2 lightAzimuth = normalize(float2(-0.52, 0.86));
    float slope = clamp(dot(gradient, lightAzimuth) * 1.35, -1.5, 1.5);
    // Shaped into a lit band and a shaded band with a neutral zone between them. The neutral
    // zone is the point: it is where the underwater scene comes through untouched, and it is
    // what makes a crest read as a line rather than as a full-screen brightness gradient.
    float lit = smoothstep(0.06, 0.42, slope);
    float shaded = smoothstep(0.06, 0.42, -slope);
    return lit - shaded;
}

// Stylized sheen. The view vector is +Z because the prototype camera looks straight down, and
// the exponents stay low enough that the lobe spreads across a whole crest. The tight lobe is
// clamped so a steep crest cannot blow out into a white highlight.
float WaterOpticsSpecular(float2 uv, float phase, float scale, float shape)
{
    // Crest-only normal, tilted harder than the refraction normal so a crest actually turns
    // far enough to catch the key. The base swell is left out on purpose - a sheen that
    // follows the broad field is just a bright cloud.
    float2 gradient = WaterOpticsRippleGradient(uv, phase, scale, shape);
    float3 normal = normalize(float3(-gradient.x * 0.90, -gradient.y * 0.90, 1.0));
    float3 viewDirection = float3(0.0, 0.0, 1.0);
    float3 keyDirection = normalize(float3(-0.42, 0.58, 0.70));
    float3 halfDirection = normalize(keyDirection + viewDirection);
    float sheen = saturate(dot(normal, halfDirection));
    float broad = pow(sheen, 14.0);
    float tight = pow(sheen, 46.0);
    return saturate(broad * 0.72 + min(tight, 0.55) * 0.62);
}

// Reflection hint. Looking straight down at flat water shows almost nothing; a crest flank
// tips the normal toward a grazing angle and starts to return sky. This is a stylized
// fresnel-like response, not a probe: the point is only to say that a boundary exists.
float WaterOpticsFresnel(float2 uv, float phase, float scale, float shape)
{
    float3 normal = WaterOpticsNormal(uv, phase, scale, shape);
    float grazing = 1.0 - saturate(normal.z);
    return saturate(pow(max(grazing, 0.0), 0.62) * 1.35);
}

float WaterOpticsCurvature(float2 uv, float phase, float scale, float shape)
{
    float epsilon = 0.0105;
    float center = WaterOpticsHeight(uv, phase, scale, shape);
    float left = WaterOpticsHeight(uv - float2(epsilon, 0.0), phase, scale, shape);
    float right = WaterOpticsHeight(uv + float2(epsilon, 0.0), phase, scale, shape);
    float down = WaterOpticsHeight(uv - float2(0.0, epsilon), phase, scale, shape);
    float up = WaterOpticsHeight(uv + float2(0.0, epsilon), phase, scale, shape);
    float laplacian = (left + right + down + up - center * 4.0) / (epsilon * epsilon);
    return saturate(0.5 - laplacian * 0.018);
}

float2 WaterOpticsRefraction(float2 uv, float phase, float scale, float strength, float coefficient, float shape)
{
    float limit = WaterOpticsSlopeLimit(shape);
    float2 gradient = clamp(WaterOpticsGradient(uv, phase, scale, shape), -limit, limit);
    // At the current 1024x474 capture size the gameplay coefficient (0.0035) starts around a
    // pixel of slow motion. The coefficient is a profile value so the presentation state can
    // push the same field to a visible undulation without a second displacement source.
    return -gradient * (max(coefficient, 0.0) * saturate(strength));
}

float WaterOpticsLight(float2 uv, float phase, float scale, float shape)
{
    float height = WaterOpticsHeight(uv, phase, scale, shape);
    float curvature = WaterOpticsCurvature(uv, phase, scale, shape);
    float slope = saturate(length(WaterOpticsGradient(uv, phase, scale, shape)) * 0.10);
    return saturate(0.5 + (curvature - 0.5) * 1.25 + (height - 0.5) * 0.12 + slope * 0.06);
}

#endif
