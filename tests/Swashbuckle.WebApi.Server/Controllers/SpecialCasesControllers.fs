namespace Swashbuckle.WebApi.Server.Controllers

open Microsoft.AspNetCore.Mvc

[<Route("api/[controller]")>]
[<ApiController>]
type MultiFormatController() =
    inherit ControllerBase()

    [<HttpGet>]
    member _.Get() = "0.0"
