Shader "Unlit/AccumShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Accumulate" }
		Cull Off

		Pass
		{
			ZWrite Off
			Blend 0 One One
			Blend 1 Zero OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#pragma target es3.1

			#pragma shader_feature  _WEIGHTED_ON
			#pragma multi_compile _WEIGHTED0 _WEIGHTED1 _WEIGHTED2

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
				float z : TEXCOORD1;
			};

			struct f2o
			{
				float4 col0 : COLOR0;
				float4 col1 : COLOR1;
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

				o.color = tex2Dlod(_ColorTex, half4(pow((colorValue*2.0), .0625), 0, 0, 0)).rgb /** modifier*/;
				
				o.vertex = UnityObjectToClipPos(position);

				//if (/*int(quadId) % 4 != 0 ||*/ int(quadId) < 50000) {
				//	return o;
				//}

				half size = 0.02;
				half2 quadSize = half2(size, size * aspect);
				half2 deltaSize = quadCoords[quad_vertexID] * quadSize;
				o.vertex.xy += deltaSize;

				o.texCoord = quadTexCoords[quad_vertexID];

				// Camera-space depth
				o.z = abs(mul(UNITY_MATRIX_MV, position).z);

				return o;
			}

			float w(float z, float alpha) {
			#ifdef _WEIGHTED0
				return pow(z, -2.5);
			#elif _WEIGHTED1
				return alpha * max(1e-2, min(3 * 1e3, 10.0 / (1e-5 + pow(z / 5, 2) + pow(z / 200, 6))));
			#elif _WEIGHTED2
				return alpha * max(1e-2, min(3 * 1e3, 0.03 / (1e-5 + pow(z / 200, 4))));
			#endif
				return 1.0;
			}
			
			//w = clamp(pow(min(1.0, premultipliedReflect.a * 10.0) + 0.01, 3.0) * 1e8 * pow(1.0 - gl_FragCoord.z * 0.9, 3.0), 1e-2, 3e3);


			f2o frag (v2f i) : SV_Target
			{
				f2o o;

				fixed albedo = tex2Dlod(_AlbedoTex, float4(i.texCoord, 0.0, 0.0) /*float2(0.5,0.5)*/).a;

				if (albedo < 0.7)
					discard;

				float alpha = albedo;
				float3 C = (i.color) * alpha;

			#ifdef _WEIGHTED_ON
				o.col0 = float4(C, alpha) * w(i.z, alpha);
			#else
				o.col0 = float4(C, alpha);
			#endif

				o.col1 = albedo.xxxx;

				return o;

				//o.color = fixed4(/*i.color*/fixed3(0.5,0.1,0.1), albedo*0.0525);

				//o.color = fixed4(i.color, albedo/**0.25*/);


				// sample the texture
				//fixed4 col = tex2D(_MainTex, i.uv);
				//return col;
			}
			ENDCG
		}
	}
}
