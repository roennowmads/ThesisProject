Shader "Instanced/pointCloud_Procedural" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
		_AlbedoTex("AlbedoTex", 2D) = "white" {}
		_FrameTime("_FrameTime", Int) = 0
		//_Pos("_Pos", Vector) = (0.0, 0.0, 0.0, 0.0)
	}
	SubShader {
		//Tags{"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
		Tags{"RenderType" = "Opaque"}
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
			sampler2D _MainTex;
			sampler2D _AlbedoTex;
			int _FrameTime;

			uniform matrix model;
			uniform float4 trans;
			uniform float aspect;

			StructuredBuffer<float4> points;

			struct appdata
			{
				uint id : SV_VertexID;
				uint iid : SV_InstanceID;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			}; 

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float4 texCoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
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
				//o = (v2f)0;
				UNITY_INITIALIZE_OUTPUT(v2f, o)
				UNITY_SETUP_INSTANCE_ID(v);

				float texSize = 1024.0 * 4.0;
				float instanceId = float(v.iid + _FrameTime);
				float2 texCoords = float2(instanceId % texSize, instanceId / texSize) / texSize;
				o.color = tex2Dlod(_MainTex, float4(texCoords, 0, 0));

				if (o.color.a < 0.7)
				{
					return;
				}
				/*if (o.color.a < 0.5)
				{
					return;
				}*/


				//UNITY_TRANSFER_INSTANCE_ID(v, o);

				//float4 a = float4(0.0, 0.0, 0.0, 1.0);
				//float4 instancePos = UNITY_ACCESS_INSTANCED_PROP(_Pos);
				 
				//float4 quadCorner = quad[v.id];
				float4 point_position = points[v.iid] * 0.1;
				//float4 point_position = points[v.iid];

				//float4 vertex_position = quad[v.id];

				const float4 quadCoords[6] = {
					float4(-0.1, -0.1, 0.0, 0.0),
					float4(0.1, 0.1, 0.0, 0.0),
					float4(0.1, -0.1, 0.0, 0.0),

					float4(-0.1, 0.1, 0.0, 0.0),
					float4(0.1, 0.1, 0.0, 0.0),
					float4(-0.1, -0.1, 0.0, 0.0)
				};
				const float4 quadTexCoords[6] = {
					float4(0.0, 0.0, 0.0, 0.0),
					float4(1.0, 1.0, 0.0, 0.0),
					float4(1.0, 0.0, 0.0, 0.0),

					float4(0.0, 1.0, 0.0, 0.0),
					float4(1.0, 1.0, 0.0, 0.0),
					float4(0.0, 0.0, 0.0, 0.0)
				};

				//o.vertex = mul(UNITY_MATRIX_MVP, v.vertex +/*v.vertex*/);
				//o.vertex = UnityObjectToClipPos(/*instancePos*/ /*v.vertex*/ /*quadCorner*10.0 +*/ point_position*0.001 + float4(0.5 * float(v.id), 0.0, 0.0, 0.0));
				
				point_position = mul(model, point_position);
				point_position += trans;
				point_position = UnityWorldToClipPos(point_position.xyz);


				float quadSize = 0.01;

				quadSize = 0.01 * exp(o.color.a); /// (input[0].color.b*5.0 + 0.1);

				/*if (halfSideLength < 0.001)
				{
				return;
				}*/

				o.vertex = point_position;
				o.vertex.x += quadCoords[v.id].x * quadSize;
				o.vertex.y += quadCoords[v.id].y * quadSize * aspect;


				//o.color.a = 1.0;

				o.texCoord = quadTexCoords[v.id];
			}

			struct fragOutput
			{
				fixed4 color : SV_Target;
			};


			fragOutput frag(v2f i)
			{
				fragOutput o;
				
				float albedo = tex2D(_AlbedoTex, i.texCoord).a;
				//o.color = fixed4(i.color);//fixed4(1.0, 1.0, 1.0, 1.0);//fixed4(i.color.xyz, albedo);
				o.color = fixed4(i.color.xyz, albedo);
				//o.color = fixed4(1.0, 1.0, 1.0, 1.0);
				return o;
			}
			ENDCG
		}
	}
}