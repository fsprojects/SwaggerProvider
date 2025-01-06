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
