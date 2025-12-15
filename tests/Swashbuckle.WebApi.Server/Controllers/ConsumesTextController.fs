namespace Swashbuckle.WebApi.Server.Controllers

open Microsoft.AspNetCore.Mvc
open Swagger.Internal

[<Route("api/[controller]")>]
[<ApiController>]
type ConsumesTextController() =
    [<HttpPost; Consumes("text/plain"); Produces("text/plain")>]
    member this.Post([<FromBody>] request: string) =
        request |> ActionResult<string>
