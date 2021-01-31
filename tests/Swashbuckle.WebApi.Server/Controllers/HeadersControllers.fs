namespace Swashbuckle.WebApi.Server.Controllers

open System
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Swagger.Internal
open Swashbuckle.AspNetCore.Filters

[<Route("api/[controller]")>]
[<ApiController>]
type HeadersController () =
    inherit ControllerBase()
    [<HttpGet; SwaggerResponseHeader(StatusCodes.Status200OK, "ETag", "string", "An ETag of the resource")>]
    [<Consumes(MediaTypes.ApplicationJson); Produces(MediaTypes.ApplicationJson)>]
    member this.Get ([<FromQuery>]x: string) =
        this.Response.Headers.Add(HeaderNames.ETag, StringValues(Guid.NewGuid().ToString()))
        this.Ok(x)
    [<HttpPost; SwaggerResponseHeader(StatusCodes.Status201Created, "Location", "string", "Location of the newly created resource")>]
    [<Consumes(MediaTypes.ApplicationJson); ProducesResponseType(StatusCodes.Status201Created, Type=typeof<string>)>]
    member this.Post ([<FromBody>]x: string) =
        this.Created(Guid.NewGuid().ToString(), x)
