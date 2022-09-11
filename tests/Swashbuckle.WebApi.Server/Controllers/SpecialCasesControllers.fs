namespace Swashbuckle.WebApi.Server.Controllers

open System
open Microsoft.AspNetCore.Mvc
open Swagger.Internal

[<Route("api/[controller]")>]
[<ApiController>]
type MultiFormatController() =
    inherit ControllerBase()

    [<HttpGet>]
    member _.Get() = "0.0"
