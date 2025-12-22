namespace Swashbuckle.WebApi.Server.Controllers

open System.IO
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Mvc.Formatters
open Swagger.Internal

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnPlainController() =
    [<HttpGet; Produces("text/plain")>]
    member this.Get() =
        "Hello world" |> ActionResult<string>

[<Route("api/[controller]")>]
[<ApiController>]
type ReturnCsvController() =
    [<HttpGet; Produces("text/csv")>]
    member this.Get() =
        "Hello,world" |> ActionResult<string>

[<Route("api/[controller]")>]
[<ApiController>]
type ConsumesTextController() =
    [<HttpPost; Consumes("text/plain"); Produces("text/plain")>]
    member this.Post([<FromBody>] request: string) =
        request |> ActionResult<string>

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

// Text/plain input formatter for reading plain text request bodies
type TextPlainInputFormatter() as this =
    inherit TextInputFormatter()

    do
        this.SupportedMediaTypes.Add("text/plain")
        this.SupportedEncodings.Add(Encoding.UTF8)
        this.SupportedEncodings.Add(Encoding.Unicode)

    override _.CanRead(context) =
        context.ModelType = typeof<string>

    override _.ReadRequestBodyAsync(context, encoding) =
        task {
            use reader = new StreamReader(context.HttpContext.Request.Body, encoding)
            let! content = reader.ReadToEndAsync()
            return InputFormatterResult.Success(content)
        }
