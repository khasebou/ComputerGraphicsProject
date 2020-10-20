#version 330 core
out vec4 FragColor;

// inputs to shader
// Resolution of the screen
uniform vec2 u_resolution;

uniform sampler2D scene;
uniform sampler2D bloomBlur;
uniform float exposure;

void main()
{             
    vec2 uv = (gl_FragCoord.xy/u_resolution);

    const float gamma = 2.2;
    vec3 hdrColor = texture(scene, uv).rgb;      
    vec3 bloomColor = texture(bloomBlur, uv).rgb;
    
    hdrColor += bloomColor; // additive blending
    // tone mapping
    //vec3 result = vec3(1.0) - exp(-hdrColor * 0.6);
    // also gamma correct while we're at it       
    //result = pow(result, vec3(1.0 / gamma));
    
    FragColor = vec4(hdrColor, 1.0);
}