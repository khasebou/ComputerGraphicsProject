#version 330 core
layout (location = 0) in vec3 aPos;

// I used https://learnopengl.com/Advanced-Lighting/Bloom to learn about what bloom filtering is and how to implement it
void main()
{
    gl_Position = vec4(aPos, 1.0);
}