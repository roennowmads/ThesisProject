Shader "Instanced/pointCloud_Inst" {

	Properties {
		_MainTex("Texture", 2D) = "white" {}
		_AlbedoTex("AlbedoTex", 2D) = "white" {}
		_FrameTime("_FrameTime", Int) = 0
		_Pos("_Pos", Vector) = (0.0, 0.0, 0.0, 0.0)
	}
	SubShader {
		//Tags{"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
		Tags {"RenderType" = "Opaque"}
		Blend SrcAlpha OneMinusSrcAlpha
		AlphaTest Greater .01
		//Cull Off
		ZWrite off
		LOD 200
		
		Pass{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// Enable instancing for this shader
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"

			// Config maxcount. See manual page.
			// #pragma instancing_options

			//float4x4 depthCameraTUnityWorld;
			//float point_size;
			sampler2D _MainTex;
			sampler2D _AlbedoTex;
			int _FrameTime;

			struct appdata
			{
				float4 vertex : POSITION;
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
			UNITY_INSTANCING_CBUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(float4, _Pos)	// Make _Color an instanced property (i.e. an array)
			UNITY_INSTANCING_CBUFFER_END

			v2f vert(appdata v)
			{
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f, o)

				UNITY_SETUP_INSTANCE_ID(v);
				//UNITY_TRANSFER_INSTANCE_ID(v, o);

				//float4 a = float4(0.0, 0.0, 0.0, 1.0);
				float4 instancePos = UNITY_ACCESS_INSTANCED_PROP(_Pos);

				//o.vertex = mul(UNITY_MATRIX_MVP, v.vertex +/*v.vertex*/);
				o.vertex = UnityObjectToClipPos(v.vertex + instancePos /*+ float4(0.5 * float(v.iid), 0.0, 0.0, 0.0)*/);
				//o.vertex = UnityObjectToClipPos(v.vertex);
			
				float texSize = 4096.0;
				float vertexId = float(v.id + _FrameTime);

				float2 texCoords = float2(vertexId % texSize, vertexId / texSize) / texSize;
				o.color.rgba = tex2Dlod(_MainTex, float4(texCoords, 0, 0)).rgba;
				o.color.a = 1.0;

				o.texCoord = float4(0.0, 0.0, 0.0, 0.0);

				return o;
			}

			/*[maxvertexcount(4)]
			void geom(point v2f input[1], inout TriangleStream<v2f> OutputStream)
			{
				v2f test = (v2f)0;
				test.color = input[0].color;
				test.texCoord = input[0].texCoord;


				float halfSideLength = 0.0025; 

				float4 vert = input[0].vertex;
				vert.xy += halfSideLength;
				test.vertex = vert;
				test.texCoord.xy = float2(1.0, 1.0);
				OutputStream.Append(test);

				vert = input[0].vertex;
				vert.x += halfSideLength;
				vert.y -= halfSideLength;
				test.vertex = vert;
				test.texCoord.xy = float2(1.0, 0.0);
				OutputStream.Append(test);

				vert = input[0].vertex;
				vert.x -= halfSideLength;
				vert.y += halfSideLength;
				test.vertex = vert;
				test.texCoord.xy = float2(0.0, 1.0);
				OutputStream.Append(test);

				vert = input[0].vertex;
				vert.xy -= halfSideLength;
				test.vertex = vert;
				test.texCoord.xy = float2(0.0, 0.0);
				OutputStream.Append(test);
			}*/

			struct fragOutput
			{
				fixed4 color : SV_Target;
			};


			fragOutput frag(v2f i)
			{
				fragOutput o;

				float albedo = tex2D(_AlbedoTex, i.texCoord).a;
				o.color = fixed4(1.0, 1.0, 1.0, 1.0);//fixed4(i.color.xyz, albedo);
				return o;
			}
			ENDCG
		}
	}
}