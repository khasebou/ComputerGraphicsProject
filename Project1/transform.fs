#version 330 core


// Tampere University
// COMP.CE.430 Computer Graphics Coding Assignment 2020
//
// Write your name and student id here:
//   example name, H123456
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
//   Camera movement and rotation | -  | 
//   Sharp shadows                | X  | 
// Extra functionalities --------------------------------------------------------
//   Tone mapping                 |    | 
//   PBR shading                  |    | 
//   Soft shadows                 |    | 
//   Sharp reflections            |    | 
//   Glossy reflections           |    | 
//   Refractions                  |    | 
//   Caustics                     |    | 
//   SDF Ambient Occlusions       |    | 
//   Texturing                    |  - | 
//   Simple game                  |    | 
//   Progressive path tracing     |    | 
//   Basic post-processing        |    | 
//   Advanced post-processing     |    | 
//   Screen space reflections     |    | 
//   Screen space AO              |    | 
//   Simple own SDF               |    | 
//   Advanced own SDF             |    | 
//   Animated SDF                 |    | 
//   Other?                       |    | 

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
#define STEP_RATIO 0.9999
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


// materials

struct material
{
	// The color of the surface
	vec4 color;
	// PHONG shading params
    bool use_phong_shading;
	vec3 diffuse;
	vec3 specular;
	float diffuse_intensity;
	float specular_intensity;
	float shininess;
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


/* Each object has a distance function and a material function. The distance
 * function evaluates the distance field of the object at a given point, and
 * the material function determines the surface material at a point.
 */

float blob_distance(vec3 p)
{
    vec3 q = p - vec3(-0.5, -2.2 + abs(sin(u_time*3.0)), 2.0);
    return length(q) - 0.8 + sin(10.0*q.x)*sin(10.0*q.y)*sin(10.0*q.z)*0.07;
}

material blob_material(vec3 p)
{
    material mat;
    mat.color = vec4(1.0, 0.5, 0.3, 0.0);
    
    mat.use_phong_shading = true;
    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(1.0, 0.5, 0.3);
    mat.specular = vec3(1.0, 1.0, 1.0);

    return mat;
}

float sphere_distance(vec3 p)
{
    return length(p - vec3(1.5, -1.8, 4.0)) - 1.2;
}

material sphere_material(vec3 p)
{
    material mat;
    mat.color = vec4(0.1, 0.5, 0.0, 1.0);

	mat.use_phong_shading = true;
    mat.shininess = 0.2;
    mat.specular_intensity = 0.7;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(0.1, 0.2, 0.0);
    mat.specular = vec3(1.0, 1.0, 1.0);

    return mat;
}

float room_distance(vec3 p)
{
    return max(
        -box(p-vec3(0.0,3.1,3.0), vec3(0.5, 0.5, 0.5)),
        -box(p-vec3(0.0,0.0,0.0), vec3(3.0, 3.0, 6.0))
    );
}

material room_material(vec3 p)
{
    material mat;
    mat.color = vec4(1.0, 1.0, 1.0, 1.0);
    
    mat.use_phong_shading = true;
    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.specular = vec3(0.8,0.8,0.8);

    if(p.x <= -2.98) mat.color.rgb = vec3(1.0, 0.0, 0.0);
    else if(p.x >= 2.98) mat.color.rgb = vec3(0.0, 1.0, 0.0);

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

    mat.use_phong_shading = true;
    mat.shininess = 0.2;
    mat.specular_intensity = 0.288;
    mat.diffuse_intensity = 0.5;
    mat.diffuse = vec3(0.740,0.733,0.309);
    mat.specular = vec3(0.750,0.643,0.750);


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

    dist = sphere_distance(p);
    if(dist < min_dist) {
        mat = sphere_material(p);
        min_dist = dist;
    }

    // Add your own objects here!

    return min_dist;
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
    
    float dif = clamp(dot(pNorm, l), 0., 1.);
    
	material mat;
	vec3 p1, n1;
    
    float distanceToLight= length(lightPos- p);
    
    // Compute intersection point along the view ray.
    bool hit = intersect(p + pNorm * 0.001, l, distanceToLight * 0.9, p1, n1, mat, false);
    
    if( hit ){
      	dif *= 0.3;
        inShadow = true;
    }else{
        inShadow = false;
    }
    
    return dif;
}

vec3 shade(vec3 n, vec3 rd, vec3 ld, vec3 color, material mat){
    /*
    calculate the phong reflection model diffuse and specular term here!
    Hint: Check the lecture slides from the previous lecture
    */
    vec3 diffuse = mat.diffuse * mat.diffuse_intensity;
    diffuse = diffuse * max(dot(ld, n), 0.0);
    
    vec3 reflectedLightDirection = reflect(ld, n);
    vec3 spec = mat.specular * mat.specular_intensity *
        pow(max(dot(reflectedLightDirection, rd), 0.0), mat.shininess);
   	
    return 0.5 * color + diffuse + spec;
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
    vec3 lamp_pos = vec3(0.0, 3.0, 3.0);

    vec3 p, n;
    material mat;

    // Compute intersection point along the view ray.
    intersect(ro, rd, MAX_DIST, p, n, mat, false);

    // Add some lighting code here!
    bool isInShadow;
	vec3 color = mat.color.rgb * GetLight(p, n, lamp_pos, isInShadow);
    
	if(!isInShadow)
	{
		color = shade(n, rd, normalize(lamp_pos-p), color, mat);
	}

    return color;
}

uniform vec3 cameraFront;
uniform vec3 cameraPos;
uniform vec3 cameraRotation;
out vec4 FragColor;

void main()
{
	bool rotateCamera = false;
	bool moveCamera = false;
    
    // This is the position of the pixel in normalized device coordinates.
    vec2 uv = (gl_FragCoord.xy/u_resolution)*2.0-1.0;
    // Calculate aspect ratio
    float aspect = u_resolution.x/u_resolution.y;

    // Modify these two to create perspective projection!
    // Origin of the view ray
    //vec3 cameraPos = vec3(vec2(0, 0), -3.0);
	// Direction of the view ray
    vec3 rd = vec3(uv.x,uv.y,1);
    rd = rot_x(rd, cameraRotation.x);
    rd = rot_y(rd, cameraRotation.y);
    // if(moveCamera)
    // {
    // 	cameraPos.z += sin(u_time / 2.) * 2;
    // }

    // if(rotateCamera)
    // {
    //     rd = rot_y(rd, u_time / 10.);
    // }
    
    FragColor = vec4(render(cameraPos, rd), 1.0);
}