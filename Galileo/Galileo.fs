namespace Galileo

open System
open System.IO
open System.Numerics
open System.Diagnostics

open Ferop

[<Struct>]
type Renderer =
    val Window : nativeint
    val GLContext : nativeint

[<Struct>]
type DrawTriangle =
    val X : Vector3
    val Y : Vector3
    val Z : Vector3

    new (x, y, z) = { X = x; Y = y; Z = z }

type Triangle = Triangle of startIndex: int * endIndex: int

type Sphere = Sphere of unit

[<System.Runtime.InteropServices.UnmanagedFunctionPointer (System.Runtime.InteropServices.CallingConvention.Cdecl)>]
type VboDelegate = delegate of unit -> unit

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

module R = 

    [<Import; MI (MIO.NoInlining)>]
    let init () : Renderer =
        C """
        SDL_Init (SDL_INIT_VIDEO);

        R_Renderer r;

        r.Window = 
            SDL_CreateWindow(
                "Galileo",
                SDL_WINDOWPOS_UNDEFINED,
                SDL_WINDOWPOS_UNDEFINED,
                600, 600,
                SDL_WINDOW_OPENGL);

        SDL_GL_SetAttribute (SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL_GL_SetAttribute (SDL_GL_CONTEXT_MINOR_VERSION, 2);
        SDL_GL_SetAttribute (SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);

        r.GLContext = SDL_GL_CreateContext ((SDL_Window*)r.Window);
        SDL_GL_SetSwapInterval (0);

        #if defined(__GNUC__)
        #else
        glewExperimental = GL_TRUE;
        glewInit ();
        #endif

        return r;
        """

    [<Import; MI (MIO.NoInlining)>]
    let exit (r: Renderer) : int =
        C """
        SDL_GL_DeleteContext (r.GLContext);
        SDL_DestroyWindow ((SDL_Window*)r.Window);
        SDL_Quit ();
        return 0;
        """
    
    [<Import; MI (MIO.NoInlining)>]
    let clear () : unit = C """ glClear (GL_COLOR_BUFFER_BIT); """

    [<Import; MI (MIO.NoInlining)>]
    let draw (app: Renderer) : unit = C """ SDL_GL_SwapWindow ((SDL_Window*)app.Window); """

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
    let bindVbo (vbo: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vbo);
        glEnableVertexAttribArray(0);
        glVertexAttribPointer(
            0,                  // attribute 0. No particular reason for 0, but must match the layout in the shader.
            3,                  // size
            GL_FLOAT,           // type
            GL_FALSE,           // normalized?
            0,                  // stride
            (void*)0            // array buffer offset
        );
        glDisableVertexAttribArray(0);
        """

    [<Import; MI (MIO.NoInlining)>]
    let drawVbo (offset: int) (size: int) (f: VboDelegate) (vbo: int) : unit =
        C """
        glEnableVertexAttribArray(0);
//        glVertexAttribPointer(
//            0,                  // attribute 0. No particular reason for 0, but must match the layout in the shader.
//            3,                  // size
//            GL_FLOAT,           // type
//            GL_FALSE,           // normalized?
//            0,                  // stride
//            (void*)0            // array buffer offset
//        );
        f ();
        // Draw the triangle !
        glDrawArrays(GL_TRIANGLES, offset, size); // 3 indices starting at 0 -> 1 triangle
        //f ();
        glDisableVertexAttribArray(0);
        """

    [<Import; MI (MIO.NoInlining)>]
    let updateVbo (offset: int) (size: int) (data: DrawTriangle []) (vbo: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vbo);
        glBufferSubData (GL_ARRAY_BUFFER, offset, size, data);
        """

    [<Import; MI (MIO.NoInlining)>]
    let drawColor (shaderProgram: int) (r: single) (g: single) (b: single) : unit = 
        C """
        GLint uni_color = glGetUniformLocation (shaderProgram, "uni_color");
        glUniform4f (uni_color, r, g, b, 0.0f);
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

// http://gafferongames.com/game-physics/fix-your-timestep/
module GameLoop =
    type private GameLoop<'T> = { 
        State: 'T
        PreviousState: 'T
        LastTime: int64
        UpdateTime: int64
        UpdateAccumulator: int64
        RenderAccumulator: int64
        RenderFrameCount: int
        RenderFrameCountTime: int64
        RenderFrameLastCount: int }

    let start (state: 'T) (pre: unit -> unit) (update: int64 -> int64 -> 'T -> 'T) (render: float32 -> 'T -> 'T -> unit) =
        let targetUpdateInterval = (1000. / 30.) * 10000. |> int64
        let targetRenderInterval = (1000. / 12000.) * 10000. |> int64
        let skip = (1000. / 5.) * 10000. |> int64

        let stopwatch = Stopwatch.StartNew ()
        let inline time () = stopwatch.Elapsed.Ticks

        let rec loop gl =
            let currentTime = time ()
            let deltaTime =
                match currentTime - gl.LastTime with
                | x when x > skip -> skip
                | x -> x

            let updateAcc = gl.UpdateAccumulator + deltaTime

            // We do not want our render accumulator going out of control,
            // so let's put a limit of its interval.
            let renderAcc = 
                match gl.RenderAccumulator with
                | x when x > targetRenderInterval -> targetRenderInterval
                | x -> x + deltaTime

            let rec processUpdate gl =
                if gl.UpdateAccumulator >= targetUpdateInterval
                then
                    let state = update gl.UpdateTime targetUpdateInterval gl.State

                    processUpdate
                        { gl with 
                            State = state
                            PreviousState = gl.State
                            UpdateTime = gl.UpdateTime + targetUpdateInterval
                            UpdateAccumulator = gl.UpdateAccumulator - targetUpdateInterval }
                else
                    gl

            let processRender gl =
                if gl.RenderAccumulator >= targetRenderInterval then
                    render (single gl.UpdateAccumulator / single targetUpdateInterval) gl.PreviousState gl.State

                    let renderCount, renderCountTime, renderLastCount =
                        if currentTime >= gl.RenderFrameCountTime + (10000L * 1000L) then
                            printfn "FPS: %A" gl.RenderFrameLastCount
                            1, gl.RenderFrameCountTime + (10000L * 1000L), gl.RenderFrameCount
                        else
                            gl.RenderFrameCount + 1, gl.RenderFrameCountTime, gl.RenderFrameLastCount

                    { gl with 
                        LastTime = currentTime
                        RenderAccumulator = gl.RenderAccumulator - targetRenderInterval
                        RenderFrameCount = renderCount
                        RenderFrameCountTime = renderCountTime
                        RenderFrameLastCount = renderLastCount }
                else
                    { gl with LastTime = currentTime }

            pre ()
       
            { gl with UpdateAccumulator = updateAcc; RenderAccumulator = renderAcc }
            |> processUpdate
            |> processRender
            |> loop

        loop
            { State = state
              PreviousState = state
              LastTime = 0L
              UpdateTime = 0L
              UpdateAccumulator = targetUpdateInterval
              RenderAccumulator = 0L
              RenderFrameCount = 0
              RenderFrameCountTime = 0L
              RenderFrameLastCount = 0 }

[<RequireQualifiedAccess>]
module Galileo =

    let vbo = ref 0
    let shaderProgram = ref 0

    let currentOffset = ref 0
    let bufferInfo = System.Collections.Generic.Dictionary<int * int, single * single * single> ()

    let addDrawTriangle datum color =
        let size = sizeof<DrawTriangle>
        let offset = !currentOffset

        R.updateVbo offset size [|datum|] !vbo
        bufferInfo.Add ((offset, size), color)
        currentOffset := offset + size

    let proc = new MailboxProcessor<AsyncReplyChannel<unit> * (unit -> unit)>(fun inbox ->
        let rec loop () = async { 
            let r = R.init ()

            // buffer limit
            let size = 65536
            let lines = Array.init size (fun _ -> DrawTriangle())
            vbo := R.generateVbo (size * sizeof<DrawTriangle>) lines

            shaderProgram := R.loadShaders ()

            let rec executeRenderCommands () =
                match inbox.CurrentQueueLength with
                | 0 -> ()
                | _ ->
                    let ch, cmd = inbox.Receive () |> Async.RunSynchronously

                    try
                        cmd ()
                    with | ex -> printfn "%A" ex

                    ch.Reply ()
                    executeRenderCommands ()

            GameLoop.start ()
                (fun () ->
                    ()
                )
                (fun time interval curr ->
                    GC.Collect (2, GCCollectionMode.Forced, true)

                    ()
                ) 
                (fun t prev curr ->
                    executeRenderCommands ()

                    R.clear ()

                    // FIXME: kinda not working
                    R.bindVbo !vbo
                    bufferInfo
                    |> Seq.iter (fun kvp ->
                        let key, value = kvp.Key, kvp.Value
                        let offset, size = key
                        let r, g, b = value

                        let del = VboDelegate (fun () -> 
                                R.drawColor !shaderProgram r g b
                        )

                        R.drawVbo offset size del !vbo)

                    R.draw r
                )
        }
        loop ())

    printfn "Begin Initializing Galileo"
    proc.Start ()

    let spawnDefaultRedTriangle () : Async<Triangle> = async {
        proc.PostAndReply <| fun ch -> ch, fun () ->
            let datum = DrawTriangle (Vector3 (0.f, 1.f, 0.f), Vector3 (-1.f, -1.f, 0.f), Vector3 (1.f, -1.f, 0.f))
            addDrawTriangle datum (1.f, 0.f, 0.f)

        return Triangle (0, 0)
    }

    let spawnDefaultBlueTriangle () : Async<Triangle> = async {
        proc.PostAndReply <| fun ch -> ch, fun () ->
            let datum = DrawTriangle (Vector3 (0.f, -1.f, 0.f), Vector3 (1.f, 1.f, 0.f), Vector3 (-1.f, 1.f, 0.f))
            addDrawTriangle datum (0.f, 0.f, 1.f)

        return Triangle (0, 0)
    }

    let spawnDefaultSphere () : Async<Sphere> = async {
        return raise <| System.NotImplementedException ()
    }

