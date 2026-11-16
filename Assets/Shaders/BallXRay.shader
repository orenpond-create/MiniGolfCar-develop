// X-ray "glowing wireframe globe" for the golf ball.
// Add it as a SECOND material on the ball's MeshRenderer. It draws only where something is in front
// of the ball (ZTest Greater), so the normal ball looks unchanged and the glow appears only when hidden.
// The "wireframe" is a latitude/longitude grid from the sphere's UVs plus a bright rim - no geometry
// shader, so it works on mobile and Metal. Cost: one extra draw of the ball mesh.
// Set both line counts to 0 for a rim-only silhouette (used for the car, whose UVs don't form a grid).
// Stencil: the ball leaves the compare at Always. XRaySilhouette (car) sets NotEqual together with the
// XRayMask pass, so parts of an object hidden only by the object itself don't glow.
Shader "MiniCarGolf/BallXRay"
{
    Properties
    {
        _Color ("Glow color", Color) = (0.2, 0.9, 1, 1)
        _LongitudeLines ("Longitude lines (0 = none)", Float) = 6
        _LatitudeLines ("Latitude lines (0 = none)", Float) = 4
        _LineWidth ("Line width (pixels)", Float) = 1.2
        _RimPower ("Rim power", Float) = 2
        _FillAlpha ("Fill alpha", Range(0, 1)) = 0.15
        _StencilRef ("Stencil ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil compare", Float) = 8
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZTest Greater
            ZWrite Off
            Cull Back
            Blend SrcAlpha One
            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _LongitudeLines, _LatitudeLines, _LineWidth, _RimPower, _FillAlpha;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 viewDir : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            // 1 on a grid line, 0 between lines; line width in screen pixels, anti-aliased.
            float GridLine (float coord, float count)
            {
                float x = coord * count;
                float d = abs(frac(x + 0.5) - 0.5) / max(fwidth(x), 1e-4);
                return saturate(_LineWidth * 0.5 + 0.5 - d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // step(0.5, count) switches a set of lines off when its count is 0.
                float lines = max(GridLine(i.uv.x, _LongitudeLines) * step(0.5, _LongitudeLines),
                                  GridLine(i.uv.y, _LatitudeLines) * step(0.5, _LatitudeLines));
                float rim = pow(1 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), _RimPower);
                float a = saturate(_FillAlpha + lines + rim);
                return fixed4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
}
