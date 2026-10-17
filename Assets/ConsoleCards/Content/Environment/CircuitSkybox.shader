// Circuit skybox (doc 30, C0 quick win Q2; owner, 2026-10-18: currents moving along the circuits).
// The panoramic circuit picture plus bright-teal pulses that run along its traces like electrical current.
// _FlowTex matches CircuitSkybox.png: R = where a trace is (faded towards the poles), G/B = the cosine/sine of
// the position along the trace, so a pulse can travel along it. Pulse Strength 0 turns the movement off.
Shader "ConsoleCards/Circuit Skybox"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Circuit picture (360°)", 2D) = "black" {}
        [NoScaleOffset] _FlowTex ("Flow map (linear)", 2D) = "black" {}
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        [HDR] _PulseColor ("Pulse colour", Color) = (0.20, 0.88, 0.78, 1)
        _PulseStrength ("Pulse strength (0 = still)", Range(0, 4)) = 1.4
        _PulseSpeed ("Pulse speed", Range(0, 2)) = 0.12
        _PulseSharpness ("Pulse length (higher = shorter)", Range(1, 40)) = 14
        _PulseRepeat ("Pulses per stretch of trace", Range(1, 4)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _FlowTex;
            half _Exposure;
            float _Rotation;
            half4 _PulseColor;
            half _PulseStrength;
            float _PulseSpeed;
            half _PulseSharpness;
            float _PulseRepeat;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundY(float3 position, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;
                float s, c;
                sincos(radians, s, c);
                return float3(c * position.x - s * position.z, position.y, s * position.x + c * position.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(RotateAroundY(v.vertex.xyz, _Rotation));
                o.direction = v.vertex.xyz;
                return o;
            }

            // The same latitude-longitude layout as Unity's Skybox/Panoramic.
            float2 RadialCoords(float3 direction)
            {
                float3 d = normalize(direction);
                float latitude = acos(d.y);
                float longitude = atan2(d.z, d.x);
                return float2(0.5, 1.0) - float2(longitude * (0.5 / UNITY_PI), latitude / UNITY_PI);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = RadialCoords(i.direction);
                half3 colour = tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb;
                half4 flow = tex2Dlod(_FlowTex, float4(uv, 0, 0));
                float along = atan2(flow.b * 2.0 - 1.0, flow.g * 2.0 - 1.0) / (2.0 * UNITY_PI);
                float wave = frac(along * _PulseRepeat - _Time.y * _PulseSpeed);
                half pulse = pow(wave, _PulseSharpness) * flow.r * _PulseStrength;
                colour += _PulseColor.rgb * pulse;
                return half4(colour * _Exposure, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
