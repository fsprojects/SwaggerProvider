namespace Swashbuckle.WebApi.Server.Controllers

open System
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Swagger.Internal

type FormWithFile() =
    member val Name:string = "" with get, set
    member val File:IFormFile = null with get, set

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnFileController () =
    inherit ControllerBase()

    [<HttpGet; Produces(MediaTypes.ApplicationOctetStream, Type = typeof<FileResult>)>]
    member this.Get () =
        let bytes = System.Text.Encoding.UTF8.GetBytes("I am totally a file's\ncontent")
        let stream = new System.IO.MemoryStream(bytes)
        this.File(stream, MediaTypes.ApplicationOctetStream, "hello.txt") :> FileResult

    [<HttpPost("single"); Produces(MediaTypes.ApplicationOctetStream, Type = typeof<FileResult>)>]
    member this.PostFile (file:IFormFile) :FileResult =
        if isNull file then raise <| NullReferenceException("file is null")
        this.File(file.OpenReadStream(), MediaTypes.ApplicationOctetStream, file.FileName) :> FileResult

    [<HttpPost("multiple"); Produces(MediaTypes.ApplicationJson, Type=typeof<int>)>]
    member this.PostFiles (files:IFormFileCollection) =
        if isNull files then raise <| NullReferenceException("files is null")
        files.Count // ??? 0

    [<HttpPost("form-with-file"); Produces(MediaTypes.ApplicationOctetStream, Type = typeof<FileResult>)>]
    member this.PostFormWithFile ([<FromForm>] formWithFile:FormWithFile) :FileResult =
        this.File(formWithFile.File.OpenReadStream(), MediaTypes.ApplicationOctetStream, formWithFile.Name) :> FileResult
