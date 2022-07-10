namespace Swashbuckle.WebApi.Server.Controllers

open System
open Microsoft.AspNetCore.Mvc
open System.Runtime.InteropServices
open Swagger.Internal

[<Route("api/[controller]")>]
[<ApiController>]
type UpdateController<'a>(f: 'a -> 'a) =
    inherit ControllerBase()

    [<HttpGet; Consumes(MediaTypes.ApplicationJson); Produces(MediaTypes.ApplicationJson)>]
    member this.Get([<FromQuery>] x) =
        f x |> ActionResult<'a>

    [<HttpPost; Consumes(MediaTypes.ApplicationJson); Produces(MediaTypes.ApplicationJson)>]
    member this.Post x =
        f x |> ActionResult<'a>

type UpdateBoolController() =
    inherit UpdateController<bool>(not)

type UpdateInt32Controller() =
    inherit UpdateController<int>((+) 1)

type UpdateInt64Controller() =
    inherit UpdateController<int64>((+) 1L)

type UpdateFloatController() =
    inherit UpdateController<float32>((+) 1.0f)

type UpdateDoubleController() =
    inherit UpdateController<float>((+) 1.0)

type UpdateStringController() =
    inherit UpdateController<string>((+) "Hello, ")

type UpdateDateTimeController() =
    inherit UpdateController<DateTime>(fun x -> x.AddDays(1.0))

type UpdateGuidController() =
    inherit UpdateController<Guid>(id)

type UpdateEnumController() =
    inherit UpdateController<UriKind>(id)

type UpdateArrayIntController() =
    inherit UpdateController<int[]>(Array.rev)

type UpdateArrayEnumController() =
    inherit UpdateController<UriKind[]>(Array.rev)

type UpdateArrayGuidController() =
    inherit UpdateController<Guid[]>(Array.rev)

type UpdateListIntController() =
    inherit UpdateController<int list>(List.rev)

type UpdateSeqIntController() =
    inherit UpdateController<int seq>(Seq.toList >> List.rev >> Seq.ofList)

type UpdateObjectPointClassController() =
    inherit UpdateController<Types.PointClass>(fun p -> Types.PointClass(p.Y, p.X))

[<Route("api/[controller]")>]
[<ApiController>]
type UpdateObjectFileDescriptionClassController() =
    inherit ControllerBase()

    [<HttpGet>]
    member this.Get([<FromQuery>] x) =
        Types.FileDescription("1.txt", x) |> ActionResult<_>

    [<HttpPost>]
    member this.Post(x: Types.FileDescription) =
        ActionResult<_>(x)

[<Route("api/[controller]")>]
[<ApiController>]
type UpdateWithOptionalIntController() =
    inherit ControllerBase()

    [<HttpGet>]
    member this.Get([<FromQuery>] x, [<FromQuery; Optional; DefaultParameterValue(1)>] y: int) =
        x + y |> ActionResult<_>
