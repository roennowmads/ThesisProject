Shader "Instanced/pointCloud_Procedural" {
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
		AlphaTest Greater .01
		//Cull Off
		ZWrite off 
		//LOD 200

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// Enable instancing for this shader
			#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
			#pragma target es3.1

			//float4x4 depthCameraTUnityWorld;

			//Texture2D<float> _MainTex;
			//sampler2D _MainTex;
			Texture2D<float4> _MainTex;
			//Texture2D<float4> _MainTex2;

			sampler2D _ColorTex;
			sampler2D _AlbedoTex;
			

			StructuredBuffer<float3> _Points;

			uniform matrix model;
			uniform float4 trans;
			uniform float aspect;
			uniform int _PointsCount;
			uniform uint _FrameTime;

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

			SamplerState sampler_MainTex
			{
				Filter = MIN_MAG_MIP_POINT;
				AddressU = Wrap;
				AddressV = Wrap;
			};

			/*SamplerState sampler_MainTex2
			{
				Filter = MIN_MAG_MIP_POINT;
				AddressU = Wrap;
				AddressV = Wrap;
			};*/

			static const uint magnitude = 13;
			static const uint texSize = 1 << magnitude; // 1 << 12 == 4096 
			static const float inv_texSize = 1.0 / texSize;

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
				
				//float2 texCoords = float2(instanceId, instanceId >> magnitude) * inv_texSize;
				
				//float value = _MainTex.SampleLevel(sampler_MainTex, texCoords, 0).a;

				half value = 0.0; 


				//if (_FrameTime < 13125805) {
					uint instanceId = v.iid + (_FrameTime * _PointsCount);//float(v.iid + _FrameTime);
					uint3 texelCoords = uint3(instanceId % texSize, instanceId >> magnitude, 0);
					value = _MainTex.Load(texelCoords).a;
				//}
				//else {
				//	uint instanceId = v.iid + (_FrameTime - 13125805);//float(v.iid + _FrameTime);
				//	uint3 texelCoords = uint3(instanceId % texSize, instanceId >> magnitude, 0);
				//	value = _MainTex2.Load(texelCoords).a;
				//}
				//float value = tex2Dlod(_MainTex, texCoords).a;

				/*if (value < 0.1) {
					return;
				}*/

				if (value > 0.7)
				{
					return;
				}
				/*if (value < 0.5 || value > 0.9999)
				{
					return;
				}*/
				/*if (value < 0.5)
				{
				return;
				}*/

				//o.color = tex2Dlod(_ColorTex, half4(value, 0, 0, 0)).rgb;
				o.color = float3(value, value, value);

				float4 point_position = float4(_Points[v.iid], 0.0);
				//Correcting the translation:
				o.vertex = mul(model, point_position);
				o.vertex += trans;
				o.vertex = UnityWorldToClipPos(o.vertex.xyz);

				//Translating the vertices in a quad shape:
				half size = 0.01 * exp(1.0 - value);
				half2 quadSize = half2(size, size * aspect);
				half2 deltaSize = quadCoords[v.id] * quadSize;
				o.vertex.xy += deltaSize;

				o.texCoord = quadTexCoords[v.id];
			}

			struct fragOutput
			{
				fixed4 color : SV_Target;
			};


			fragOutput frag(v2f i)
			{
				fragOutput o;
				
				fixed albedo = tex2D(_AlbedoTex, i.texCoord).a;
				o.color = fixed4(i.color, albedo);
				return o;
			}
			ENDCG
		}
	}
}