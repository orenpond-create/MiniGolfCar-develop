// Rail material that opens a see-through "window" where the rail stands between the camera and the
// golf ball. Outside the window it is a normal Standard-lit surface.
// Inside the window the rail is drawn semi-transparent (Window opacity: 0 = invisible, 1 = solid),
// with an optional glowing wireframe grid and a glowing edge ring.
//
// How: pass 1 (surface shader) draws the rail normally but cuts the window out (clip), keeping only the
// ring / grid lines as glow. Pass 2 draws only the window pixels again with alpha blending - so the
// costly blending happens only inside the small window, not on the whole rail.
//
// The ball's screen position comes from the global _BallScreen, set every frame by BallOcclusionHelper
// on the game camera (xy = viewport position, z = eye depth, w = window radius in viewport-height units).
// When _BallScreen.w is 0 (no helper, or edit mode) the rail renders normally.
// Shadows come from the Standard fallback, so the rail's shadow never gets a hole.
Shader "MiniCarGolf/SeeThroughRail"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.15, 0.15, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _WindowOpacity ("Window opacity (0 = see-through, 1 = solid)", Range(0, 1)) = 0.4
        _GlowColor ("Glow color (ring / grid)", Color) = (0.2, 0.9, 1, 1)
        _EdgeWidth ("Window edge ring (fraction of radius, 0 = none)", Range(0, 0.5)) = 0.08
        [Toggle] _ShowGrid ("Show wireframe grid", Float) = 0
        _GridSize ("Wireframe grid size (m)", Float) = 0.02
        _LineWidth ("Grid line width (pixels)", Float) = 1.2
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }

        CGINCLUDE
        float4 _BallScreen;
        float _BallScreenAspect;
        float _EdgeWidth, _ShowGrid, _GridSize, _LineWidth;

        float SeeThroughGridLine (float coord)
        {
            float x = coord / _GridSize;
            float d = abs(frac(x + 0.5) - 0.5) / max(fwidth(x), 1e-4);
            return saturate(_LineWidth * 0.5 + 0.5 - d);
        }

        // Grid lines on the two axes running along the face (the axis along the normal is skipped: it is
        // nearly constant across a face, and if it lands on a grid line it would fill the whole face).
        float SeeThroughGrid (float3 worldPos, float3 worldNormal)
        {
            float3 n = abs(normalize(worldNormal));
            return max(max(SeeThroughGridLine(worldPos.x) * step(n.x, 0.7),
                           SeeThroughGridLine(worldPos.y) * step(n.y, 0.7)),
                           SeeThroughGridLine(worldPos.z) * step(n.z, 0.7)) * step(0.5, _ShowGrid);
        }

        // r: 0 at the ball, 1 at the window edge. inWindow: rail pixel in front of the ball, inside the circle.
        void SeeThroughWindow (float4 screenPos, float eyeDepth, out float r, out bool inWindow)
        {
            float2 delta = screenPos.xy / screenPos.w - _BallScreen.xy;
            delta.x *= _BallScreenAspect;
            r = length(delta) / max(_BallScreen.w, 1e-5);
            inWindow = _BallScreen.w > 0 && eyeDepth < _BallScreen.z && r < 1;
        }
        ENDCG

        // ---- Pass 1: normal lit rail, window cut out (ring / grid lines kept as glow) ----
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        fixed4 _Color, _GlowColor;
        half _Glossiness, _Metallic;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float4 screenPos;
            float eyeDepth;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            COMPUTE_EYEDEPTH(o.eyeDepth);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            float lines = SeeThroughGrid(IN.worldPos, IN.worldNormal);   // derivatives: keep outside branches
            float r; bool inWindow;
            SeeThroughWindow(IN.screenPos, IN.eyeDepth, r, inWindow);
            if (inWindow)
            {
                float glow = max(lines, step(1 - _EdgeWidth, r) * step(0.001, _EdgeWidth));
                clip(glow - 0.5);
                o.Albedo = 0;
                o.Emission = _GlowColor.rgb;
            }
        }
        ENDCG

        // ---- Pass 2: the window area again, semi-transparent ----
        // Back faces are culled, so through the outer face you see the lane / ball, not the rail's inner face.
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert2
            #pragma fragment frag2
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            fixed4 _Color;
            float _WindowOpacity;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            v2f vert2 (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.eyeDepth);
                return o;
            }

            fixed4 frag2 (v2f i) : SV_Target
            {
                float lines = SeeThroughGrid(i.worldPos, i.worldNormal);
                float r; bool inWindow;
                SeeThroughWindow(i.screenPos, i.eyeDepth, r, inWindow);
                float glow = max(lines, step(1 - _EdgeWidth, r) * step(0.001, _EdgeWidth));
                clip(inWindow ? 0.5 - glow : -1);   // only window pixels that pass 1 cut out

                // Simple lighting so the see-through part keeps roughly the rail's shading.
                float3 n = normalize(i.worldNormal);
                float3 light = ShadeSH9(float4(n, 1)) + _LightColor0.rgb * saturate(dot(n, _WorldSpaceLightPos0.xyz));
                return fixed4(_Color.rgb * light, _WindowOpacity);
            }
            ENDCG
        }
    }
    Fallback "Standard"
}
