// Shared meadow light for the DQ Main floor and the actors that stand on it.
// One definition so the floor, sprites and shadows read the same cloud shade.
#ifndef DQ_MEADOW_INCLUDED
#define DQ_MEADOW_INCLUDED

float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float Noise(float2 p)
{
    float2 a=floor(p), b=frac(p); b=b*b*(3-2*b);
    return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);
}

// Slow cloud shade amount at a floor position. meadowLight.x = scale (1/m), .y = depth.
float DQCloudShade(float2 xz, float4 meadowLight)
{
    float cloud=Noise(xz*meadowLight.x+float2(31,7))*.65+Noise(xz*meadowLight.x*2.3+3)*.35;
    return smoothstep(.34,.68,cloud)*meadowLight.y;
}

// Colour multiplier for a cloud shade amount: darker and slightly cooler.
half3 DQCloudTint(float shade)
{
    return lerp(1,half3(.80,.92,1.06),shade)*(1-shade*.62);
}

#endif
