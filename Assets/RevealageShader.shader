Shader "Unlit/RevealageShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Revealage" }
		Cull Off

		Pass
		{
			ZWrite Off
			Blend Zero OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#pragma target es3.1

			sampler2D _ColorTex;
			sampler2D _AlbedoTex;

			StructuredBuffer<float3> _Points;
			StructuredBuffer<uint> _IndicesValues;

			uniform float aspect;

			struct appdata
			{
				uint id : SV_VertexID;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed3 color : COLOR;
				float2 texCoord : TEXCOORD0;
			};

			static const half2 quadCoords[6] = {
				half2(-0.1, -0.1),
				half2(0.1, 0.1),
				half2(0.1, -0.1),

				half2(-0.1, 0.1),
				half2(0.1, 0.1),
				half2(-0.1, -0.1)
			};

			static const float2 quadTexCoords[6] = {
				float2(0.0, 0.0),
				float2(1.0, 1.0),
				float2(1.0, 0.0), 

				float2(0.0, 1.0),
				float2(1.0, 1.0),
				float2(0.0, 0.0)
			};

			static const float inv6 = 1.0 / 6.0;
			static const float inv255 = 1.0 / 255.0;
			
			v2f vert (appdata v)
			{
				v2f o;

				float quadId = v.id * inv6;

				uint value = _IndicesValues[quadId];

				uint quad_vertexID = mad(-6.0, floor(quadId), v.id);  //Useful trick: foo % n == foo & (n - 1), if n is a power of 2

				uint index = value >> 8;
				float4 position = float4(-_Points[index], 1.0);
				float colorValue = (value & 0xFF) * inv255;

				o.color = tex2Dlod(_ColorTex, half4(pow((colorValue), .0625), 0, 0, 0)).rgb /** modifier*/;

				o.vertex = UnityObjectToClipPos(position);

				//if (/*int(quadId) % 4 != 0 ||*/ int(quadId) < 50000) {
				//	return o;
				//}

				half size = 0.08;
				half2 quadSize = half2(size, size * aspect);
				half2 deltaSize = quadCoords[quad_vertexID] * quadSize;
				o.vertex.xy += deltaSize;

				o.texCoord = quadTexCoords[quad_vertexID];

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed albedo = tex2Dlod(_AlbedoTex, float4(i.texCoord, 0.0, 0.0) /*float2(0.5,0.5)*/).a;

				if (albedo < 0.7)
					discard;

				//fixed4 alpha = fixed4(i.color, 1.0) * albedo;

				//return alpha.aaaa;//alpha.aaaa;

				return albedo.xxxx;
			}
			ENDCG
		}
	}
}
