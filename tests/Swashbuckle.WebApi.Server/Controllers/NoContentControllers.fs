namespace Swashbuckle.WebApi.Server.Controllers

open Microsoft.AspNetCore.Mvc

[<Route("api/[controller]")>]
[<ApiController>]
type NoContentController() =
    inherit ControllerBase()

    [<HttpGet>]
    [<Produces("application/json")>]
    [<ProducesResponseType(204)>]
    member x.Get() =
        x.NoContent()

    [<HttpPost>]
    [<Produces("application/json")>]
    [<ProducesResponseType(204)>]
    member x.Post() =
        x.NoContent()

    [<HttpPut>]
    [<Produces("application/json")>]
    [<ProducesResponseType(204)>]
    member x.Put() =
        x.NoContent()

    [<HttpDelete>]
    [<Produces("application/json")>]
    [<ProducesResponseType(204)>]
    member x.Delete() =
        x.NoContent()

[<Route("api/[controller]")>]
[<ApiController>]
type AcceptedController() =
    inherit ControllerBase()

    [<HttpGet>]
    [<Produces("application/json")>]
    [<ProducesResponseType(typeof<string>, 202)>]
    member x.Get() : ActionResult<string> =
        x.StatusCode(202, "accepted-value") |> ActionResult<string>

    [<HttpPost>]
    [<Produces("application/json")>]
    [<ProducesResponseType(typeof<string>, 202)>]
    member x.Post() : ActionResult<string> =
        x.StatusCode(202, "accepted-value") |> ActionResult<string>
