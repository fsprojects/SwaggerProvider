namespace Swashbuckle.WebApi.Server.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http

type FormWithFile() =
    member val Name:string = "" with get, set
    member val File:IFormFile = null with get, set

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnFileController () =
    inherit ControllerBase()

    [<HttpGet; Produces(Application.OctetStream, Type = typeof<FileResult>)>]
    member this.Get () =
        let bytes = System.Text.Encoding.UTF8.GetBytes("I am totally a file's\ncontent")
        let stream = new System.IO.MemoryStream(bytes)
        this.File(stream, Application.OctetStream, "hello.txt") :> FileResult

    [<HttpPost("single"); Produces(Application.OctetStream, Type = typeof<FileResult>)>]
    member this.PostFile (file:IFormFile) :FileResult =
        this.File(file.OpenReadStream(), Application.OctetStream, file.Name) :> FileResult

    [<HttpPost("multiple"); Produces(Application.Json, Type=typeof<int>)>]
    member this.PostFiles (files:IFormFileCollection) =
        files.Count // ??? 0

    [<HttpPost("form-with-file"); Produces(Application.OctetStream, Type = typeof<FileResult>)>]
    member this.PostFormWithFile ([<FromForm>] formWithFile:FormWithFile) :FileResult =
        this.File(formWithFile.File.OpenReadStream(), Application.OctetStream, formWithFile.Name) :> FileResult
