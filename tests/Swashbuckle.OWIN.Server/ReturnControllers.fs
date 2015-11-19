namespace Controllers

open System
open System.Web.Http

type ReturnController<'T>(value:'T) =
    inherit ApiController()

    member this.Get () = value
    member this.Post () = value


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