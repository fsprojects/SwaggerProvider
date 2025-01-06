namespace Swashbuckle.WebApi.Server

open System
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting

module Program =
    let exitCode = 0

    let CreateWebHostBuilder args =
        WebHost.CreateDefaultBuilder(args).UseStartup<Startup>()

    [<EntryPoint>]
    let main args =
        let webHost = CreateWebHostBuilder(args).Build()
        let _ = webHost.RunAsync()

        printfn "Swagger UI is running on /swagger"
        printfn "Send <something> to input stream to shut down."
        Console.Read() |> ignore

        printfn "Stopping WebApi ..."
        webHost.StopAsync() |> Async.AwaitTask |> Async.RunSynchronously

        exitCode
