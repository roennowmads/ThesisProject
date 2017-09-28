
Shader "Unlit/BlendShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_AccumTex("Accum", 2D) = "black" {}
		_RevealageTex("Revealage", 2D) = "white" {}
	}
	SubShader
	{
		//Tags { "RenderType"="Opaque" }
		ZTest Always 
		Cull Off ZWrite Off 
		Fog{ Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _MainTex;
			sampler2D _AccumTex;
			sampler2D _RevealageTex;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float2 texcoord : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				/*float revealage = tex2D(_RevealageTex, i.texcoord).r;
				if (revealage == 1.0) {
					// Save the blending and color texture fetch cost
					return fixed4(0, 0, 0, 0);
				}

				fixed4 background = tex2D(_MainTex, i.texcoord);
				float4 accum = tex2D(_AccumTex, i.texcoord);*/

				fixed4 background = tex2D(_MainTex, i.texcoord);
				float4 accum = tex2D(_AccumTex, i.texcoord);
				float revealage = tex2D(_RevealageTex, i.texcoord).r;
				fixed4 col = float4(accum.rgb / clamp(accum.a, 1e-4, 5e4), revealage);
				return (1.0 - col.a) * col + col.a * background;
			}
			ENDCG
		}
	}
}
