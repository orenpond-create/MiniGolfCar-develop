// Invisible pre-pass for XRaySilhouette: marks the stencil buffer wherever the object is VISIBLE
// (passes the normal depth test). The X-ray pass afterwards only draws where this mark is missing,
// so parts hidden by the object itself (far wheels behind the body) don't glow - only parts hidden
// by something else do. Draws no colour.
Shader "MiniCarGolf/XRayMask"
{
    Properties
    {
        _StencilRef ("Stencil ref", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert (float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag () : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
