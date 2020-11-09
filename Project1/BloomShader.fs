#version 330 core
out vec4 FragColor;

// inputs to shader
// Resolution of the screen
uniform vec2 u_resolution;

uniform sampler2D scene;
uniform sampler2D bloomBlur;
uniform bool use_bloom;

void main()
{             
    vec2 uv = (gl_FragCoord.xy/u_resolution);

    const float gamma = 2.2;
    vec3 hdrColor = texture(scene, uv).rgb;      
    
    if(use_bloom){
        vec3 bloomColor = texture(bloomBlur, uv).rgb;
        hdrColor += bloomColor; 
    }
    
    FragColor = vec4(hdrColor, 1.0);
}