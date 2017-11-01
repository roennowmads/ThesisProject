// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Unlit/SortingTest" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
		_MainTex2("_MainTex2", 2D) = "white" {}
		_AlbedoTex("AlbedoTex", 2D) = "white" {}
		_ColorTex("_ColorTex", 2D) = "white" {}
	}
	SubShader {
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		//Tags{"RenderType" = "Opaque"}
		Blend SrcAlpha OneMinusSrcAlpha
		//Blend One OneMinusSrcAlpha		
		Cull Off
		ZWrite Off
		//LOD 3000
		//AlphaToMask Off

		Pass {
			CGPROGRAM

			#define PENTAGON
			//#define QUAD
			//#define SIXVERTEXQUAD

			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom

			#include "UnityCG.cginc"
			#pragma target es3.1

			sampler2D _ColorTex;
			sampler2D _AlbedoTex;

			//StructuredBuffer<float4> _CoordsTex;

			//Texture2D _CoordsTex;


			StructuredBuffer<float3> _Points;
			StructuredBuffer<uint> _IndicesValues;

			//RasterizerOrderedBuffer<float3> _ROV1;

			uniform float aspect;
			uniform float4 pentagonParams;
			uniform uint _Magnitude;
			uniform int _TextureSwitchFrameNumber;
			uniform float pointSizeScale;

			struct appdata
			{
				uint id : SV_VertexID;
			};

			/*struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed3 color : COLOR;
				float2 texCoord : TEXCOORD0;
				//float colorValue : TEXCOORD1;
			};*/

			struct v2g
			{
				float4 vertex : SV_POSITION;
				fixed3 color : COLOR;
			};

			struct g2f
			{
				float4 vertex : SV_POSITION;
				fixed3 color : COLOR;
				float2 texCoord : TEXCOORD0;
			};

			/*static const half4 quadCoordsAndTexCoords[6] = {
				half4(-1.0, -1.0, 0.0, 0.0),
				half4(1.0, 1.0, 1.0, 1.0),
				half4(1.0, -1.0, 1.0, 0.0),

				half4(-1.0, 1.0, 0.0, 1.0),
				half4(1.0, 1.0, 1.0, 1.0),
				half4(-1.0, -1.0, 0.0, 0.0)
			};*/


			/*static const half2 quadCoordsAndTexCoords[6] = {
				half2(0.0, 0.0),
				half2(1.0, 1.0),
				half2(1.0, 0.0),

				half2(0.0, 1.0),
				half2(1.0, 1.0),
				half2(0.0, 0.0)
			};*/


			//0,1,2,3,4,5   // requires 3 bits
			
			//1st component:
			//0,1,1,0,1,0

			//2nd component:
			//0,1,0,1,1,0

			/*if !1st & !2nd & !3rd  -> 0  //0

			if 1st & !2nd & !3rd -> 1    //1

			if !1st & 2nd & !3rd -> 1    //2

			if 1st & 2nd & !3rd -> 0     //3

			if !1st & !2nd & 3rd -> 1    //4

			if 1st & !2nd & 3rd -> 0     //5


			001
			010
			100

				quad_vertexID & 1 | quad_vertexID & 2 | quad_vertexID & 4*/

			//((quad_vertexID << 3) >> 3 & 1 | (quad_vertexID << 2) & 16 | (quad_vertexID << 3) & 4

			/*static const int bitCoords[6] = {
				0, 3, 2, 1, 3, 0
			};*/

			//half2 quadCoordsAndTexCoord = half2(half((bitCoord & 2) >> 1), half(bitCoord & 1));

			/*static const half2 quadCoords[6] = {
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
			};*/

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

			void vert(in appdata v, out v2g o)
			{
				UNITY_INITIALIZE_OUTPUT(v2g, o)				
				uint value = v.id;//_IndicesValues[v.id];

				uint index = value /*>> 8*/;
				float4 position = float4(-_Points[index], 1.0);
				o.vertex = UnityObjectToClipPos(position);

				float colorValue = 0.5;//(value & 0xFF) * inv255;
				o.color = tex2Dlod(_ColorTex, half4(pow((colorValue), .0625), 0, 0, 0)).rgb /** modifier*/;
			}

			// Geometry Shader -----------------------------------------------------
			//[maxvertexcount(6)]
		#if defined(QUAD)
			[maxvertexcount(4)]
		#else
			[maxvertexcount(5)]
		#endif
			void geom(point v2g p[1], inout TriangleStream<g2f> triStream)
			{
				half size = pointSizeScale*0.345;//0.352;
				half2 quadSize = half2(size, size * aspect);
				half2 quadSizeDouble = quadSize * 2.0;

				g2f pIn;

				pIn.color = p[0].color;
				pIn.vertex.zw = p[0].vertex.zw;

			#ifdef SIXVERTEXQUAD
				float2 v0 = p[0].vertex.xy - quadSize.xy;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.0, 0.0);
				triStream.Append(pIn);

				v0.xy += quadSizeDouble;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(1.0, 1.0);
				triStream.Append(pIn);

				v0.y -= quadSizeDouble.y;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(1.0, 0.0);
				triStream.Append(pIn);

				triStream.RestartStrip();

				v0.x -= quadSizeDouble.x;
				v0.y += quadSizeDouble.y;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.0, 1.0);
				triStream.Append(pIn);

				v0.x += quadSizeDouble.x;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(1.0, 1.0);
				triStream.Append(pIn);

				v0.xy -= quadSizeDouble;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.0, 0.0);
				triStream.Append(pIn);
			#endif

			#ifdef QUAD
				float2 v0 = float2(p[0].vertex.x + quadSize.x, p[0].vertex.y - quadSize.y);
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(1.02, -0.015);
				triStream.Append(pIn);

				v0.y += quadSizeDouble.y;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(1.02, 0.985);
				triStream.Append(pIn);

				v0 -= quadSizeDouble;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.02, -0.015);
				triStream.Append(pIn);

				v0.y += quadSizeDouble.y;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.02, 0.985);
				triStream.Append(pIn);
			#endif

			#ifdef PENTAGON
				//vertex coordinates found here: http://mathworld.wolfram.com/Pentagon.html
				//float radius = 0.87;//0.8725;//0.65;//0.9;//0.8725;  //nvidia tablet closer to: 0.65

				//float radius = pointSizeScale*0.87; //Tango device
				//float radius = 0.77; //single particle view
				float radius = pointSizeScale*0.785; //Tegra tablet
				
				//0.8725;//0.65;//0.9;//0.8725;  //nvidia tablet closer to: 0.65


				pentagonParams *= radius;

				//texture coordinates found here: http://manual.starling-framework.org/en/img/pentagon-texcoords.png
				float2 v0 = float2(p[0].vertex.x, p[0].vertex.y - radius * 0.85);
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.515, 1.0);
				triStream.Append(pIn);

				v0 = float2(p[0].vertex.x + pentagonParams.z, p[0].vertex.y - pentagonParams.x);
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(1.015, 0.66);
				triStream.Append(pIn);

				v0 = p[0].vertex.xy - pentagonParams.zx;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.015, 0.66);
				triStream.Append(pIn);

				v0 = p[0].vertex.xy + pentagonParams.wy;
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.815, 0.06);
				triStream.Append(pIn);

				v0 = float2(p[0].vertex.x - pentagonParams.w, p[0].vertex.y + pentagonParams.y);
				pIn.vertex.xy = v0;
				pIn.texCoord = float2(0.215, 0.06);
				triStream.Append(pIn);
			#endif
			}


			struct fragOutput
			{
				fixed4 color : SV_Target;
			};


			fragOutput frag(g2f i)
			{
				fragOutput o;

				//fixed3 color = tex2D(_ColorTex, half2(i.colorValue, 0)).rgb;


				fixed albedo = tex2Dlod(_AlbedoTex, float4(i.texCoord, 0.0, 0.0) /*float2(0.5,0.5)*/).a;
				//fixed albedo = tex2D(_AlbedoTex, i.texCoord /*float2(0.5,0.5)*/).a;
				
				//float3 a = _ROV1[0];

			#if defined(QUAD)
				if (albedo < 0.325) {  //for quad
			#elif defined(PENTAGON)
				if (albedo < 0.43) { //for pentagon
			#endif
					discard;
					//o.color = fixed4(fixed3(0.0,1.0,0.0), 0.1/*albedo*//**0.25*/);
				}
				else {
			#if defined(QUAD)
					o.color = fixed4(i.color/*color*/, 0.5/*albedo*0.25*//*albedo*//**0.25*/);
			#elif defined(PENTAGON)
					o.color = fixed4(i.color/*color*/, 0.5/*albedo*0.225*//*albedo*//**0.25*/);
			#endif
					
				}
			


				//if (albedo < 0.7) 
				//	discard;

				//o.color = fixed4(/*i.color*/fixed3(0.5,0.1,0.1), albedo*0.0525);

				//_ROV1[0] = a + float3(1.0, 1.0, 1.0);

				//o.color = fixed4(i.color/*color*/, albedo/*albedo*//**0.25*/);
				return o;
			}
			ENDCG
		}
	}
}