namespace Swashbuckle.WebApi.Server.Controllers

open Microsoft.AspNetCore.Mvc

[<Route("api/[controller]")>]
[<ApiController>]
type MultiFormatController() =
    inherit ControllerBase()

    [<HttpGet>]
    member _.Get() = "0.0"

/// Controller that echoes a string path parameter, used to test that special
/// characters (e.g. `$0`) in path parameter values are passed through correctly.
[<Route("api/[controller]/{value}")>]
[<ApiController>]
type EchoPathController() =
    inherit ControllerBase()

    [<HttpGet; Produces("text/plain")>]
    member _.Get(value: string) = value
