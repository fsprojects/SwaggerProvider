namespace Swashbuckle.WebApi.Server

open System
open System.Collections.Generic
open System.Linq
open System.Reflection.Metadata
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.OpenApi.Models

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        // Add framework services.
        services
          .AddMvc(fun option -> option.EnableEndpointRouting <- false)
          .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
        |> ignore
        // Register the Swagger & OpenApi services
        services.AddSwaggerGen(fun c ->
            c.SwaggerDoc("v1", OpenApiInfo(Title = "My API", Version = "v1"));
        ) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        app.UseDeveloperExceptionPage() |> ignore
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        //app.UseHsts() |> ignore

        // Register the Swagger generator and the Swagger UI middlewares
        app.UseSwagger(fun c ->
            c.SerializeAsV2 <- false // false = v3 = OpenApi
        ) |> ignore
        app.UseSwaggerUI(fun c ->
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
        ) |> ignore

        //app.UseHttpsRedirection() |> ignore
        app.UseMvc() |> ignore

    member val Configuration : IConfiguration = null with get, set
