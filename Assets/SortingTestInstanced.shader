﻿Shader "Unlit/SortingTestInstanced" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
		_MainTex2("_MainTex2", 2D) = "white" {}
		_AlbedoTex("AlbedoTex", 2D) = "white" {}
		_ColorTex("_ColorTex", 2D) = "white" {}
		_FrameTime("_FrameTime", Int) = 0
	}
	SubShader {
		Tags{"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
		//Tags{"RenderType" = "Opaque"}
		Blend SrcAlpha OneMinusSrcAlpha
		//Cull Off
		ZWrite Off 
		//LOD 200

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// Enable instancing for this shader
			//#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
			#pragma target es3.1
			
			//float4x4 depthCameraTUnityWorld;

			//Texture2D<float> _MainTex;
			//sampler2D _MainTex;
			//Texture2D<float4> _MainTex;
			//Texture2D<float4> _MainTex2;

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
			uniform float pointSizeScale;
			uniform float pointSizeScaleIndependent;

			struct appdata
			{
				uint id : SV_VertexID;
				uint iid : SV_InstanceID;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			}; 
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed3 color : COLOR;
				float2 texCoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			//SamplerState sampler_MainTex
			//{
			//	Filter = MIN_MAG_MIP_POINT;
			//	AddressU = Wrap;
			//	AddressV = Wrap;
			//};

			//SamplerState sampler_MainTex2
			//{
			//	Filter = MIN_MAG_MIP_POINT;
			//	AddressU = Wrap;
			//	AddressV = Wrap;
			//};

			//static const uint magnitude = 14;
			//static const uint texSize = 1 << _Magnitude; // 1 << 12 == 4096 
			//static const float inv_texSize = 1.0 / texSize;

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
				UNITY_SETUP_INSTANCE_ID(v); 
				//UNITY_TRANSFER_INSTANCE_ID(v, o);				
				
				// http://http.developer.nvidia.com/GPUGems2/gpugems2_chapter33.html
				// "It may also be possible to eliminate the frac instruction from Listing 33-2 by using the repeating-tiled addressing mode (such as GL_REPEAT)."
				// This means I might not need the modulus operation, since it will do it automatically.

				//Division by 4096 == instanceId << 12 

				//float quadId = v.id * inv6;
				float quadId = v.iid;
				uint quad_vertexID = v.id;
				//uint quad_vertexID = -6.0 * floor(quadId) + v.id;
				//uint quad_vertexID = mad(-6.0, floor(quadId), v.id);

				/*if (quadId % 2 != 0) {
					return;
				}*/


				uint value = _IndicesValues.Load(quadId);

				uint index = value >> 8;
				float colorValue = (value & 0xFF) * inv255;

				//good for fireball:
				//o.color = tex2Dlod(_ColorTex, half4(value*2.0, 0, 0, 0)).rgb /** modifier*/;
				//o.color = tex2Dlod(_ColorTex, half4(pow((value*5.0), .03125), 0, 0, 0)).rgb /** modifier*/;
			    o.color = tex2Dlod(_ColorTex, half4(pow((colorValue), .0625), 0, 0, 0)).rgb /** modifier*/;
				//o.color = tex2Dlod(_ColorTex, half4(pow((value*5.0), .0625)*0.95, 0, 0, 0)).rgb /** modifier*/;
				//o.color = float3(value, value, value);
				//o.color = float3(0.0, 0.0, 1.0);
				
				//_Points[v.iid] += float3(0.1, 0.1, 0.1);

				//Correcting the translation:
				o.vertex = float4(-_Points[index], 1.0);
				o.vertex = UnityObjectToClipPos(o.vertex);
				//o.vertex += trans;
				//o.vertex = UnityWorldToClipPos(o.vertex.xyz);
				
				

				//Translating the vertices in a quad shape:
				//half size = 0.4 * exp(1.0 - value) /** modifier*/;
				//half size = 0.02 /** exp(1.0 - colorValue)*/ /** modifier*/;
				half size = pointSizeScaleIndependent*pointSizeScale*0.345;//0.352;
				//half size = 0.15 * exp(value) /** modifier*/;
				half2 quadSize = half2(size, size * aspect); 

				float2 quadCoordsAndTexCoord = quadTexCoords[quad_vertexID];

				half2 deltaSize = (quadCoordsAndTexCoord * 2.0 - 1.0) * quadSize;
				o.vertex.xy += deltaSize;

				o.texCoord = quadCoordsAndTexCoord + float2(0.02, -0.015);
			}

			struct fragOutput
			{
				fixed4 color : SV_Target;
			};


			fragOutput frag(v2f i)
			{
				fragOutput o;
				
				//fixed albedo = tex2D(_AlbedoTex, i.texCoord).a;

				//good for fireball:
				//o.color = fixed4(i.color, albedo*0.0125);

				fixed albedo = tex2Dlod(_AlbedoTex, float4(i.texCoord, 0.0, 0.0) /*float2(0.5,0.5)*/).a;
				//fixed albedo = tex2D(_AlbedoTex, i.texCoord /*float2(0.5,0.5)*/).a;

				//float3 a = _ROV1[0];

				if (albedo < 0.325) {  //for quad
					discard;
					//o.color = fixed4(fixed3(0.0,1.0,0.0), 0.1/*albedo*//**0.25*/);
				}
				else {
					o.color = fixed4(i.color/*color*/, 0.1 + albedo*0.00000001/*albedo*0.25*//*albedo*//**0.25*/);
				}

				return o;
			}
			ENDCG
		}
	}
}