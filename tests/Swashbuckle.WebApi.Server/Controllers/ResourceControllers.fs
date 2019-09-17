namespace Swashbuckle.WebApi.Server.Controllers

open System.Collections.Generic
open Microsoft.AspNetCore.Mvc

[<ApiController>]
type ResourceController<'a,'b when 'a: equality> (dict:System.Collections.Generic.Dictionary<'a,'b>)=
    inherit ControllerBase()
    [<HttpGet>]
    member __.Get key = dict.[key] |> ActionResult<_>
    [<HttpDelete>]
    member __.Delete key = dict.Remove(key) |> ignore
    [<HttpPut>]
    member __.Put (key) ([<FromBody>]value) = dict.Add(key, value)
    [<HttpPost>]
    member __.Post (key) ([<FromBody>]value) = dict.[key] <- value

module StaticResources =
    let storage = Dictionary<_, _> ()

[<Route("api/ResourceStringString/{key}")>]
type ResourceStringStringController () =
    inherit ResourceController<string,string>(StaticResources.storage)
