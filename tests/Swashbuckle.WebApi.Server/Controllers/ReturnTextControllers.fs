namespace Swashbuckle.WebApi.Server.Controllers

open System.Text
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Mvc.Formatters
open Swagger.Internal

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnPlainController() =
    [<HttpGet; Consumes(MediaTypes.ApplicationJson); Produces("text/plain")>]
    member this.Get() =
        "Hello world" |> ActionResult<string>

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnCsvController() =
    [<HttpGet; Consumes(MediaTypes.ApplicationJson); Produces("text/csv")>]
    member this.Get() =
        "Hello,world" |> ActionResult<string>

// Simple CSV output formatter
// This formatter assumes the controller returns a string (already CSV-formatted)
type CsvOutputFormatter() as this =
    inherit TextOutputFormatter()

    do
        this.SupportedMediaTypes.Add("text/csv")
        this.SupportedEncodings.Add(Encoding.UTF8)
        this.SupportedEncodings.Add(Encoding.Unicode)

    override _.CanWriteType(t) =
        // Accept string type only (for simplicity)
        t = typeof<string>

    override _.WriteResponseBodyAsync(context, encoding) =
        let response = context.HttpContext.Response
        let value = context.Object :?> string
        let bytes = encoding.GetBytes(value)
        response.Body.WriteAsync(bytes, 0, bytes.Length)
