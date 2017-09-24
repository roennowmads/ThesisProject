
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

			#ifdef VERTEX
			//#version 430
			//#version 310 es
		 
			precision highp float;
			//precision highp vec2;
			//precision highp vec3;
			//precision highp sampler2D;

			precision highp int;

			float ImmCB_0_0_1[6];
			float ImmCB_0_0_0[6];
			vec2 ImmCB_0_0_2[6];


			uniform 	vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
			uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
			uniform 	float aspect;
			uniform lowp sampler2D _ColorTex;

			struct _Points_type {
				uint[3] value;
			};

			layout(std430, binding = 0) readonly buffer _Points {
				_Points_type _Points_buf[];
			};
			struct _IndicesValues_type {
				uint[1] value;
			};

			layout(std430, binding = 1) readonly buffer _IndicesValues {
				_IndicesValues_type _IndicesValues_buf[];
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
				ImmCB_0_0_1[0] = -0.100000001;
				ImmCB_0_0_1[1] = 0.100000001;
				ImmCB_0_0_1[2] = -0.100000001;
				ImmCB_0_0_1[3] = 0.100000001;
				ImmCB_0_0_1[4] = 0.100000001;
				ImmCB_0_0_1[5] = -0.100000001;
				ImmCB_0_0_0[0] = -0.100000001;
				ImmCB_0_0_0[1] = 0.100000001;
				ImmCB_0_0_0[2] = 0.100000001;
				ImmCB_0_0_0[3] = -0.100000001;
				ImmCB_0_0_0[4] = 0.100000001;
				ImmCB_0_0_0[5] = -0.100000001;
				ImmCB_0_0_2[0] = vec2(0.0, 0.0);
				ImmCB_0_0_2[1] = vec2(1.0, 1.0);
				ImmCB_0_0_2[2] = vec2(1.0, 0.0);
				ImmCB_0_0_2[3] = vec2(0.0, 1.0);
				ImmCB_0_0_2[4] = vec2(1.0, 1.0);
				ImmCB_0_0_2[5] = vec2(0.0, 0.0);
				u_xlat0.x = float(uint(gl_VertexID));
				u_xlat5.x = u_xlat0.x * 0.166666672;
				u_xlatu10 = uint(u_xlat5.x);
				u_xlat5.x = floor(u_xlat5.x);
				u_xlat0.x = u_xlat5.x * -6.0 + u_xlat0.x;
				u_xlatu0 = uint(u_xlat0.x);
				u_xlatu5 = _IndicesValues_buf[u_xlatu10].value[(0 >> 2) + 0];
				u_xlatu10 = u_xlatu5 >> 8u;
				u_xlatu5 = u_xlatu5 & 255u;
				u_xlat5.x = float(u_xlatu5);
				u_xlat5.x = u_xlat5.x * 0.00784313772;
				u_xlat5.x = log2(u_xlat5.x);
				u_xlat5.x = u_xlat5.x * 0.0625;
				u_xlat1.x = exp2(u_xlat5.x);
				u_xlat5.xyz = vec3(uintBitsToFloat(_Points_buf[u_xlatu10].value[(0 >> 2) + 0]), uintBitsToFloat(_Points_buf[u_xlatu10].value[(0 >> 2) + 1]), uintBitsToFloat(_Points_buf[u_xlatu10].value[(0 >> 2) + 2]));
				u_xlat2 = (-u_xlat5.yyyy) * hlslcc_mtx4x4unity_ObjectToWorld[1];
				u_xlat2 = hlslcc_mtx4x4unity_ObjectToWorld[0] * (-u_xlat5.xxxx) + u_xlat2;
				u_xlat2 = hlslcc_mtx4x4unity_ObjectToWorld[2] * (-u_xlat5.zzzz) + u_xlat2;
				u_xlat2 = u_xlat2 + hlslcc_mtx4x4unity_ObjectToWorld[3];
				u_xlat3 = u_xlat2.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
				u_xlat3 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat2.xxxx + u_xlat3;
				u_xlat3 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat2.zzzz + u_xlat3;
				u_xlat2 = hlslcc_mtx4x4unity_MatrixVP[3] * u_xlat2.wwww + u_xlat3;
				u_xlat5.x = aspect * 0.0199999996;
				u_xlat16_4.y = u_xlat5.x * ImmCB_0_0_1[int(u_xlatu0)];
				u_xlat16_4.x = 0.0199999996 * ImmCB_0_0_0[int(u_xlatu0)];
				vs_TEXCOORD0.xy = ImmCB_0_0_2[int(u_xlatu0)].xy;
				gl_Position.xy = u_xlat2.xy + u_xlat16_4.xy;
				gl_Position.zw = u_xlat2.zw;
				u_xlat1.y = 0.0;
				u_xlat0.xyz = textureLod(_ColorTex, u_xlat1.xy, 0.0).xyz;
				vs_COLOR0.xyz = u_xlat0.xyz;
				return;

			}

			#endif
			#ifdef FRAGMENT
			//#version 430
			//#version 310 es

			precision highp int;
			precision highp float;
			//precision highp sampler2D;

			uniform lowp sampler2D _AlbedoTex;
			in mediump vec3 vs_COLOR0;
			in highp vec2 vs_TEXCOORD0;
			layout(location = 0) out mediump vec4 SV_Target0;
			float u_xlat0;
			bool u_xlatb1;
			void main()
			{
				u_xlat0 = textureLod(_AlbedoTex, vs_TEXCOORD0.xy, 0.0).w;
				u_xlatb1 = u_xlat0 < 0.699999988;
				if ((int(u_xlatb1) * int(0xffffffffu)) != 0) { discard; }
				SV_Target0.xyz = vs_COLOR0.xyz;
				SV_Target0.w = u_xlat0;
				return;
			}

			#endif


			ENDGLSL

		}
	}
}