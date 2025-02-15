Shader "Hidden/FX/CameraCompositor"
{
    Properties
    {
        _MainTex("Main Texture", 2DArray) = "grey" {}
        _TextureA("Texture A", 2D) = "white" {} 
        _TextureB("Texture B", 2D) = "white" {}
        _TextureKey("Texture Key", 2D) = "white" {} 
        _TextureMask("Texture Mask", 2D) = "white" {} 
        _Brightness("Brightness", Float) = 1.0
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_MainTex);

    TEXTURE2D(_TextureA); 
    SAMPLER(sampler_TextureA); 

    TEXTURE2D(_TextureB); 
    SAMPLER(sampler_TextureB); 

    TEXTURE2D(_TextureKey); 
    SAMPLER(sampler_TextureKey); 

    TEXTURE2D(_TextureMask); 
    SAMPLER(sampler_TextureMask); 

    float _Brightness;

    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 colorA = SAMPLE_TEXTURE2D(_TextureA, sampler_TextureA, input.texcoord).xyz;
        float3 colorB = SAMPLE_TEXTURE2D(_TextureB, sampler_TextureB, input.texcoord).xyz;
        float3 colorKey = SAMPLE_TEXTURE2D(_TextureKey, sampler_TextureKey, input.texcoord).xyz;
        float3 maskColor = SAMPLE_TEXTURE2D(_TextureMask, sampler_TextureMask, input.texcoord).xyz;

        bool isKeyColorPink = (colorKey.r == 1.0) && (colorKey.g == 0.0) && (colorKey.b == 1.0);
        float3 outputColor = isKeyColorPink ? colorA : colorB;

        outputColor *= maskColor * _Brightness;

        return float4(outputColor, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "PP"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
