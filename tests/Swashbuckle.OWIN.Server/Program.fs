open Microsoft.Owin.Hosting
open Owin
open System
open System.Web.Http
open Swashbuckle.Application


let getAppBuilder() =
    let config = new HttpConfiguration()
    // Configure routes
    config.Routes
        .MapHttpRoute("default", "{controller}") |> ignore
    // Enable Swagger and Swagger UI
    config
        .EnableSwagger(fun c -> c.SingleApiVersion("v1", "Test Controllers for SwaggerProvider") |> ignore)
        .EnableSwaggerUi();

    fun (appBuilder:IAppBuilder) ->
        appBuilder.UseWebApi(config) |> ignore


[<EntryPoint>]
let main argv =
    let hostAddress = "http://localhost:8735"
    let server = WebApp.Start(hostAddress, getAppBuilder())

    printfn "Web server up and running on %s\n" hostAddress
    printfn "Swagger UI is running on %s/swagger/ui/index" hostAddress
    printfn "Swagger Json Schema is available on %s/swagger/docs/v1" hostAddress
    printf  "\nPress any key to stop"

    Console.ReadKey() |> ignore

    server.Dispose()
    0 // return an integer exit code
