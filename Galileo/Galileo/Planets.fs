namespace Planets

open System.IO
open Ferop

[<Struct>]
type Application =
    val Window : nativeint
    val GLContext : nativeint

[<Struct>]
type vec3 =
    val X : single
    val Y : single
    val Z : single
    new (x, y, z) = { X = x; Y = y; Z = z }

[<Struct>]
type DrawTriangle =
    val X : vec3
    val Y : vec3
    val Z : vec3
    new (x, y, z) = { X = x; Y = y; Z = z }


[<Ferop>]
[<ClangOsx (
    "-DGL_GLEXT_PROTOTYPES -I/Library/Frameworks/SDL2.framework/Headers",
    "-F/Library/Frameworks -framework Cocoa -framework OpenGL -framework IOKit -framework SDL2"
)>]
[<GccLinux ("-I../../include/SDL2", "-lSDL2")>]
#if __64BIT__
[<MsvcWin (""" /I ..\include\SDL2 /I ..\include ..\lib\win\x64\SDL2.lib ..\lib\win\x64\SDL2main.lib ..\lib\win\x64\glew32.lib opengl32.lib """)>]
#else
[<MsvcWin (""" /I  ..\include\SDL2 /I  ..\include  ..\lib\win\x86\SDL2.lib  ..\lib\win\x86\SDL2main.lib  ..\lib\win\x86\glew32.lib opengl32.lib """)>]
#endif
[<Header ("""
#include <stdio.h>
#if defined(__GNUC__)
#   include "SDL.h"
#   include "SDL_opengl.h"
#else
#   include "SDL.h"
#   include <GL/glew.h>
#   include <GL/wglew.h>
#endif
""")>]

module App = 

    [<Import; MI (MIO.NoInlining)>]
    let init () : Application =
        C """
SDL_Init (SDL_INIT_VIDEO);

App_Application app;

app.Window = 
    SDL_CreateWindow(
        "Galileo",
        SDL_WINDOWPOS_UNDEFINED,
        SDL_WINDOWPOS_UNDEFINED,
        600, 600,
        SDL_WINDOW_OPENGL);

SDL_GL_SetAttribute (SDL_GL_CONTEXT_MAJOR_VERSION, 3);
SDL_GL_SetAttribute (SDL_GL_CONTEXT_MINOR_VERSION, 2);
SDL_GL_SetAttribute (SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);

app.GLContext = SDL_GL_CreateContext ((SDL_Window*)app.Window);
SDL_GL_SetSwapInterval (0);

#if defined(__GNUC__)
#else
glewExperimental = GL_TRUE;
glewInit ();
#endif

return app;
        """

    [<Import; MI (MIO.NoInlining)>]
    let exit (app: Application) : int =
        C """
SDL_GL_DeleteContext (app.GLContext);
SDL_DestroyWindow ((SDL_Window*)app.Window);
SDL_Quit ();
return 0;
        """
    
    [<Import; MI (MIO.NoInlining)>]
    let clear () : unit = C """ glClear (GL_COLOR_BUFFER_BIT); """

    [<Import; MI (MIO.NoInlining)>]
    let draw (app: Application) : unit = C """ SDL_GL_SwapWindow ((SDL_Window*)app.Window); """

    [<Import; MI (MIO.NoInlining)>]
    let generateVbo (size: int) (data: DrawTriangle[]) : int =
        C """
GLuint vbo;
glGenBuffers (1, &vbo);

glBindBuffer (GL_ARRAY_BUFFER, vbo);

glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);
return vbo;
        """

    [<Import; MI (MIO.NoInlining)>]
    let drawVbo (size: int) (data: DrawTriangle[]) (vbo: int) : unit =
        C """
glEnableVertexAttribArray(0);
glBindBuffer (GL_ARRAY_BUFFER, vbo);
glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);

glVertexAttribPointer(
    0,                  // attribute 0. No particular reason for 0, but must match the layout in the shader.
    3,                  // size
    GL_FLOAT,           // type
    GL_FALSE,           // normalized?
    0,                  // stride
    (void*)0            // array buffer offset
);

// Draw the triangle !
glDrawArrays(GL_TRIANGLES, 0, 3); // 3 indices starting at 0 -> 1 triangle
glDisableVertexAttribArray(0);
        """

    [<Import; MI (MIO.NoInlining)>]
    let drawColors (shaderProgram: int) : unit = 

        C """
GLint uni_color = glGetUniformLocation (shaderProgram, "uni_color");
glUniform4f (uni_color, 1.0f, 0.0f, 0.0f, 0.0f);
"""

    [<Import; MI (MIO.NoInlining)>]
    let loadRawShaders (vertexSource: byte[]) (fragmentSource: byte[]) : int =
        C """
GLuint vertexShader = glCreateShader (GL_VERTEX_SHADER);
glShaderSource (vertexShader, 1, (const GLchar*const*)&vertexSource, NULL);    
glCompileShader (vertexShader);

GLuint fragmentShader = glCreateShader (GL_FRAGMENT_SHADER);
glShaderSource (fragmentShader, 1, (const GLchar*const*)&fragmentSource, NULL);
glCompileShader (fragmentShader);

/******************************************************/

GLuint shaderProgram = glCreateProgram ();
glAttachShader (shaderProgram, vertexShader);
glAttachShader (shaderProgram, fragmentShader);

glBindFragDataLocation (shaderProgram, 0, "color");

glLinkProgram (shaderProgram);

glUseProgram (shaderProgram);

/******************************************************/

GLuint vao;
glGenVertexArrays (1, &vao);

glBindVertexArray (vao);

GLint posAttrib = glGetAttribLocation (shaderProgram, "position");

glVertexAttribPointer (posAttrib, 2, GL_FLOAT, GL_FALSE, 0, 0);

glEnableVertexAttribArray (posAttrib);

return shaderProgram;
        """

    let loadShaders () =
        let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes ("v.vertex"))
        let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes ("f.fragment"))

        loadRawShaders vertexFile fragmentFile


module Galileo = 

    let mutable private vbo = 0

    let init () =

        let size = 65536
        let lines = Array.init size (fun _ -> DrawTriangle())
        vbo <- App.generateVbo (size * sizeof<DrawTriangle>) lines

    let fooTriangle (triplet1:(float32*float32*float32))
                (triplet2:(float32*float32*float32))
                (triplet3:(float32*float32*float32)) =
        
        let x1,y1,z1 = triplet1
        let x2,y2,z2 = triplet2
        let x3,y3,z3 = triplet3

        // points coordinates in [ -1.; 1. ]
        let dl = DrawTriangle (vec3(x1,y1,z1), vec3(x2,y2,z2), vec3(x3,y3,z3))
        App.drawVbo (sizeof<DrawTriangle>) [| dl |] vbo