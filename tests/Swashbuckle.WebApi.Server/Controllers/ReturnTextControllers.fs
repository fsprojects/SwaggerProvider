namespace Swashbuckle.WebApi.Server.Controllers

open Microsoft.AspNetCore.Mvc
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
