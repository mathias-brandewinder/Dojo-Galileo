#r @"bin\Debug\Galileo.dll"

open Planets
open System

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let app = Planets.App.init () 

Galileo.init ()

let shaderProgram = Planets.App.loadShaders ()
Planets.App.clear ()
Planets.App.draw app

Planets.App.drawColors (shaderProgram)
Galileo.fooTriangle (-1.0f, -1.0f, 0.0f) ( 1.0f, -1.0f, 0.0f) ( 0.0f,  1.0f, 0.0f)

App.draw app