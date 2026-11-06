Shader "Demon's Maze/Combat Screen"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _Fear ("Fear", Range(0,1)) = 0
        _Rage ("Rage", Range(0,1)) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Fear;
            float _Rage;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, input.uv);
                float gray = dot(scene.rgb, float3(0.2126, 0.7152, 0.0722));
                float edge = smoothstep(0.15, 0.71, length(input.uv - 0.5));
                float3 red = scene.rgb * float3(1.35, 0.70, 0.64) + float3(0.11, 0.012, 0.008) * edge;
                return fixed4(lerp(lerp(scene.rgb, red, _Rage), gray.xxx, _Fear), scene.a);
            }
            ENDCG
        }
    }
}
