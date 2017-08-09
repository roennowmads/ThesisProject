Shader "Unlit/SortingTest" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
		_MainTex2("_MainTex2", 2D) = "white" {}
		_AlbedoTex("AlbedoTex", 2D) = "white" {}
		_ColorTex("_ColorTex", 2D) = "white" {}
		_FrameTime("_FrameTime", Int) = 0
	}
	SubShader {
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		//Tags{"RenderType" = "Opaque"}
		//Blend SrcAlpha OneMinusSrcAlpha
		//Cull Off
		ZWrite Off
		//LOD 200

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#pragma target es3.1

			sampler2D _ColorTex;
			sampler2D _AlbedoTex;


			StructuredBuffer<float3> _Points;
			StructuredBuffer<uint> _IndicesValues;

			uniform matrix model;
			uniform float4 trans;
			uniform float aspect;
			uniform int _PointsCount;
			uniform uint _FrameTime;
			uniform uint _Magnitude;
			uniform int _TextureSwitchFrameNumber;

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

			// Declare instanced properties inside a cbuffer.
			// Each instanced property is an array of by default 500(D3D)/128(GL) elements. Since D3D and GL imposes a certain limitation
			// of 64KB and 16KB respectively on the size of a cubffer, the default array size thus allows two matrix arrays in one cbuffer.
			// Use maxcount option on #pragma instancing_options directive to specify array size other than default (divided by 4 when used
			// for GL).
			//UNITY_INSTANCING_CBUFFER_START(Props)
			//	UNITY_DEFINE_INSTANCED_PROP(float4, _Pos)	// Make _Color an instanced property (i.e. an array)
			//	UNITY_INSTANCING_CBUFFER_END

			void vert(in appdata v, out v2f o)
			{
				UNITY_INITIALIZE_OUTPUT(v2f, o)

				float quadId = v.id * inv6;
				//uint quad_vertexID = v.id % 6;
				//uint quad_vertexID = -6.0 * floor(quadId) + v.id;
				uint quad_vertexID = mad(-6.0, floor(quadId), v.id);

				/*if (quadId % 2 != 0) {
				return;
				}*/


				uint value = _IndicesValues.Load(quadId);

				uint index = value; //>> 8;
				//float colorValue = (value & 0xFF) * inv255;

				//good for fireball:
				//o.color = tex2Dlod(_ColorTex, half4(value*2.0, 0, 0, 0)).rgb /** modifier*/;
				//o.color = tex2Dlod(_ColorTex, half4(pow((value*5.0), .03125), 0, 0, 0)).rgb /** modifier*/;
				//o.color = tex2Dlod(_ColorTex, half4(pow((colorValue*2.0), .0625), 0, 0, 0)).rgb /** modifier*/;
				//o.color = tex2Dlod(_ColorTex, half4(pow((value*5.0), .0625)*0.95, 0, 0, 0)).rgb /** modifier*/;
				//o.color = float3(value, value, value);
				o.color = float3(0.0, 0.0, quadId / 16.0);

				//_Points[v.iid] += float3(0.1, 0.1, 0.1);

				//Correcting the translation:
				o.vertex = mul(model, -_Points[index]);
				o.vertex += trans;
				o.vertex = UnityWorldToClipPos(o.vertex.xyz);



				//Translating the vertices in a quad shape:
				//half size = 0.4 * exp(1.0 - value) /** modifier*/;
				half size = 0.2 /** exp(1.0 - colorValue)*/ /** modifier*/;
				//half size = 0.15 * exp(value) /** modifier*/;
				half2 quadSize = half2(size, size * aspect);
				half2 deltaSize = quadCoords[quad_vertexID] * quadSize;
				o.vertex.xy += deltaSize;

				o.texCoord = quadTexCoords[quad_vertexID];
			}

			struct fragOutput
			{
				fixed4 color : SV_Target;
			};


			fragOutput frag(v2f i)
			{
				fragOutput o;

				fixed albedo = tex2D(_AlbedoTex, i.texCoord).a;
				//o.color = fixed4(/*i.color*/fixed3(0.5,0.1,0.1), albedo*0.0525);

				//good for fireball:
				o.color = fixed4(i.color, albedo/**0.125*/);
				return o;
			}
			ENDCG
		}
	}
}