// Constant-width outline (in screen pixels) around a piece wherever something else hides it - like the
// hidden-ball outline in Golf With Your Friends. Used by XRaySilhouette together with XRayMask.
//
// XRayMask first marks the piece's whole silhouette in the stencil buffer. This pass then draws a copy of
// the piece inflated along its normals by Outline width pixels, only OUTSIDE that silhouette (stencil
// NotEqual) and only where something is in front of it (ZTest Greater) - so a hidden piece gets a ring,
// a visible one gets nothing. The inflation is done in 3D (view space), scaled by distance so it stays the
// same number of pixels, and both sides of every face are drawn (Cull Off) so the ring stays closed even
// on models made of separate, open panels (the same approach as the "Quick Outline" package).
//
// Hidden threshold: the copy is pulled this many metres toward the camera before the depth test, so only
// things at least that far in front count as hiding the piece. Without it a ball resting on the floor
// would get a ring at its bottom (the floor right in front of its lower edge is slightly closer).
// Use smoothed normals: reads normals averaged at the model's hard edges from UV4 (written by the car's
// baked mesh) so the outline doesn't open gaps at sharp corners. Leave off for smooth meshes (the ball).
// Works on mobile / Metal: plain vertex + fragment shader, no geometry shader.
Shader "MiniCarGolf/XRayOutline"
{
    Properties
    {
        _Color ("Outline color", Color) = (0.2, 0.9, 1, 1)
        _OutlineWidth ("Outline width (pixels)", Range(0, 20)) = 3
        _DepthBias ("Hidden threshold (m)", Float) = 0.03
        [Toggle] _UseSmoothNormals ("Use smoothed normals (UV4)", Float) = 0
        _StencilRef ("Stencil ref (same as the mask)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZTest Greater
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Separate variant: UV4 is only read when the toggle is on. A mesh without UV4 (e.g. the ball's
            // built-in sphere) is silently skipped by Unity if the shader asks for the channel.
            #pragma shader_feature_local _USESMOOTHNORMALS_ON
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _OutlineWidth, _DepthBias;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            #ifdef _USESMOOTHNORMALS_ON
                float3 smoothNormal : TEXCOORD3;   // mesh UV4
            #endif
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
            #ifdef _USESMOOTHNORMALS_ON
                float3 n = v.smoothNormal;
            #else
                float3 n = v.normal;
            #endif
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, n));

                // Size of one screen pixel at this vertex's distance (perspective camera), so the inflation
                // is Outline width pixels wide at any distance. abs(): when Unity renders into an intermediate
                // texture (e.g. with MSAA) it flips the sign of P[1][1], which would shrink the outline inward.
                float pixel = 2.0 * -viewPos.z / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);
                viewPos += viewNormal * (_OutlineWidth * pixel);

                // Pull toward the camera by the hidden threshold (view space looks down -z).
                viewPos.z += _DepthBias;

                o.pos = mul(UNITY_MATRIX_P, float4(viewPos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
