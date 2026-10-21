Shader "ConsoleCards/Persona Character"
{
    // PERSONA-2 (owner, 2026-10-21: "full colour with a seat glow"): a player's character in full colour, softly
    // lit by the scene's main light, with an edge light in the seat colour (brighter on the player's turn) and a
    // light holographic flicker on that edge. On the body, the clothes are painted by the shader from the mesh's
    // colour mask (made by the Persona builder: red = clothed, green = lower body, blue = shoes), so any colour and
    // pattern can be chosen. Hair and eyes are recoloured by brightness (_ColourMode 1). A character close to the
    // camera fades out by dithering (_Fade). Unlit pass, like the other ConsoleCards shaders.
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        [Enum(Multiply,0,Recolour,1)] _ColourMode ("Colour Mode", Float) = 0
        [Toggle] _UseOutfit ("Clothes from the Mask", Float) = 0
        _TopColour ("Top", Color) = (0.2,0.2,0.25,1)
        _BottomColour ("Bottom", Color) = (0.15,0.2,0.4,1)
        _ShoeColour ("Shoes", Color) = (0.1,0.1,0.1,1)
        _Pattern ("Pattern (0 plain, 1 two-tone, 2 stripes, 3 circuit)", Float) = 0
        _RimColour ("Seat Colour", Color) = (0.2,0.55,0.96,1)
        _RimStrength ("Seat Glow", Float) = 0.6
        _RimPower ("Seat Glow Sharpness", Range(0.5,8)) = 3
        _Flicker ("Flicker", Range(0,1)) = 0.12
        _Fade ("Fade (1 shown)", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;
            float _ColourMode;
            float _UseOutfit;
            fixed4 _TopColour;
            fixed4 _BottomColour;
            fixed4 _ShoeColour;
            float _Pattern;
            fixed4 _RimColour;
            float _RimStrength;
            float _RimPower;
            float _Flicker;
            float _Fade;

            // Set by URP for every frame: the main directional light.
            float4 _MainLightPosition;
            half4 _MainLightColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 colour : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 objectPos : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                fixed4 mask : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.objectPos = input.vertex.xyz;
                output.screenPos = ComputeScreenPos(output.position);
                output.mask = input.colour;
                return output;
            }

            // 4 x 4 ordered dither for fading without transparency.
            float Dither(float2 pixel)
            {
                const float4x4 bayer = float4x4(
                    0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                    3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                    15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0);
                uint2 p = uint2(pixel) % 4;
                return bayer[p.x][p.y];
            }

            // Plain: one colour for the whole outfit. Two-tone: top and bottom. Stripes: two-tone with stripes of
            // the bottom colour across the top. Circuit: two-tone with thin bright traces. Shoes always their own.
            fixed3 Clothes(v2f input, fixed3 skin)
            {
                float clothed = step(0.5, input.mask.r);
                float lower = step(0.5, input.mask.g);
                float shoes = step(0.5, input.mask.b);
                fixed3 top = _TopColour.rgb;
                fixed3 bottom = _Pattern < 0.5 ? _TopColour.rgb : _BottomColour.rgb;
                float3 p = input.objectPos * 12.0;
                if (_Pattern > 1.5 && _Pattern < 2.5)
                {
                    top = lerp(top, bottom, step(0.5, frac(p.y * 0.9)));
                }
                else if (_Pattern > 2.5)
                {
                    float3 g = abs(frac(p * 0.6) - 0.5);
                    float trace = step(min(min(g.x, g.y), g.z), 0.035);
                    top = saturate(top + (0.35 * trace));
                    bottom = saturate(bottom + (0.25 * trace));
                }

                fixed3 cloth = lerp(top, bottom, lower);
                cloth = lerp(cloth, _ShoeColour.rgb, shoes);
                return lerp(skin, cloth, clothed);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                if (_Fade < 0.999)
                {
                    float2 pixel = (input.screenPos.xy / max(input.screenPos.w, 1e-5)) * _ScreenParams.xy;
                    clip(_Fade - Dither(pixel) - 0.001);
                }

                fixed4 art = tex2D(_MainTex, input.uv);
                fixed3 albedo = art.rgb * _Tint.rgb;
                if (_ColourMode > 0.5)
                {
                    float brightness = dot(art.rgb, float3(0.299, 0.587, 0.114));
                    albedo = _Tint.rgb * saturate(brightness * 1.8);
                }

                if (_UseOutfit > 0.5)
                {
                    albedo = Clothes(input, albedo);
                }

                float3 normal = normalize(input.worldNormal);
                float3 lightDir = normalize(_MainLightPosition.xyz);
                float wrap = saturate(dot(normal, lightDir) * 0.5 + 0.5);
                fixed3 light = 0.45 + (0.75 * wrap * _MainLightColor.rgb);
                fixed3 colour = albedo * light;

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.worldPos);
                float rim = pow(saturate(1.0 - abs(dot(normal, viewDir))), _RimPower);
                float noise = frac(sin(floor(_Time.y * 14.0) * 12.9898) * 43758.5453);
                float flicker = 1.0 - (_Flicker * noise);
                colour += _RimColour.rgb * rim * _RimStrength * flicker;
                return fixed4(colour, 1.0);
            }
            ENDCG
        }
    }
}
