Shader "ConsoleCards/Persona Hologram"
{
    // PERSONA-1 (owner, 2026-10-21): a player's Persona, a hologram figure behind their seat. The seat colour,
    // with moving scanlines and a light flicker; additive, so it glows. A solid shape glows most at its edges (the
    // rim, as a hologram does) and is faint inside (_Fill), which shows its volume; a flat picture (the chest
    // emblem) uses _Fill 1 and no rim. _Intensity is raised on the player's turn. Unlit, like the other ConsoleCards
    // shaders (drawn in URP's unlit pass).
    Properties
    {
        _MainTex ("Picture (white: none)", 2D) = "white" {}
        _Color ("Seat Colour", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Fill ("Fill (inside)", Range(0,1)) = 1
        _Rim ("Rim (edges)", Range(0,4)) = 0
        _RimPower ("Rim Sharpness", Range(0.5,8)) = 2.5
        _ScanDensity ("Scanlines per Unit", Float) = 40
        _ScanSpeed ("Scanline Speed", Float) = 0.6
        _ScanDepth ("Scanline Depth", Range(0,1)) = 0.35
        _Flicker ("Flicker", Range(0,1)) = 0.08
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull [_Cull]
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Intensity;
            float _Fill;
            float _Rim;
            float _RimPower;
            float _ScanDensity;
            float _ScanSpeed;
            float _ScanDepth;
            float _Flicker;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 art = tex2D(_MainTex, input.uv);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.worldPos);
                float facing = abs(dot(normalize(input.worldNormal), viewDir));
                float rim = pow(saturate(1.0 - facing), _RimPower);
                float shape = _Fill + (_Rim * rim);
                float scan = 1.0 - _ScanDepth * (0.5 + 0.5 * sin((input.worldPos.y * _ScanDensity - _Time.y * _ScanSpeed) * 6.2831853));
                float noise = frac(sin(floor(_Time.y * 18.0) * 12.9898) * 43758.5453);
                float flicker = 1.0 - _Flicker * noise;
                float alpha = saturate(art.a * _Color.a * shape * scan * flicker * saturate(_Intensity));
                clip(alpha - 0.005);
                return fixed4(_Color.rgb * art.rgb * max(_Intensity, 0.0), alpha);
            }
            ENDCG
        }
    }
}
