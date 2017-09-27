
Shader "Unlit/AccumShader"
{
	SubShader
	{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Accumulate" }
		Cull Off
		ZWrite Off
		Blend 0 One One
		Blend 1 Zero OneMinusSrcColor

		Pass
		{
			GLSLPROGRAM
			#version 310 es

			#ifdef VERTEX
			//#version 310 es

			/*const vec4 quadCoordsAndTexCoords[6] = vec4[6] (
				vec4(-1.0, -1.0, 0.0, 0.0),
				vec4(1.0, 1.0, 1.0, 1.0),
				vec4(1.0, -1.0, 1.0, 0.0),

				vec4(-1.0, 1.0, 0.0, 1.0),
				vec4(1.0, 1.0, 1.0, 1.0),
				vec4(-1.0, -1.0, 0.0, 0.0)
			);*/

			/*const vec2 quadCoordsAndTexCoords[6] = vec2[6] (
				vec2(0.0, 0.0),
				vec2(1.0, 1.0),
				vec2(1.0, 0.0),

				vec2(0.0, 1.0),
				vec2(1.0, 1.0),
				vec2(0.0, 0.0)
			);*/

			uniform 	float aspect;
			uniform lowp sampler2D _ColorTex;
			struct _Points_type {
				float[3] value;
				//float value1;
				//float value2;
				//float value3;
				//vec3 value;
			};

			layout(std430, binding = 0) readonly buffer _Points {
				_Points_type _Points_buf[];
			};

			layout(std430, binding = 1) readonly buffer _IndicesValues {
				uint _IndicesValues_buf[];
			};
			out mediump vec3 vs_COLOR0;
			out highp vec2 vs_TEXCOORD0;

			const float inv6 = 1.0 / 6.0;
			const float inv255 = 2.0 / 255.0;

			vec2 colorCoords = vec2(0.0);

			void main()
			{
				int quadId = int(float(gl_VertexID) * inv6);
				int quad_vertexID = -6 * quadId + gl_VertexID;

				bvec4 bits = equal(ivec4(quad_vertexID), ivec4(1, 4, 2, 3));
				bool bit = bits.x || bits.y;
				vec2 quadCoordsAndTexCoord = vec2(bit || bits.z, bit || bits.w);

				//vec2 quadCoordsAndTexCoord = quadCoordsAndTexCoords[quad_vertexID];
				//vec4 quadCoordsAndTexCoord = quadCoordsAndTexCoords[quad_vertexID];
				
				float size = 0.001;//0.02;
				vec2 quadSize = vec2(size, size * aspect);

				vec2 deltaSize = (quadCoordsAndTexCoord * 2.0 - 1.0) * quadSize;
				//vec2 deltaSize = quadCoordsAndTexCoord.xy * quadSize;

				uint value = _IndicesValues_buf[quadId];

				uint index = value >> 8u;
				float colorValue = float(value & 255u) * inv255;


				//vec3 pos =  vec3(_Points_buf[index].value);
				//vec3 pos =  vec3(_Points_buf[index].value1, _Points_buf[index].value2, _Points_buf[index].value3); 

				vec4 position = /*vec4(pos, -1.0);*/vec4(_Points_buf[index].value[0], _Points_buf[index].value[1], _Points_buf[index].value[2], -1.0);

				vs_TEXCOORD0 = quadCoordsAndTexCoord;
				//vs_TEXCOORD0 = quadCoordsAndTexCoord.zw;
				
				gl_Position = gl_ModelViewProjectionMatrix * (-position);
				gl_Position.xy += deltaSize;

				colorCoords.x = pow((colorValue), .0625);
				vs_COLOR0 = textureLod(_ColorTex, colorCoords, 0.0).xyz;
			}

			#endif
			#ifdef FRAGMENT	

			uniform highp sampler2D _AlbedoTex;

			//layout(early_fragment_tests) in;

			in mediump vec3 vs_COLOR0;
			in highp vec2 vs_TEXCOORD0;

			layout(location = 0) out mediump vec4 SV_Target0;
			layout(location = 1) out mediump float SV_Target1;

			float w(float z, float alpha) {
			//#ifdef _WEIGHTED0
			//return pow(z, -2.5);
			//#elif _WEIGHTED1
				//return alpha * max(1e-2, min(3e3, 10.0 / (1e-5 + pow(z / 5, 2) + pow(z / 200, 6))));
			//#elif _WEIGHTED2
			//	return alpha * max(1e-2, min(3e3, 0.03 / (1e-5 + pow(z / 200, 4))));
			//#endif
			//	return 1.0;

			//  return clamp(pow(min(1.0, alpha * 10.0) + 0.01, 3.0) * 1e8 * pow(1.0 - z * 0.9, 3.0), 1e-2, 3e3);

			//  return pow(z, -2.5);

				//from Phenomenological Transparency paper:
				return min(max(pow(10.0*(1.0 - 0.99 * z) * alpha, 3.0), 0.01), 30.0); 
			}

			void main()
			{
				float alpha = textureLod(_AlbedoTex, vs_TEXCOORD0.xy, 0.0).a;
				if (alpha < 0.7) {
					discard; 
				}

				vec3 C = vs_COLOR0 * alpha;

				//#ifdef _WEIGHTED_ON
				SV_Target0 = vec4(C, alpha) * w(gl_FragCoord.z, alpha);
				//#else
				//	o.col0 = half4(C, alpha);
				//#endif

				SV_Target1 = alpha;
			}

			#endif


			ENDGLSL 

		}
	}
}
