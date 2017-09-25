
Shader "Unlit/SortingTest" { // defines the name of the shader 

	SubShader{ // Unity chooses the subshader that fits the GPU best
		Tags{ "QUEUE" = "Transparent" "IGNOREPROJECTOR" = "true" "RenderType" = "Transparent" }

		Pass{ // some shaders require multiple passes
			Tags{ "QUEUE" = "Transparent" "IGNOREPROJECTOR" = "true" "RenderType" = "Transparent" }

			GLSLPROGRAM

			#version 310 es

			#ifdef VERTEX

			/*const vec2 quadCoords[6] = {
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
			};*/

			const float inv6 = 1.0 / 6.0;
			const float inv255 = 1.0 / 255.0;

			uniform 	float aspect;
			uniform highp sampler2D _ColorTex;

			struct _Points_type {
				float[3] value;
			};

			layout(std430, binding = 0) readonly buffer _Points {
				_Points_type _Points_buf[];
			};

			layout(std430, binding = 1) readonly buffer _IndicesValues {
				uint _IndicesValues_buf[];
			};

			out highp vec3 vs_COLOR0;
			out highp vec2 vs_TEXCOORD0;

			void main()
			{
				float vertId = float(gl_VertexID);
				float quadId = vertId * inv6;
				uint value = _IndicesValues_buf[int(quadId)];

				uint quad_vertexID = uint(int(-6.0 * floor(quadId)) + int(gl_VertexID));

				uint index = value >> 8;

				float[3] positionArr = _Points_buf[index].value;
				vec4 position = vec4(positionArr[0], positionArr[1], positionArr[2], -1.0);
				float colorValue = float(value & uint(0xFF)) * inv255;

				vs_COLOR0 = textureLod(_ColorTex, vec2(pow((colorValue*2.0), .0625), 0.0), 0.0).xyz;
				
				gl_Position = gl_ModelViewProjectionMatrix * (-position);

				float size = 0.002;//0.02;
				vec2 quadSize = vec2(size, size * aspect);

				bool bit = (quad_vertexID == 1u) || (quad_vertexID == 4u);
				vec2 quadCoordsAndTexCoord = vec2(bit || (quad_vertexID == 2u), bit || (quad_vertexID == 3u));

				vec2 deltaSize = (quadCoordsAndTexCoord * 2.0 - 1.0) * quadSize;
				//vec2 deltaSize = quadCoords[quad_vertexID] * quadSize;

				gl_Position.xy += deltaSize;

				//vs_TEXCOORD0 = quadTexCoords[quad_vertexID];
				vs_TEXCOORD0 = quadCoordsAndTexCoord;
			}

			#endif
			#ifdef FRAGMENT	

			uniform highp sampler2D _AlbedoTex;

			in highp vec3 vs_COLOR0;
			in highp vec2 vs_TEXCOORD0;

			layout(location = 0) out highp vec4 SV_Target0;

			void main()
			{
				float albedo = textureLod(_AlbedoTex, vs_TEXCOORD0.xy, 0.0).a;
				if (albedo < 0.7) {
					discard; 
				}

				SV_Target0.xyz = vs_COLOR0;
				SV_Target0.w = albedo;
				
			}

			#endif


			ENDGLSL 

		}
	}
}