Shader"Postprocessing/Custom/Spider-Verse/3D glasses Focus"
{
    Properties
    {
        _Intensity("Intensity", Float) = 0.5
        _Thickness("Sample thickness", Float) = 0.0
        _DepthThickness("Sample thickness", Float) = 0.0
        _DepthThreshold("Sample threshold", Float) = 0.0
        [ShowAsVector2] _FocusPosition("Focus Screen Position", Vector) = (0.5, 0.5, 0, 0)
        _RightTint("Right Tint", Color) = (1, 0, 0, 1)
        _LeftTint("Left Tint", Color) = (0, 1, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "3D Glasses focus"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // Core.hlsl for XR dependencies
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

             // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            
            //Declara lo que hace falta para leer el depth buffer
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            SAMPLER(sampler_BlitTexture);


//Implementación de Sobel basada en: https://www.youtube.com/watch?v=RMt6DcaMxcE
            const static float sobel_kernel_left[9] = 
            {
                1, 0, -1,
                2, 0, -2,
                1, 0, -1
            };

            const static float sobel_kernel_up[9] = 
            {
                1, 2, 1,
                0, 0, 0,
                -1, -2, -1
            };
            
            const static float2 sobel_samples[9] =
            {
                float2(1, 1), float2(0, 1), float2(1, 1),
                float2(-1, 0), float2(0, 0), float2(1, 1),
                float2(-1, -1), float2(0, -1), float2(1, -1)
            };
            
            float _Thickness;
            float _Intensity;
            float _DepthThickness;
            float _DepthThreshold;

            //Solo hace falta declararlo, el header del depth le da el valor indicado
            half4 _SourceSize;

            float4 _RightTint;
            float4 _LeftTint;
            float2 _FocusPosition;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                //return float4(uv.xy, 0, 1);

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                #ifdef _LINEAR_TO_SRGB_CONVERSION
                col = LinearToSRGB(col);
                #endif

                #if defined(DEBUG_DISPLAY)
                half4 debugColor = 0;

                if(CanDebugOverrideOutputColor(col, uv, debugColor))
                {
                    return debugColor;
                }
                #endif
    
                float unfocus = distance(_FocusPosition, uv);
                //Sobel para todas las direcciones        
                float4 sobel = 0;
    
                //float depth = LOAD_TEXTURE2D_X(_CameraDepthTexture,
                //    _SourceSize.xy * uv).x;
                //depth = Linear01Depth(depth, _ZBufferParams);
                //return depth;
        
                            
                [unroll] for (int i = 0; i < 9; i++)
                {
                    float depth = LOAD_TEXTURE2D_X(_CameraDepthTexture, 
                    _SourceSize.xy * uv+ sobel_samples[i] * _Thickness * unfocus).x;
                    depth = LinearEyeDepth(depth, _ZBufferParams);
        
                    //Acumular los resultados de los kernels. Usar el inverso para la dirección contraria.
                    //Para el efecto solo nos importa lo horizontal, pero meh, didáctico
                    //Izquierda, Arriba, Derecha, Abajo
                    sobel += float4(sobel_kernel_left[i], sobel_kernel_up[i], 
                        -sobel_kernel_left[i], -sobel_kernel_up[i]) * depth;
                }
    

                sobel = smoothstep(0, _DepthThreshold, sobel);
                sobel = pow(sobel, _DepthThickness);
                
                //Mostrar detección de bordes
                //float sobel_mask = sobel.x + sobel.y + sobel.z + sobel.w;
                //return sobel_mask;
    

                
                return lerp(col, _RightTint, sobel.b * _Intensity) * 0.5 + //Right
                        lerp(col, _LeftTint, sobel.r * _Intensity) * 0.5;  //Left
            }
            ENDHLSL
        }
    }
}
