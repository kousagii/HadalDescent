Shader "HadalDescent/Hazard Underwater"
{
    Properties { _WaterState("Water state",Vector)=(0,0,0,0) _Submarine("Protected area",Vector)=(.5,.2,1.78,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "HazardWater"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _WaterState;
                float4 _Submarine;
            CBUFFER_END

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float depth = LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float farWater = saturate((depth-12.0)/35.0);
                float2 relative = (uv-_Submarine.xy)*float2(_Submarine.z,1);
                float protection = smoothstep(.085,.23,length(relative));
                float sediment = saturate(_WaterState.x);
                float time = _WaterState.z;
                float wisp = .75 + .25*sin(uv.x*15.0 + sin(uv.y*10.0-time*.4)*2.0 + time*.7);
                float affected = sediment * protection * wisp;
                // Keep nearby silhouettes and the submarine sharp. Only the 3D scene is processed.
                float localBlur = affected * (.40 + .60*farWater);
                float2 pixel = _BlitTexture_TexelSize.xy;
                float2 ripple = float2(sin(uv.y*32.0+time*1.1),cos(uv.x*24.0-time*.8));
                float2 sampleUV = clamp(uv+ripple*pixel*affected*1.6,pixel,1.0-pixel);
                float2 radius = pixel*(2.0+sediment*14.0)*localBlur;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUV).rgb;
                [branch] if (localBlur > .005)
                {
                    color *= .4;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUV+float2(radius.x,0)).rgb*.15;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUV-float2(radius.x,0)).rgb*.15;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUV+float2(0,radius.y)).rgb*.15;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUV-float2(0,radius.y)).rgb*.15;
                }
                color *= lerp(half3(1,1,1),half3(.72,.96,1.07),.12+farWater*.22);
                half3 water = lerp(half3(.035,.19,.24),half3(.018,.075,.14),_WaterState.w);
                half3 silt = lerp(water,half3(.44,.34,.20),.85);
                float haze = saturate(.09*farWater+affected*(.42+.40*farWater));
                color = lerp(color,lerp(water,silt,sediment),haze);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
