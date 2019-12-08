namespace Swashbuckle.WebApi.Server.Controllers

open System
open Microsoft.AspNetCore.Mvc
open Swagger.Internal

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnController<'a>(value:'a) =
    inherit ControllerBase()
    [<HttpGet; Consumes(MediaTypes.ApplicationJson); Produces(MediaTypes.ApplicationJson)>]
    member this.Get () = value |> ActionResult<'a>

    [<HttpPost; Consumes(MediaTypes.ApplicationJson); Produces(MediaTypes.ApplicationJson)>]
    member this.Post () = value |> ActionResult<'a>


type ReturnBooleanController () =
    inherit ReturnController<bool>(true)

type ReturnInt32Controller () =
    inherit ReturnController<int>(42)

type ReturnInt64Controller () =
    inherit ReturnController<int64>(42L)

type ReturnFloatController () =
    inherit ReturnController<float32>(42.0f)

type ReturnDoubleController () =
    inherit ReturnController<float>(42.0)

type ReturnStringController () =
    inherit ReturnController<string>("Hello world")

type ReturnDateTimeController () =
    inherit ReturnController<DateTime>(DateTime(2015,1,1))

type ReturnGuidController () =
    inherit ReturnController<Guid>(Guid.Empty)

type ReturnEnumController () =
    inherit ReturnController<UriKind>(UriKind.Absolute)

type ReturnArrayIntController () =
    inherit ReturnController<int array>([|1;2;3|])

type ReturnArrayEnumController () =
    inherit ReturnController<UriKind array>([|System.UriKind.Absolute; System.UriKind.Relative|])

type ReturnListIntController () =
    inherit ReturnController<int list>([1;2;3])

type ReturnSeqIntController () =
    inherit ReturnController<int seq>([1;2;3] |> List.toSeq)

type ReturnObjectPointClassController () =
    inherit ReturnController<Types.PointClass>(Types.PointClass(0,0))

type ReturnFileDescriptionController () =
    inherit ReturnController<Types.FileDescription>(Types.FileDescription("1.txt",[|1uy;2uy;3uy|]))
