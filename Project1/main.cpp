#include <glad/glad.h>
#include <GLFW/glfw3.h>

#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <glm/gtc/type_ptr.hpp>

#include <glad/glad.h>
#include <GLFW/glfw3.h>

#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <glm/gtc/type_ptr.hpp>
#include <time.h>

#include <iostream>

#include "ImageUtils.h"
#include "Shader.h"
#include "stb_image.h"

std::pair<glm::vec3, glm::vec3> makeTBNTransformForWalls(glm::vec3* wallVertices);
void framebuffer_size_callback(GLFWwindow* window, int width, int height);
void processInput(GLFWwindow* window);

// settings
const unsigned int SCR_WIDTH = 800;
const unsigned int SCR_HEIGHT = 600;

int main()
{
    // glfw: initialize and configure
    // ------------------------------
    glfwInit();
    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
    glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

    // glfw window creation
    // --------------------
    GLFWwindow* window = glfwCreateWindow(SCR_WIDTH, SCR_HEIGHT, "LearnOpenGL", NULL, NULL);
    if (window == NULL)
    {
        std::cout << "Failed to create GLFW window" << std::endl;
        glfwTerminate();
        return -1;
    }
    glfwMakeContextCurrent(window);
    glfwSetFramebufferSizeCallback(window, framebuffer_size_callback);

    // glad: load all OpenGL function pointers
    // ---------------------------------------
    if (!gladLoadGLLoader((GLADloadproc)glfwGetProcAddress))
    {
        std::cout << "Failed to initialize GLAD" << std::endl;
        return -1;
    }

    // configure global opengl state
    // -----------------------------
    glEnable(GL_DEPTH_TEST);

    // build and compile our shader zprogram
    // ------------------------------------
    Shader ourShader("transform.vs", "transform.fs");

    // SETUP 2 vertices to draw box spanning the whole screen to force pixel shader to run
    float vertices[] = {
        -1.f, -1.f, 0.f,
        -1.f, 1.f, 0.f,
        1.f, 1.f, 0.f,

       -1.f, -1.f, 0.f,
       1.f, -1.f, 0.f,
       1.f, 1.f, 0.f,
    };

    // setup normal maps transformation
    glm::vec3 WallVerticies[] = {
        glm::vec3(-2.98, 1, 0),
        glm::vec3(-2.98, -1, 0),
        glm::vec3(-2.98, -1, 1),
        glm::vec3(2.98, 1, 0),
        glm::vec3(2.98, -1, 0),
        glm::vec3(2.98, -1, 1),
    };

    auto leftWallTangents = makeTBNTransformForWalls(&WallVerticies[0]);
    auto rightWallTangents = makeTBNTransformForWalls(&WallVerticies[3]);
    ourShader.setVec3("left_wall_tangent", leftWallTangents.first);
    ourShader.setVec3("left_wall_bitangent", leftWallTangents.second);
    ourShader.setVec3("right_wall_tangent", rightWallTangents.first);
    ourShader.setVec3("right_wall_bitangent", rightWallTangents.second);


    unsigned int VBO, VAO;
    glGenVertexArrays(1, &VAO);
    glGenBuffers(1, &VBO);

    glBindVertexArray(VAO);

    glBindBuffer(GL_ARRAY_BUFFER, VBO);
    glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

    // position attribute
    glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 3 * sizeof(float), (void*)0);
    glEnableVertexAttribArray(0);

 
    // load texture
    ourShader.use();

    //load image
    glActiveTexture(GL_TEXTURE0);
    bool success;
    unsigned int brickWallTextureID = loadTexture("Brick_Wall_Texture.jpg", success);
    assert(success);
    unsigned int brickWallTextureNormalID = loadTexture("Brick_Wall_Texture_NORMAL.jpg", success);
    assert(success);

    ourShader.setInt("brickWallTexture", 0);
    ourShader.setInt("brickWallTextureNormal", 1);

    // render loop
    // -----------
    while (!glfwWindowShouldClose(window))
    {
        // input
        // -----
        processInput(window);

        // render
        // ------
        glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT); // also clear the depth buffer now!

        
        // activate shader
        ourShader.use();

        // create transformations
        glm::vec2 u_resolution = glm::vec2(float(SCR_WIDTH), float(SCR_HEIGHT));
        
        double x_mouse, y_mouse;
        glfwGetCursorPos(window, &x_mouse, &y_mouse);
        glm::vec2 u_mouse = glm::vec2(float(x_mouse), float(y_mouse));

        float u_time = (float) glfwGetTime();

        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D, brickWallTextureID);
        glActiveTexture(GL_TEXTURE1);
        glBindTexture(GL_TEXTURE_2D, brickWallTextureNormalID);

        // pass transformation matrices to the shader
        ourShader.setVec2("u_resolution", u_resolution);
        ourShader.setVec2("u_mouse", u_mouse);
        ourShader.setFloat("u_time", u_time);

        glDrawArrays(GL_TRIANGLES, 0, 6);
        // glfw: swap buffers and poll IO events (keys pressed/released, mouse moved etc.)
        // -------------------------------------------------------------------------------
        glfwSwapBuffers(window);
        glfwPollEvents();
    }

    // glfw: terminate, clearing all previously allocated GLFW resources.
    // ------------------------------------------------------------------
    glfwTerminate();
    return 0;
}


// process all input: query GLFW whether relevant keys are pressed/released this frame and react accordingly
// ---------------------------------------------------------------------------------------------------------
void processInput(GLFWwindow* window)
{
    if (glfwGetKey(window, GLFW_KEY_ESCAPE) == GLFW_PRESS)
        glfwSetWindowShouldClose(window, true);
}

// glfw: whenever the window size changed (by OS or user resize) this callback function executes
// ---------------------------------------------------------------------------------------------
void framebuffer_size_callback(GLFWwindow* window, int width, int height)
{
    // make sure the viewport matches the new window dimensions; note that width and 
    // height will be significantly larger than specified on retina displays.
    glViewport(0, 0, width, height);
}


std::pair<glm::vec3, glm::vec3> makeTBNTransformForWalls(glm::vec3 wallVertices[])
{
    // texture coordinates
    const glm::vec2 uv1(0.0, 1.0);
    const glm::vec2 uv2(0.0, 0.0);
    const glm::vec2 uv3(1.0, 0.0);

    
    glm::vec3 edge1 = wallVertices[1] - wallVertices[0];
    glm::vec3 edge2 = wallVertices[2] - wallVertices[0];
    glm::vec2 deltaUV1 = uv2 - uv1;
    glm::vec2 deltaUV2 = uv3 - uv1;

    glm::vec3 tangent1, bitangent1;
    float f = 1.0f / (deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y);

    tangent1.x = f * (deltaUV2.y * edge1.x - deltaUV1.y * edge2.x);
    tangent1.y = f * (deltaUV2.y * edge1.y - deltaUV1.y * edge2.y);
    tangent1.z = f * (deltaUV2.y * edge1.z - deltaUV1.y * edge2.z);

    bitangent1.x = f * (-deltaUV2.x * edge1.x + deltaUV1.x * edge2.x);
    bitangent1.y = f * (-deltaUV2.x * edge1.y + deltaUV1.x * edge2.y);
    bitangent1.z = f * (-deltaUV2.x * edge1.z + deltaUV1.x * edge2.z);

    return std::make_pair(tangent1, bitangent1);
}