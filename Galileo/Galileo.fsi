namespace Galileo

[<Sealed>]
type Triangle

[<Sealed>]
type Sphere

[<System.Runtime.InteropServices.UnmanagedFunctionPointer (System.Runtime.InteropServices.CallingConvention.Cdecl)>]
type VboDelegate = delegate of unit -> unit

[<RequireQualifiedAccess>]
module Galileo =

    val spawnDefaultRedTriangle : unit -> Async<Triangle>

    val spawnDefaultBlueTriangle : unit -> Async<Triangle>

    val spawnDefaultSphere : unit -> Async<Sphere>
