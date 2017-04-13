open Microsoft.Owin.Hosting
open Owin
open System
open System.Web.Http
open Swashbuckle.Application


let getAppBuilder() =
    let config = new HttpConfiguration()
    // Web API routes
    config.MapHttpAttributeRoutes();
    // Configure routes
    config.Routes.MapHttpRoute("default",  "api/{controller}") |> ignore
    // Enable Swagger and Swagger UI
    config
        .EnableSwagger(fun c -> c.SingleApiVersion("v1", "Test Controllers for SwaggerProvider") |> ignore)
        .EnableSwaggerUi();

    fun (appBuilder:IAppBuilder) ->
        appBuilder.UseWebApi(config) |> ignore


[<EntryPoint>]
let main argv =
    try
        let hostAddress = "http://localhost:8735"
        use server = WebApp.Start(hostAddress, getAppBuilder())

        let swaggerUiUrl = sprintf "%s/swagger/ui/index" hostAddress
        printfn "Web server up and running on %s\n" hostAddress
        printfn "Swagger UI is running on %s" swaggerUiUrl
        printfn "Swagger Json Schema is available on %s/swagger/docs/v1" hostAddress

        // printf  "\nPress Enter to open Swagger UI"
        // Console.ReadLine() |> ignore
        // System.Diagnostics.Process.Start(swaggerUiUrl) |> ignore

        let rec exitLoop n =
            printf  "\nPlease enter 'q' to exit (%d):" n
            match (Console.ReadLine()) with
            | "q" | "Q" -> ()
            | _ ->
                printfn "Sleep (%d) 5000" n
                System.Threading.Thread.Sleep(5000)
                exitLoop (n+1)
        //exitLoop 0
        System.Threading.Thread.Sleep(60*60*1000)
    with
    | e ->
        printfn "Exception %A" e
        raise e
    0 // return an integer exit code
