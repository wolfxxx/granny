// Stylized URP water: gerstner-ish sine waves, fresnel sky tint, sun glints, fog.
Shader "GrannyWorld/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.25, 0.75, 0.85, 0.75)
        _DeepColor ("Deep Color", Color) = (0.05, 0.32, 0.55, 0.92)
        _SkyColor ("Sky Reflection Color", Color) = (0.70, 0.85, 1.0, 1)
        _WaveAmplitude ("Wave Amplitude", Float) = 0.12
        _WaveLength ("Wave Length", Float) = 9
        _WaveSpeed ("Wave Speed", Float) = 1.2
        _Glossiness ("Glossiness", Range(8, 512)) = 180
        _SparkleScale ("Sparkle Scale", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _SkyColor;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                float _Glossiness;
                float _SparkleScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            // Sum of a few directional sine waves; returns height and analytic gradient.
            float3 Waves(float2 p, float t)
            {
                const float2 dirs[4] = { float2(1, 0.23), float2(-0.37, 1), float2(0.71, -0.61), float2(-0.93, -0.29) };
                const float scales[4] = { 1.0, 0.737, 0.531, 0.389 };
                float h = 0; float2 grad = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 d = normalize(dirs[i]);
                    float k = 6.28318 / (_WaveLength * scales[i]);
                    float a = _WaveAmplitude * scales[i];
                    float ph = dot(d, p) * k + t * _WaveSpeed * (1.0 + i * 0.37);
                    h += a * sin(ph);
                    grad += a * k * cos(ph) * d;
                }
                return float3(h, grad);
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                // the far grid is coarse, so only displace vertices near the camera
                float near = saturate(1.0 - distance(ws, _WorldSpaceCameraPos) / 120.0);
                ws.y += Waves(ws.xz, _Time.y).x * near;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 w = Waves(i.positionWS.xz, _Time.y);
                // add a fine ripple layer to the normal for sparkle
                float2 rip = float2(sin(i.positionWS.x * 2.3 + _Time.y * 2.1) + sin(i.positionWS.z * 1.7 - _Time.y * 1.6),
                                    cos(i.positionWS.z * 2.1 + _Time.y * 1.8) + cos(i.positionWS.x * 1.3 - _Time.y * 2.4)) * 0.03;
                // flatten the normals with distance so the wave pattern doesn't visibly tile
                float fade = saturate(1.0 - distance(i.positionWS, _WorldSpaceCameraPos) / 220.0);
                fade *= fade;
                float3 n = normalize(float3((-w.y - rip.x) * fade, 1.0, (-w.z - rip.y) * fade));
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light sun = GetMainLight();
                float ndl = saturate(dot(n, sun.direction));
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 4.0);

                float depthTint = saturate(w.x / max(_WaveAmplitude, 1e-3) * 0.5 + 0.5);
                float4 baseCol = lerp(_DeepColor, _ShallowColor, depthTint * 0.6 + 0.2);
                float3 col = baseCol.rgb * (0.55 + 0.45 * ndl) * sun.color;
                col = lerp(col, _SkyColor.rgb, fresnel * 0.75);

                float3 h = normalize(sun.direction + viewDir);
                float spec = pow(saturate(dot(n, h)), _Glossiness);
                float sparkle = step(0.985, spec) * _SparkleScale;
                col += sun.color * (spec * 1.6 + sparkle);

                float alpha = saturate(baseCol.a + fresnel * 0.3 + spec);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
