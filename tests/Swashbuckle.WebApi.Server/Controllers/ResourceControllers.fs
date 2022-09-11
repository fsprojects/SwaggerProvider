namespace Swashbuckle.WebApi.Server.Controllers

open System.Collections.Generic
open Microsoft.AspNetCore.Mvc
open Swagger.Internal

[<ApiController>]
type ResourceController<'a, 'b when 'a: equality>(dict: System.Collections.Generic.Dictionary<'a, 'b>) =
    inherit ControllerBase()

    [<HttpGet; Consumes(MediaTypes.ApplicationJson); Produces(MediaTypes.ApplicationJson)>]
    member _.Get key =
        dict.[key] |> ActionResult<'b>

    [<HttpDelete>]
    member _.Delete key =
        dict.Remove(key) |> ignore

    [<HttpPut>]
    member _.Put (key) ([<FromBody>] value) =
        dict.Add(key, value)

    [<HttpPost>]
    member _.Post (key) ([<FromBody>] value) =
        dict.[key] <- value

module StaticResources =
    let storage = Dictionary<_, _>()

[<Route("api/ResourceStringString/{key}")>]
type ResourceStringStringController() =
    inherit ResourceController<string, string>(StaticResources.storage)
