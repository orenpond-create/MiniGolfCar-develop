// Invisible pre-pass for XRaySilhouette: marks the piece's WHOLE silhouette in the stencil buffer
// (ZTest Always - whether or not it's hidden). The XRayOutline pass afterwards only draws outside this
// mark, so the outline becomes a ring around the piece instead of covering it. Draws no colour.
// Give each piece its own stencil ref (ball 2, car 1) and use the same ref on its outline material.
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
            ZTest Always
            Cull Off
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
