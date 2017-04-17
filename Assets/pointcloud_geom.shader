// Upgrade NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'

Shader "Geom/PointCloud" {
Properties{
        //point_size("Point Size", Float) = 5000.0
		_MainTex("Texture", 2D) = "white" {}
		_AlbedoTex("AlbedoTex", 2D) = "white" {}
		_FrameTime("_FrameTime", Int) = 0
		//_Pos("_Pos", Vector) = (0.0, 1.0, 0.0, 1.0)
}
  SubShader {
	 /*Tags
	 {
			"RenderType" = "Transparent" 
	 
	 }*/
	Tags{"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
	//Blend SrcAlpha OneMinusSrcAlpha
	//AlphaTest Greater .01
	//Cull Off
	//ZWrite off


	 LOD 100

     Pass {
        CGPROGRAM

		//#define UNITY_MAX_INSTANCE_COUNT 1000000
		//#pragma instancing_options maxcount:1000000

        #pragma vertex vert
        #pragma fragment frag
		#pragma geometry geom

		//#pragma multi_compile_instancing
       
		#pragma target 5.0

        #include "UnityCG.cginc"

        struct appdata
        {
           float4 vertex : POSITION;
		   //float4 color : COLOR;
		   uint id : SV_VertexID;
		   //uint id : SV_InstanceID;
		   //UNITY_INSTANCE_ID
        };

        struct v2f
        {
           float4 vertex : SV_POSITION;
           float4 color : COLOR;
		   float4 texCoord : TEXCOORD0;
           //float size : PSIZE;
		   //UNITY_INSTANCE_ID
        };

		//UNITY_INSTANCING_CBUFFER_START(Props)
		//UNITY_DEFINE_INSTANCED_PROP(float4, _Pos)
		//UNITY_INSTANCING_CBUFFER_END
       
        float4x4 depthCameraTUnityWorld;
        //float point_size;
		sampler2D _MainTex;
		sampler2D _AlbedoTex;
		int _FrameTime;
       
        v2f vert (appdata v)
        {
           v2f o;
		   UNITY_INITIALIZE_OUTPUT(v2f, o)

		   //UNITY_SETUP_INSTANCE_ID(v);
		   //UNITY_TRANSFER_INSTANCE_ID(v, o);

		   //float4 a = float4(0.0, 0.0, 0.0, 1.0);
		   //float4 instancePos = UNITY_ACCESS_INSTANCED_PROP(_Pos);

           //o.vertex = mul(UNITY_MATRIX_MVP, v.vertex +/*v.vertex*/);
		   //o.vertex = UnityObjectToClipPos(v.vertex + instancePos);
		   o.vertex = UnityObjectToClipPos(v.vertex);
		   //o.vertex = v.vertex;

		   float texSize = 4096.0;
		   float vertexId = float(v.id + _FrameTime);

		   float2 texCoords = float2(vertexId % texSize, vertexId / texSize) / texSize;
		   o.color.rgba = tex2Dlod(_MainTex, float4(texCoords, 0, 0)).rgba;
		   
		   o.texCoord = float4(0.0, 0.0, 0.0, 0.0);

           return o;
        }

		[maxvertexcount(4)]
		void geom(point v2f input[1], inout TriangleStream<v2f> OutputStream)
		{ 
			v2f test = (v2f)0;
			//float3 normal = normalize(cross(input[1].worldPosition.xyz - input[0].worldPosition.xyz, input[2].worldPosition.xyz - input[0].worldPosition.xyz));
			test.color = input[0].color;
			test.texCoord = input[0].texCoord;

			/*if (input[0].color.a < 0.25)
			{
				return;
			}*/

			float halfSideLength = 0.0025; //* pow(input[0].color.a, 3.0); /// (input[0].color.b*5.0 + 0.1);

			/*if (halfSideLength < 0.001)
			{
				return;
			}*/

			

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



		}
       
		struct fragOutput
		{
			fixed4 color : SV_Target;
		};


		fragOutput frag(v2f i)
		{
			fragOutput o;

			float albedo = tex2D(_AlbedoTex, i.texCoord).a;
			o.color = fixed4(1.0,1.0,1.0,1.0);//fixed4(i.color.xyz, albedo);
			return o;
		}

        /*fixed4 frag (v2f i) : SV_Target
        {
			//UNITY_SETUP_INSTANCE_ID(i);
			//return UNITY_ACCESS_INSTANCED_PROP(_Pos);

			float albedo = tex2D(_AlbedoTex, i.texCoord).a;
			//if (albedo <= 0.5)
			//{
			//	clip(-1.0);
			//	return 1.0;
			//}

			//return fixed4(i.color.xyz + albedo, 0.5);
			//return fixed4(albedo, 0.5);
			return fixed4(i.color.xyz, albedo);
        }*/
        ENDCG
     }
  }
}

