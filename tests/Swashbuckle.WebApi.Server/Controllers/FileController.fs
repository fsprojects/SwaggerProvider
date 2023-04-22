namespace Swashbuckle.WebApi.Server.Controllers

open System
open System.IO
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.OpenApi.Models
open Swagger.Internal
open Swashbuckle.AspNetCore.SwaggerGen

type FormWithFile() =
    member val Name: string = "" with get, set
    member val File: IFormFile = null with get, set

// https://stackoverflow.com/questions/41141137/how-can-i-tell-swashbuckle-that-the-body-content-is-required
type BinaryContentAttribute() =
    inherit Attribute()

type BinaryContentFilter() =
    interface IOperationFilter with
        member _.Apply(op, ctx) =
            let att = ctx.MethodInfo.GetCustomAttributes(typeof<BinaryContentAttribute>, false)

            if att.Length > 0 then
                op.RequestBody <- OpenApiRequestBody(Required = true)

                op.RequestBody.Content.Add(
                    MediaTypes.ApplicationOctetStream,
                    OpenApiMediaType(Schema = OpenApiSchema(Type = "string", Format = "binary"))
                )

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnFileController() =
    inherit ControllerBase()

    [<HttpGet; Produces(MediaTypes.ApplicationOctetStream, Type = typeof<FileResult>)>]
    member this.Get() =
        let bytes = System.Text.Encoding.UTF8.GetBytes("I am totally a file's\ncontent")
        let stream = new MemoryStream(bytes)
        this.File(stream, MediaTypes.ApplicationOctetStream, "hello.txt") :> FileResult

    [<HttpPost("stream"); BinaryContent>]
    member this.GetFileLength() =
        task {
            use reader = new StreamReader(this.Request.Body)
            let! content = reader.ReadToEndAsync()
            return content.Length
        }

    [<HttpPost("single"); Produces(MediaTypes.ApplicationOctetStream, Type = typeof<FileResult>)>]
    member this.PostFile(file: IFormFile) : FileResult =
        this.File(file.OpenReadStream(), MediaTypes.ApplicationOctetStream, file.FileName) :> FileResult

    [<HttpPost("multiple"); Produces(MediaTypes.ApplicationJson, Type = typeof<int>)>]
    member this.PostFiles(files: IFormFileCollection) =
        files.Count // return 0 when you call from Swagger UI

    [<HttpPost("form-with-file"); Produces(MediaTypes.ApplicationOctetStream, Type = typeof<FileResult>)>]
    member this.PostFormWithFile([<FromForm>] formWithFile: FormWithFile) : FileResult =
        this.File(formWithFile.File.OpenReadStream(), MediaTypes.ApplicationOctetStream, formWithFile.Name) :> FileResult
