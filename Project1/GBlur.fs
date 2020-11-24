#version 330 core
out vec4 FragColor;

uniform vec2 u_resolution;
uniform sampler2D image;

uniform bool horizontal;
uniform float weight[5] = float[] (0.2270270270, 0.1945945946, 0.1216216216, 0.0540540541, 0.0162162162);

// I used https://learnopengl.com/Advanced-Lighting/Bloom to learn about what bloom filtering is and how to implement it
void main()
{             
    vec2 TexCoords = (gl_FragCoord.xy/u_resolution);

    vec2 tex_offset = 1.0 / textureSize(image, 0); // gets size of single texel
    vec3 result = texture(image, TexCoords).rgb * weight[0];
    if(horizontal)
    {
        for(int i = 1; i < 5; ++i)
        {
        result += texture(image, TexCoords + vec2(tex_offset.x * i, 0.0)).rgb * weight[i];
        result += texture(image, TexCoords - vec2(tex_offset.x * i, 0.0)).rgb * weight[i];
        }
    }
    else
    {
        for(int i = 1; i < 5; ++i)
        {
            result += texture(image, TexCoords + vec2(0.0, tex_offset.y * i)).rgb * weight[i];
            result += texture(image, TexCoords - vec2(0.0, tex_offset.y * i)).rgb * weight[i];
        }
    }
     FragColor = vec4(result, 1.0);
}