namespace NSwag.WebApi.Server

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

module Routes =
    let Root = "/swagger"
    let SwaggerDocumentName = "Swagger"
    let SwaggerPath = Root + "/v1/swagger.json"
    let OpenApiDocumentName = "OpenApi"
    let OpenApiPath = Root + "/v1/openapi.json"

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        // Add framework services.
        services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2) |> ignore
         // Register the Swagger & OpenApi services
        services.AddSwaggerDocument(fun config ->
            config.DocumentName <- Routes.SwaggerDocumentName
            config.PostProcess <- fun document ->
                document.Info.Title <- "Test WebAPI Server (v2 / Swagger)"
        ).AddOpenApiDocument(fun config ->
            config.DocumentName <- Routes.OpenApiDocumentName
            config.PostProcess <- fun document ->
                document.Info.Title <- "Test WebAPI Server (v3 / OpenApi)"
        ) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IHostingEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore
        else
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts() |> ignore

        // Register the Swagger generator and the Swagger UI middlewares
        app.UseOpenApi(fun options ->
            options.DocumentName <- Routes.SwaggerDocumentName
            options.Path <- Routes.SwaggerPath
        ).UseOpenApi(fun options ->
            options.DocumentName <- Routes.OpenApiDocumentName
            options.Path <- Routes.OpenApiPath
        ).UseSwaggerUi3(fun options ->
            options.SwaggerRoutes.Add(NSwag.AspNetCore.SwaggerUi3Route(Routes.SwaggerDocumentName, Routes.SwaggerPath))
            options.SwaggerRoutes.Add(NSwag.AspNetCore.SwaggerUi3Route(Routes.OpenApiDocumentName, Routes.OpenApiPath))

            options.Path <- Routes.Root
        ) |> ignore

        app.UseHttpsRedirection() |> ignore
        app.UseMvc() |> ignore

    member val Configuration : IConfiguration = null with get, set
