#include "QuadRenderer.h"
#include "Logger.h"
#include <vector>
#include <fstream>

QuadRenderer::QuadRenderer()
    : _vertexShader(nullptr)
    , _pixelShader(nullptr)
    , _samplerState(nullptr)
{
}

QuadRenderer::~QuadRenderer()
{
    Shutdown();
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LoadShader
// 
// Loads compiled shader bytecode from a .cso file.
// Returns false if the file cannot be opened or read.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
bool QuadRenderer::LoadShader(const std::string& path, std::vector<char>& bytecode)
{
    std::ifstream file(path, std::ios::binary | std::ios::ate);
    if (!file.is_open())
    {
        Logger::Instance().Write("QuadRenderer: failed to open shader: %s", path.c_str());
        return false;
    }

    size_t size = (size_t)file.tellg();
    file.seekg(0);
    bytecode.resize(size);
    file.read(bytecode.data(), size);

    Logger::Instance().Write("QuadRenderer: loaded shader: %s (%zu bytes)", path.c_str(), size);
    return true;
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Initialize
// 
// Creates the shaders.
// Creates the sampler state.
// Returns false if any step fails.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
bool QuadRenderer::Initialize(ID3D11Device* device)
{
    std::vector<char> vsBytecode;
    if (!LoadShader("QuadVS.cso", vsBytecode))
    {
        return false;
    }

    HRESULT hr = device->CreateVertexShader(vsBytecode.data(), vsBytecode.size(), nullptr, &_vertexShader);
    if (FAILED(hr))
    {
        Logger::Instance().Write("QuadRenderer: CreateVertexShader failed: 0x%08X", hr);
        return false;
    }

    Logger::Instance().Write("QuadRenderer: vertex shader created. vs=%p", _vertexShader);

    std::vector<char> psBytecode;
    if (!LoadShader("QuadPS.cso", psBytecode))
    {
        return false;
    }

    hr = device->CreatePixelShader(psBytecode.data(), psBytecode.size(), nullptr, &_pixelShader);
    if (FAILED(hr))
    {
        Logger::Instance().Write("QuadRenderer: CreatePixelShader failed: 0x%08X", hr);
        return false;
    }

    Logger::Instance().Write("QuadRenderer: pixel shader created. ps=%p", _pixelShader);

    D3D11_SAMPLER_DESC samplerDesc = {};
    samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
    samplerDesc.MinLOD = 0;
    samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

    hr = device->CreateSamplerState(&samplerDesc, &_samplerState);
    if (FAILED(hr))
    {
        Logger::Instance().Write("QuadRenderer: CreateSamplerState failed: 0x%08X", hr);
        return false;
    }
    Logger::Instance().Write("QuadRenderer: sampler state created. sampler=%p", _samplerState);

    D3D11_BUFFER_DESC cbd = {};
    cbd.ByteWidth = sizeof(QuadConstants);
    cbd.Usage = D3D11_USAGE_DYNAMIC;
    cbd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    cbd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

    hr = device->CreateBuffer(&cbd, nullptr, &_constantBuffer);
    if (FAILED(hr))
    {
        Logger::Instance().Write("QuadRenderer: CreateBuffer failed: 0x%08X", hr);
        return false;
    }
    Logger::Instance().Write("QuadRenderer: constant buffer created. cb=%p", _constantBuffer);

    return true;
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Shutdown
// 
// Releases all D3D11 resources.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void QuadRenderer::Shutdown()
{
    Logger::Instance().Write("QuadRenderer: shutting down.");

    if (_samplerState)
    {
        _samplerState->Release();
        _samplerState = nullptr;
    }
    if (_pixelShader)
    {
        _pixelShader->Release();
        _pixelShader = nullptr;
    }
    if (_vertexShader)
    {
        _vertexShader->Release();
        _vertexShader = nullptr;
    }
    if (_constantBuffer)
    {
        _constantBuffer->Release();
        _constantBuffer = nullptr;
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Render
//
// Renders the given SRV to the current viewport using a screen-space quad.
// The source UV rect defines which portion of the texture to sample.
//
// context:  The D3D11 device context
// srv:      The shader resource view to sample
// u0, v0:   Top-left UV coordinate of the source region
// u1, v1:   Bottom-right UV coordinate of the source region
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void QuadRenderer::Render(ID3D11DeviceContext* context,
    ID3D11ShaderResourceView* srv,
    float u0, float v0, float u1, float v1)
{
    if ((!_vertexShader) || (!_pixelShader) || (!_samplerState) || (!_constantBuffer))
    {
        Logger::Instance().Write("QuadRenderer::Render: not initialized, skipping.");
        return;
    }

    if (srv == nullptr)
    {
        Logger::Instance().Write("QuadRenderer::Render: srv is null, skipping.");
        return;
    }

    D3D11_MAPPED_SUBRESOURCE mapped = {};
    HRESULT hr = context->Map(_constantBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped);
    if (FAILED(hr))
    {
        Logger::Instance().Write("QuadRenderer::Render: Map failed: 0x%08X", hr);
        return;
    }

    QuadConstants* constants = (QuadConstants*)mapped.pData;
    constants->uvRect[0] = u0;
    constants->uvRect[1] = v0;
    constants->uvRect[2] = u1;
    constants->uvRect[3] = v1;
    context->Unmap(_constantBuffer, 0);

    context->VSSetConstantBuffers(0, 1, &_constantBuffer);
    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    context->IASetInputLayout(nullptr);
    context->VSSetShader(_vertexShader, nullptr, 0);
    context->PSSetShader(_pixelShader, nullptr, 0);
    context->PSSetShaderResources(0, 1, &srv);
    context->PSSetSamplers(0, 1, &_samplerState);
    context->Draw(4, 0);

    ID3D11ShaderResourceView* nullSrv = nullptr;
    context->PSSetShaderResources(0, 1, &nullSrv);
}