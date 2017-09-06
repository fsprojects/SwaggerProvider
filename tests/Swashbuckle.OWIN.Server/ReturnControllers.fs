namespace Controllers

open System
open System.Collections.Generic
open System.Net
open System.Net.Http
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

type ReturnFileDescriptionController () =
    inherit ReturnController<Types.FileDescription>(Types.FileDescription("1.txt",[|1uy;2uy;3uy|]))

/// These are special snowflakes. See the customizations we have to make in the swagger registration via filters.
type ReturnFileController () =
    inherit ApiController()
     member x.Get () = 
        let bytes = System.Text.Encoding.UTF8.GetBytes("I am totally a file's\ncontent")
        let response = new HttpResponseMessage(HttpStatusCode.OK)
        response.Content <- new StreamContent(new System.IO.MemoryStream(bytes))
        response.Content.Headers.ContentType <- Headers.MediaTypeHeaderValue("application/octet-stream")
        response

    /// echoes back the first file sent
    member x.Post () = 
        if not <| x.Request.Content.IsMimeMultipartContent() 
        then raise (HttpResponseException(HttpStatusCode.UnsupportedMediaType))
        x.Request.Content.LoadIntoBufferAsync().Wait()
        printfn "got a multipart request"
        printfn "stream length is %d" (x.Request.Content.ReadAsStreamAsync().Result.Length)
        let printHeader (h: KeyValuePair<string, IEnumerable<string>>) = printfn "%s: %A" h.Key h.Value
        x.Request.Headers |> Seq.iter printHeader
        x.Request.Content.Headers |> Seq.iter printHeader
        
        // Read the form data and return an async task.
        let root = System.IO.Path.GetTempPath()
        let multipartProvider = MultipartFormDataStreamProvider(root)
        try
            let written = x.Request.Content.ReadAsMultipartAsync(multipartProvider) |> Async.AwaitTask |> Async.RunSynchronously
            written.FileData |> Seq.iter (fun fileData -> printfn "saved to %s" fileData.LocalFileName)
            written.FileData |> Seq.map (fun fileData -> fileData.Headers.ContentDisposition.Name, fileData.Headers.ContentLength)
        with
        | e -> 
            printfn "%O" e
            raise e