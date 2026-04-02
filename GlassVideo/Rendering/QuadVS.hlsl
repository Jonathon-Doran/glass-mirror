cbuffer QuadConstants : register(b0)
{
    float4 uvRect;  // x=u0, y=v0, z=u1, w=v1
};

struct VSOutput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

VSOutput main(uint vertexId : SV_VertexID)
{
    VSOutput output;
    float u = (vertexId & 1) ? 1.0 : 0.0;
    float v = (vertexId & 2) ? 1.0 : 0.0;
    output.texcoord = float2(
        u == 0.0 ? uvRect.x : uvRect.z,
        v == 0.0 ? uvRect.y : uvRect.w);
    output.position = float4(float2(u, v) * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}