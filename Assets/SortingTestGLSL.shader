
Shader "Unlit / SortingTest" { // defines the name of the shader 

	/*Properties{
		_MainTex("Texture", 2D) = "white" { }
		_MainTex2("_MainTex2", 2D) = "white" { }
		_AlbedoTex("AlbedoTex", 2D) = "white" { }
		_ColorTex("_ColorTex", 2D) = "white" { }
	}*/

	SubShader{ // Unity chooses the subshader that fits the GPU best
		Tags{ "QUEUE" = "Transparent" "IGNOREPROJECTOR" = "true" "RenderType" = "Transparent" }
		Pass{ // some shaders require multiple passes
			Tags{ "QUEUE" = "Transparent" "IGNOREPROJECTOR" = "true" "RenderType" = "Transparent" }

			GLSLPROGRAM

			#version 430
			//#version 310 es

			#ifdef VERTEX
		 
			precision highp float;
			//precision highp vec2;
			//precision highp vec3;
			//precision highp sampler2D;

			precision highp int;

			//float ImmCB_0_0_1[6];

			const vec2 quadCoords[6] = {
				vec2(-0.1, -0.1),
				vec2(0.1, 0.1),
				vec2(0.1, -0.1),

				vec2(-0.1, 0.1),
				vec2(0.1, 0.1),
				vec2(-0.1, -0.1)
			};

			const vec2 quadTexCoords[6] = {
				vec2(0.0, 0.0),
				vec2(1.0, 1.0),
				vec2(1.0, 0.0),

				vec2(0.0, 1.0),
				vec2(1.0, 1.0),
				vec2(0.0, 0.0)
			};


			float ImmCB_0_0_0[6];
			vec2 ImmCB_0_0_2[6];

			const float inv6 = 1.0 / 6.0;
			const float inv255 = 1.0 / 255.0;


			uniform 	vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
			uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
			uniform 	float aspect;
			uniform lowp sampler2D _ColorTex;

			struct _Points_type {
				float[3] value;
			};

			layout(std430, binding = 0) readonly buffer _Points {
				_Points_type _Points_buf[];
			};

			layout(std430, binding = 1) readonly buffer _IndicesValues {
				uint _IndicesValues_buf[];
			};

			out mediump vec3 vs_COLOR0;
			out highp vec2 vs_TEXCOORD0;

			vec3 u_xlat0;
			uint u_xlatu0;
			vec2 u_xlat1;
			vec4 u_xlat2;
			vec4 u_xlat3;
			mediump vec2 u_xlat16_4;
			vec3 u_xlat5;
			uint u_xlatu5;
			uint u_xlatu10;

			void main()
			{
				float vertId = float(gl_VertexID);
				float quadId = vertId * inv6;
				uint value = _IndicesValues_buf[int(quadId)];

				uint quad_vertexID = int(-6.0 * floor(quadId)) + gl_VertexID;

				uint index = value >> 8;

				float[3] positionArr = _Points_buf[index].value;
				vec4 position = vec4(positionArr[0], positionArr[1], positionArr[2], -1.0);
				float colorValue = float(value & 0xFF) * inv255;

				vs_COLOR0 = textureLod(_ColorTex, vec2(pow((colorValue*2.0), .0625), 0.0), 0.0).xyz;
				
				gl_Position = gl_ModelViewProjectionMatrix * (-position);

				float size = 0.02;
				vec2 quadSize = vec2(size, size * aspect);
				vec2 deltaSize = quadCoords[quad_vertexID] * quadSize;

				gl_Position.xy += deltaSize;

				vs_TEXCOORD0 = quadTexCoords[quad_vertexID];
			}

			#endif
			#ifdef FRAGMENT			

			precision highp int;
			precision highp float;
			//precision highp sampler2D;

			uniform lowp sampler2D _AlbedoTex;
			in mediump vec3 vs_COLOR0;
			in highp vec2 vs_TEXCOORD0;

			layout(location = 0) out mediump vec4 SV_Target0;
			void main()
			{
				float albedo = textureLod(_AlbedoTex, vs_TEXCOORD0.xy, 0.0).a;
				if (albedo < 0.7) {
					discard; 
				}

				SV_Target0.xyz = vs_COLOR0.xyz;
				SV_Target0.w = albedo;
				return;
			}

			#endif


			ENDGLSL

		}
	}
}