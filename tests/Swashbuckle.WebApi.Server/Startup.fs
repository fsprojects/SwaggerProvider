namespace Swashbuckle.WebApi.Server


open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.OpenApi.Models
open System.Text.Json.Serialization
open Swashbuckle.WebApi.Server.Controllers

type Startup private () =
    new(configuration: IConfiguration) as this =
        Startup()
        then this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        // Add framework services.

        services
            .AddControllersWithViews()
            .AddJsonOptions(fun options ->
                let converters = options.JsonSerializerOptions.Converters
                converters.Add(JsonFSharpConverter())
                converters.Add(JsonStringEnumConverter()))
            .AddMvcOptions(fun options ->
                options.OutputFormatters.Add(CsvOutputFormatter())
                options.InputFormatters.Add(TextPlainInputFormatter()))
        |> ignore
        // Register the Swagger & OpenApi services
        services.AddSwaggerGen(fun c ->
            c.SwaggerDoc("v1", OpenApiInfo(Title = "My API", Version = "v1"))
            c.OperationFilter<BinaryContentFilter>())
        |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, _: IWebHostEnvironment) =
        app.UseDeveloperExceptionPage() |> ignore
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        //app.UseHsts() |> ignore

        // Register the Swagger generator and the Swagger UI middlewares
        app.UseSwagger(fun c ->
            c.RouteTemplate <- "/swagger/{documentName}/swagger.json"
            c.SerializeAsV2 <- true // false = v3 = OpenApi
        )
        |> ignore

        app.UseSwagger(fun c ->
            c.RouteTemplate <- "/swagger/{documentName}/openapi.json"
            c.SerializeAsV2 <- false // false = v3 = OpenApi
        )
        |> ignore

        app.UseSwaggerUI(fun c ->
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "My Swagger API v1")
            c.SwaggerEndpoint("/swagger/v1/openapi.json", "My OpenAPI API v1"))
        |> ignore

        //app.UseHttpsRedirection() |> ignore
        app.UseRouting() |> ignore

        app.UseEndpoints(fun endpoints -> endpoints.MapControllers() |> ignore)
        |> ignore

    member val Configuration: IConfiguration = null with get, set
