﻿#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 1) in vec3 vsin_center;
layout(location = 2) in vec2 vsin_uv;
layout(location = 3) in vec4 vsin_color;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out vec4 fsin_color;

layout(set = 0, binding = 2) uniform Projection { mat4 projection; };
layout(set = 0, binding = 3) uniform View { mat4 view; };
layout(set = 0, binding = 4) uniform World { mat4 world; };
layout (set = 0, binding = 5) uniform MaterialUniforms
{
	vec4 tint;
	float vectorColorFactor;
	float tintFactor;
	float alphaReference;
	bool isBillboard;
};

void main()
{
	vec4 pos;
	if (isBillboard)
		pos = world * view * vec4(vsin_center, 1) + vec4(vsin_pos, 1);
	else
		pos = world * view * vec4(vsin_center + vsin_pos, 1);
	pos = projection * pos;
	gl_Position = pos;
	fsin_uv = vsin_uv;
	fsin_color = vsin_color;
}
