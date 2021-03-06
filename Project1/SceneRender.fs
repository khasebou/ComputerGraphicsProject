#version 330 core


// Tampere University
// COMP.CE.430 Computer Graphics Coding Assignment 2020
//
// Write your name and student id here:
//   KARIM HASEBOU, 050366668
//
// Mark here with an X which functionalities you implemented.
// Note that different functionalities are worth different amount of points.
//
// Name of the functionality      |Done| Notes
//-------------------------------------------------------------------------------
// example functionality          | X  | Example note: control this with var YYYY
// Mandatory functionalities ----------------------------------------------------
//   Perspective projection       | X  | 
//   Phong shading                | X  | 
//   Camera movement and rotation | X  |  'rotation using mouse and movement WSAD keys' though movement direction does not take into consideration camera orientation (IE W will always move in Z direction regardless of camera orientation)
//   Sharp shadows                | X  | 
// Extra functionalities --------------------------------------------------------
//   Soft shadows                 |  X |  Enable by pressing 'E' in the demo
//   Sharp reflections            |  X | 
//   Refractions                  |  X | 
//   Texturing                    |  x | 
//   BLOOM                        | X  | Enable by pressing 'R' in the demo
//   OWN shader                   | X  |
//   Extra basic shape TORUS      | X  |
//   Animated Shape (smooth union)| X  |
//   Complex shape (mandelbrot)   | X  |
// constants

#define PI 3.14159265359
#define EPSILON 0.00001

// These definitions are tweakable.

/* Minimum distance a ray must travel. Raising this value yields some performance
 * benefits for secondary rays at the cost of weird artefacts around object
 * edges.
 */
#define MIN_DIST 0.08
/* Maximum distance a ray can travel. Changing it has little to no performance
 * benefit for indoor scenes, but useful when there is nothing for the ray
 * to intersect with (such as the sky in outdoors scenes).
 */
#define MAX_DIST 20.0
/* Maximum number of steps the ray can march. High values make the image more
 * correct around object edges at the cost of performance, lower values cause
 * weird black hole-ish bending artefacts but is faster.
 */
#define MARCH_MAX_STEPS 128
/* Typically, this doesn't have to be changed. Lower values cause worse
 * performance, but make the tracing stabler around slightly incorrect distance
 * functions.
 * The current value merely helps with rounding errors.
 */
#define STEP_RATIO 0.99//0.9999
/* Determines what distance is considered close enough to count as an
 * intersection. Lower values are more correct but require more steps to reach
 * the surface
 */
#define HIT_RATIO 0.001


// inputs to shader
// Resolution of the screen
uniform vec2 u_resolution;
// Mouse coordinates
uniform vec2 u_mouse;
// Time since startup, in seconds
uniform float u_time;
// turns on soft_shadows instead of sharp shadows
uniform bool use_soft_shadows;
// texture for the floor
uniform sampler2D marbleFloorTexture;


// materials
struct material
{
	// The color of the surface
	vec4 color;
	// PHONG shading params
	vec3 diffuse;
	vec3 specular;
	float diffuse_intensity;
	float specular_intensity;
	float shininess;
    // reflection
    float reflectedPortion;
    //refraction params
    float refracIndex;
    vec3 colorAbsorbtion;
    bool is_transparent;
};


// Good resource for finding more building blocks for distance functions:
// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

/* Basic box distance field.
 *
 * Parameters:
 *  p   Point for which to evaluate the distance field
 *  b   "Radius" of the box
 *
 * Returns:
 *  Distance to the box from point p.
 */
float box(vec3 p, vec3 b)
{
    vec3 d = abs(p) - b;
    return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,0.0));
}

/* Rotates point around origin along the X axis.
 *
 * Parameters:
 *  p   The point to rotate
 *  a   The angle in radians
 *
 * Returns:
 *  The rotated point.
 */
vec3 rot_x(vec3 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return vec3(
        p.x,
        c*p.y-s*p.z,
        s*p.y+c*p.z
    );
}

/* Rotates point around origin along the Y axis.
 *
 * Parameters:
 *  p   The point to rotate
 *  a   The angle in radians
 *
 * Returns:
 *  The rotated point.
 */
vec3 rot_y(vec3 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return vec3(
        c*p.x+s*p.z,
        p.y,
        -s*p.x+c*p.z
    );
}


/* Rotates point around origin along the Z axis.
 *
 * Parameters:
 *  p   The point to rotate
 *  a   The angle in radians
 *
 * Returns:
 *  The rotated point.
 */
vec3 rot_z(vec3 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return vec3(
        c*p.x-s*p.y,
        s*p.x+c*p.y,
        p.z
    );
}


/* UV sampling
*/
vec2 getUV(vec3 p, vec3 n){ 
    vec3 m = abs(n);      

    if(m.x >= m.y && m.x >= m.z){ 
        return p.yz*0.25; 
    } 
    else if(m.y > m.x && m.y >= m.z){ 
        return p.xz*0.25; 
    } 
    else{ 
        return p.xy*0.25; 
    } 
} 

/* Each object has a distance function and a material function. The distance
 * function evaluates the distance field of the object at a given point, and
 * the material function determines the surface material at a point.
 */

float opSmoothUnion( float d1, float d2, float k )
{
    float h = max(k-abs(d1-d2),0.0);
    return min(d1, d2) - h*h*0.25/k;
}

float box_distance( vec3 p, vec3 loc, vec3 b )
{
    p = p - loc;
    vec3 q = abs(p) - b;
    return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
}


// used art of code tutorial for custom shapes:
// https://www.youtube.com/watch?v=Ff0jJyyiVyw&t=893s
float torus_distance( vec3 p )
{
    vec2 t = vec2(0.8,0.2);
    vec3 pos = vec3(4, -1.8, 3.0);
    p = p - pos;

    vec2 q = vec2(length(p.xy)-t.x, p.z);
    return length(q)-t.y;
}

// used art of code tutorial for custom shapes:
// https://www.youtube.com/watch?v=Ff0jJyyiVyw&t=893s
material torus_material(vec3 p)
{
    material mat;
    mat.color = vec4(8.0, 0.3, 0.3, 0.0);
    
    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(1.0, 0.5, 0.3);
    mat.specular = vec3(1.0, 1.0, 1.0);
    mat.reflectedPortion = 0.0f;
    mat.is_transparent = false;

    return mat;
}

float blob_distance(vec3 p)
{
    vec3 q = p - vec3(-0.5, -2.2 + abs(sin(u_time*3.0)), 2.0);
    return length(q) - 0.8 + sin(10.0*q.x)*sin(10.0*q.y)*sin(10.0*q.z)*0.07;
}

material blob_material(vec3 p)
{
    material mat;
    mat.color = vec4(.3, 0.65, 0.1, 1.0);
    
    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(0.0, 0.0, 0.0);
    mat.specular = vec3(0.0, 0.0, 0.0);
    mat.reflectedPortion = 0.0f;
    mat.refracIndex =  1.125;
    mat.colorAbsorbtion = vec3(8.0, 2.0, 0.1);
    mat.is_transparent = false;

    return mat;
}

float sphere_distance(vec3 p, vec3 pos, float radius)
{
    return length(p - pos) - radius;
}

material sphere_material(vec3 p)
{
    material mat;
    mat.color = vec4(0.1, 0.1, 0.1, 1.0);

    mat.shininess = 30;
    mat.specular_intensity = 0.5;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(1.0, 1.0, 1.0);
    mat.specular = vec3(1.0, 1.0, 1.0);
    mat.reflectedPortion = 0.0f;
    mat.is_transparent = true;
    mat.refracIndex = 1.125;

    return mat;
}

// used art of code video on ray marching simple shapes as reference
// https://www.youtube.com/watch?v=AfKGMUDWfuE&list=PLGmrMu-IwbgtMxMiV3x4IrHPlPmg7FD-P&index=3
float animated_box(vec3 p, vec3 loc, float stepSize)
{
    float d = 1e10;
    float an = sin(u_time);

    vec3 sphereLoc = loc;
    sphereLoc.z = sphereLoc.z + stepSize * an;

    float d1 = sphere_distance(p, sphereLoc, 0.5);
    float d2 = box_distance(p, loc, vec3(1.0, 1.0, 0.5) ); 
    float dt = opSmoothUnion(d1,d2, 0.25);
    d = min( d, dt );
    return d;
}

vec2 cpow( vec2 z, float n ) { 
    float r = length( z ); 
    float a = atan( z.y, z.x ); 
    return pow( r, n )*vec2( cos(a*n), sin(a*n) ); 
}

// used https://iquilezles.org/www/articles/mset_1bulb/mset1bulb.htm as a reference for implementing the fractal
vec3 drawFractal(float k, vec2 point , vec2 canvasArea)
{
    vec3 col = vec3(0.0);
   
    vec2 p = (-canvasArea.xy + 2.*(point))/canvasArea.y;    
    vec2 c = p * 1.25;
     
    const float threshold = 3.0;
    vec2 z = vec2( 0.0 );
    float it = 0.0;
    for( int i=0; i<100; i++ )
    {
        z = cpow(z, k) + c;
        if( dot(z,z)>threshold ) 
            break;
        it++;
    }

    if( it<99.5 )
    {
        float sit = it - log2(log2(dot(z,z))/(log2(threshold)))/log2(k); 
        col = 0.5 + 0.5*cos( 3.0 + sit*0.075*k + vec3(0.1,0.1,0.1));
    }

    return col;
}


float room_distance(vec3 p)
{
    return //max(
        //-box(p-vec3(0.0,3.1,3.0), vec3(0.5, 0.5, 0.5)),
        -box(p-vec3(0.0,0.0,0.0), vec3(6.0, 3.0, 6.0));
    //);
}

material room_material(vec3 p)
{
    material mat;
    mat.color = vec4(1.0, 1.0, 1.0, 1.0);
    
    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.specular = vec3(0.8,0.8,0.8);
    mat.reflectedPortion = 0.3f;
    mat.is_transparent = false;

    if(p.y < -2.98){
        vec3 eps = vec3(0.001, 0.0, 0.0);
        vec3 pNorm = normalize(vec3(
            room_distance(p+eps.xyy)-room_distance(p-eps.xyy),
            room_distance(p+eps.yxy)-room_distance(p-eps.yxy),
            room_distance(p+eps.yyx)-room_distance(p-eps.yyx)
        ));
        vec2 samplingLoc= getUV(p, pNorm);
        mat.color.rgb = texture(marbleFloorTexture, samplingLoc).xyz;
    }else if(p.x <= -5.98) {
        mat.color.rgb = vec3(1.0, 0.0, 0.0);
    }else if(p.x >= 5.98){
         mat.color.rgb = vec3(0.0, 1.0, 0.0);
    }else if(p.z >= 5.98){
        vec3 fract_start = vec3(-5, 3, 0);
        vec3 fract_end = vec3(5, -3, 0);
    
        vec2 relativePoint = (p - fract_start).xy;
        vec2 area = fract_end.xy - fract_start.xy;
        mat.color = vec4(drawFractal(2., relativePoint, area), 1.);
    }else if(abs(p.x) <= 0.5 && p.y >= 2.98 && abs(p.z - 3.) <= 0.5){
        mat.color = vec4(0.9, 0.9, 0.9, 1.);
    }
    
    mat.diffuse = mat.color.xyz;

    return mat;
}

float crate_distance(vec3 p)
{
    return box(rot_y(p-vec3(-1,-1,5), u_time), vec3(1, 2, 1));
}

material crate_material(vec3 p)
{
    material mat;
    mat.color = vec4(1.0, 1.0, 1.0, 1.0);

    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(0.740,0.733,0.309);
    mat.specular = vec3(0.750,0.643,0.750);
    mat.reflectedPortion = 0.0f;
    mat.is_transparent = false;

    vec3 q = rot_y(p-vec3(-1,-1,5), u_time) * 0.98;
    if(fract(q.x + floor(q.y*2.0) * 0.5 + floor(q.z*2.0) * 0.5) < 0.5)
    {
        mat.color.rgb = vec3(0.0, 1.0, 1.0);
    }
    return mat;
}


/* The distance function collecting all others.
 *
 * Parameters:
 *  p   The point for which to find the nearest surface
 *  mat The material of the nearest surface
 *
 * Returns:
 *  The distance to the nearest surface.
 */
float map(
    in vec3 p,
    out material mat
){
    float min_dist = MAX_DIST*2.0;
    float dist = 0.0;

    dist = blob_distance(p);
    if(dist < min_dist) {
        mat = blob_material(p);
        min_dist = dist;
    }

    dist = room_distance(p);
    if(dist < min_dist) {
         mat = room_material(p);
         min_dist = dist;
    }
    
    dist = crate_distance(p);
    if(dist < min_dist) {
        mat = crate_material(p);
        min_dist = dist;
    }

    vec3 sphereLoc = vec3(1.5, -1.8, 4.0);
    dist = sphere_distance(p, sphereLoc, 1.2);
    if(dist < min_dist) {
        mat = sphere_material(p);
        min_dist = dist;
    }

    // Add your own objects here!
    dist = torus_distance(p);
    if(dist < min_dist){
        mat = torus_material(p);
        min_dist = dist;
    }

    vec3 animBoxLoc = vec3(-3., -2., 3.);
    dist = animated_box(p, animBoxLoc, 1.);
    if(dist < min_dist){
        mat = torus_material(p);
        mat.color = vec4(0.0, 0.8, 0.0, 1.);
        min_dist = dist;
    }

    return min_dist;
}

float calculateSoftshadow(vec3 ro, vec3 rd, float mint, float tmax)
{
	float res = 1.0;
    float t = mint;
    float ph = 1e10; // big, such that y = 0 on the first iteration
    
    for( int i=0; i<32; i++ )
    {
        material matOut;
		float h = map( ro + rd*t , matOut);

        float y = h*h/(2.0*ph);
        float d = sqrt(h*h-y*y);
        res = min( res, 10.0*d/max(0.0,t-y) );
        ph = h;
        
        t += h;
        
        if( res<0.0001 || t>tmax ) break;
        
    }
    return clamp( res, 0.2, 1.0 );
}

/* Calculates the normal of the surface closest to point p.
 *
 * Parameters:
 *  p   The point where the normal should be calculated
 *  mat The material information, produced as a byproduct
 *
 * Returns:
 *  The normal of the surface.
 *
 * See https://www.iquilezles.org/www/articles/normalsSDF/normalsSDF.htm
 * if you're interested in how this works.
 */
vec3 normal(vec3 p, out material mat)
{
    const vec2 k = vec2(1.0, -1.0);
    return normalize(
        k.xyy * map(p + k.xyy * EPSILON, mat) +
        k.yyx * map(p + k.yyx * EPSILON, mat) +
        k.yxy * map(p + k.yxy * EPSILON, mat) +
        k.xxx * map(p + k.xxx * EPSILON, mat)
    );
}

/* Finds the closest intersection of the ray with the scene.
 *
 * Parameters:
 *  o           Origin of the ray
 *  v           Direction of the ray
 *  max_dist    Maximum distance the ray can travel. Usually MAX_DIST.
 *  p           Location of the intersection
 *  n           Normal of the surface at the intersection point
 *  mat         Material of the intersected surface
 *  inside      Whether we are marching inside an object or not. Useful for
 *              refractions.
 *
 * Returns:
 *  true if a surface was hit, false otherwise.
 */
bool intersect(
    in vec3 o,
    in vec3 v,
    in float max_dist,
    out vec3 p,
    out vec3 n,
    out material mat,
    bool inside
) {
    float t = MIN_DIST;
    float dir = inside ? -1.0 : 1.0;
    bool hit = false;

    for(int i = 0; i < MARCH_MAX_STEPS; ++i)
    {
        p = o + t * v;
        float dist = dir * map(p, mat);
        
        hit = abs(dist) < HIT_RATIO * t;

        if(hit || t > max_dist) break;

        t += dist * STEP_RATIO;
    }

    n = normal(p, mat);

    return hit;
}

float GetLight(vec3 p, vec3 pNorm, vec3 lightPos, out bool inShadow) {
    vec3 l = normalize(lightPos-p);
    
    float dif = clamp(dot(pNorm, l), 0.2, 1.);
    
	material mat;
	vec3 p1, n1;
    
    float distanceToLight= length(lightPos- p);
    
    // Compute intersection point along the view ray.
    inShadow = intersect(p + pNorm * 0.001, l, distanceToLight * 0.9, p1, n1, mat, false);
    
    if(use_soft_shadows){
      	dif *= calculateSoftshadow(p, l, 0.01, 3.0);
    }else if(inShadow){
        dif *= 0.3;
    }
    
    return dif;
}

vec3 shade(vec3 n, vec3 rd, vec3 ld, vec3 color, material mat){
    vec3 diffuse = mat.diffuse * mat.diffuse_intensity;
    diffuse = diffuse * max(dot(ld, n), 0.0);
    
    vec3 reflectedLightDirection = reflect(ld, n);
    vec3 spec = mat.specular * mat.specular_intensity *
        pow(max(dot(reflectedLightDirection, rd), 0.0), mat.shininess);
   	
    return 0.5 * color + diffuse + spec;
}

// used https://blog.demofox.org/2017/01/09/raytracing-reflection-refraction-fresnel-total-internal-reflection-and-beers-law/ as
// reference for understanding how to compute light motion within glass
vec3 GetSurfaceRefractionColor(vec3 rd, vec3 surfacePt, vec3 surfaceNormal, vec3 lamp_pos,
    vec3 originalSurfaceColor, material mat)
{
    if(!mat.is_transparent)
        return originalSurfaceColor;

    int count = 0;
    const int MAX_ITERATIONS = 3;
    material tempMat1, tempMat2;

    vec3 rayDir = rd;
    vec3 currentSurfaceNorm = surfaceNormal;
    vec3 currentSurfacePoint = surfacePt;
    vec3 nextPt, nextPtNormal, nextRayDir;
    
    do{
        nextRayDir = refract( normalize(rayDir), normalize(currentSurfaceNorm), 1. / mat.refracIndex);
        intersect(currentSurfacePoint, nextRayDir, MAX_DIST, nextPt, nextPtNormal, tempMat1, true);

        rayDir = nextRayDir;
        currentSurfacePoint = nextPt + normalize(nextRayDir) * 0.1;
        currentSurfaceNorm = nextPtNormal;
    }while(++count < MAX_ITERATIONS && tempMat1.is_transparent);

    bool isInShadow;
    vec3 color = shade(currentSurfaceNorm, rayDir, 
        normalize(lamp_pos-nextPtNormal), tempMat1.color.rgb, mat);
    return color;
}

vec3 GetSurfaceReflectionColor(vec3 rd, vec3 surfacePt, vec3 surfaceNormal, vec3 lamp_pos, 
    vec3 originalSurfaceColor, material mat)
{
    float lightIntensity = 1.0;
    const int MAX_RAY_REFLECTIONS_COUNT = 2;
    vec3 reflColorStack[MAX_RAY_REFLECTIONS_COUNT];
    float reflColorWeight[MAX_RAY_REFLECTIONS_COUNT];
    bool isInShadow;

    reflColorStack[0] = originalSurfaceColor * GetLight(surfacePt, surfaceNormal, lamp_pos, isInShadow);
    reflColorWeight[0] = lightIntensity * (1 - mat.reflectedPortion);

    lightIntensity = lightIntensity * mat.reflectedPortion;
    vec3 previousNormal = surfaceNormal;
    vec3 PreviousReflectionO = surfacePt;
    vec3 PreviousReflectionD = reflect(rd, surfaceNormal);

    int stackIndex = 1;
    for(stackIndex; stackIndex < MAX_RAY_REFLECTIONS_COUNT && lightIntensity > 0.; ++stackIndex){
        material tempMat;
        vec3 nextPointO;
        vec3 nextPointNorm;
        vec3 nextPointD;

        if( !intersect(PreviousReflectionO, PreviousReflectionD, 
                MAX_DIST, nextPointO, nextPointNorm, tempMat, false)){
            break;
        }

        
        if(tempMat.is_transparent)
        {
            tempMat.color.rgb = GetSurfaceRefractionColor(PreviousReflectionD, PreviousReflectionO, nextPointNorm,
                lamp_pos, tempMat.color.rgb, tempMat);
        }

        bool isInShadow;
        reflColorStack[stackIndex] = tempMat.color.rgb * GetLight(nextPointO, nextPointNorm, lamp_pos, isInShadow);
        if(!isInShadow && !tempMat.is_transparent){
            reflColorStack[stackIndex] = shade(nextPointO, PreviousReflectionD, normalize(lamp_pos - nextPointO), 
                reflColorStack[stackIndex], tempMat);
        }

        reflColorWeight[stackIndex] = lightIntensity* (1 - mat.reflectedPortion);
        lightIntensity = lightIntensity * mat.reflectedPortion;
        PreviousReflectionD = reflect(PreviousReflectionD, nextPointNorm);
    }

    // Add some lighting code here!
	vec3 color = vec3(0., 0., 0.);
    for(int i = 0; i < stackIndex; ++i)
    {
        color += reflColorWeight[i] * reflColorStack[i];
    }

    return color;
}


/* Calculates the color of the pixel, based on view ray origin and direction.
 *
 * Parameters:
 *  o   Origin of the view ray
 *  v   Direction of the view ray
 *
 * Returns:
 *  Color of the pixel.
 */
vec3 render(vec3 ro, vec3 rd)
{
    // This lamp is positioned at the hole in the roof.
    vec3 lamp_pos = vec3(0.0, 2.7, 3.0);
    vec3 firstP, firstN;
    material mat;

    // Compute intersection point along the view ray.
    bool hit = intersect(ro, rd, MAX_DIST, firstP, firstN, mat, false);
    if(!hit)
    {
        return vec3(0.7, 0.7, 0.7);
    }

    // for small patch in ceiling, this is a square representing light
    if(abs(firstP.x) <= 0.5 && firstP.y >= 2.98 && abs(firstP.z - 3.) <= 0.5)
    {
        return vec3(5.);
    }
    
    bool isInShadow;
    vec3 color = mat.color.rgb ;// 
    color = GetSurfaceReflectionColor(rd, firstP, firstN, lamp_pos, color, mat);
    color = GetSurfaceRefractionColor(rd, firstP, firstN, lamp_pos, color, mat);

    if(!isInShadow)
	{
		color = shade(firstN, rd, normalize(lamp_pos-firstP), color, mat);
	}

    return color.xyz;
}

uniform vec3 cameraFront;
uniform vec3 cameraPos;
uniform vec3 cameraRotation;
layout (location = 0) out vec4 FragColor;
layout (location = 1) out vec4 BrightColor;

// watched video by art of code to understand how raymaching works and implement perspective mapping
// https://www.youtube.com/watch?v=PGtv-dBi2wE&t=2s
void main()
{   
    // This is the position of the pixel in normalized device coordinates.
    vec2 uv = (gl_FragCoord.xy/u_resolution)*2.0-1.0;
    // Calculate aspect ratio
    float aspect = u_resolution.x/u_resolution.y;

    vec3 rd = vec3(uv.x,uv.y,1);
    rd = rot_x(rd, cameraRotation.x);
    rd = rot_y(rd, cameraRotation.y);

    FragColor = vec4(render(cameraPos, rd), 1.0);

    if(dot(FragColor.rgb, vec3(0.1, 0.1, 0.1)) >= 1)
    {
        BrightColor = vec4(FragColor.rgb, 1.0);
    }else{
        BrightColor = vec4(0.0, 0.0, 0.0, 1.0);
    }
}

