namespace ZipkinPOC

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Newtonsoft.Json.Serialization
open Microsoft.OpenApi.Models
open POC.Common
open OpenTelemetry.Trace.Configuration
open OpenTelemetry.Exporter.Zipkin
open System

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =

        services.Configure<KafkaOptions>(this.Configuration.GetSection("KafkaOptions")) |> ignore
        
        services.AddSwaggerGen(fun c ->
            let info = OpenApiInfo()
            info.Title <- "TestMainService"
            info.Version <- "v1"
            c.SwaggerDoc("v1", info)) |> ignore

        // Add framework services.
        services.AddControllers().AddNewtonsoftJson(fun options -> options.SerializerSettings.ContractResolver <- new DefaultContractResolver()) |> ignore

        let addOpenTelemetry (builder: TracerBuilder) =
            let useZipkin (options: ZipkinTraceExporterOptions) =
                options.ServiceName <- "TestMainService"
                ()
            builder.AddRequestCollector() |> ignore
            builder.AddDependencyCollector() |> ignore
            builder.UseZipkin(Action<ZipkinTraceExporterOptions>(useZipkin)) |> ignore
            ()

        services.AddOpenTelemetry(Action<TracerBuilder>(addOpenTelemetry)) |> ignore
        
        services.AddHttpClient() |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseHttpsRedirection() |> ignore
        app.UseRouting() |> ignore

        app.UseStaticFiles() |> ignore
        app.UseSwagger() |> ignore
        app.UseSwaggerUI(fun c -> c.SwaggerEndpoint("/swagger/v1/swagger.json", "Http Service")) |> ignore

        app.UseAuthorization() |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapControllers() |> ignore
            ) |> ignore

    member val Configuration : IConfiguration = null with get, set
