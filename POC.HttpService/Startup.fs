namespace POC.HttpService

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open POC.Common.MongoRepository
open MongoDB.Driver
open Microsoft.Extensions.Options
open System
open Microsoft.OpenApi.Models
open Newtonsoft.Json.Serialization
open OpenTelemetry.Trace.Configuration
open OpenTelemetry.Exporter.Zipkin

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        services.Configure<MongoOptions>(this.Configuration.GetSection("MongoOptions")) |> ignore

        services.AddSingleton<IMongoDatabase>(fun provider ->
            let options = provider.GetService<IOptions<MongoOptions>>()
            let client = new MongoClient(options.Value.ConnectionString)
            client.GetDatabase(options.Value.DBName)) |> ignore

        services.AddSingleton<MongoRepository>(fun provider ->
            let options = provider.GetService<IOptions<MongoOptions>>()
            let database = provider.GetService<IMongoDatabase>()
            new MongoRepository(database, options.Value.CollectionName)) |> ignore

        services.AddSwaggerGen(fun c ->
            let info = OpenApiInfo()
            info.Title <- "TestHttpService"
            info.Version <- "v1"
            c.SwaggerDoc("v1", info)) |> ignore

        // Add framework services.
        services.AddControllers().AddNewtonsoftJson(fun options -> options.SerializerSettings.ContractResolver <- new DefaultContractResolver()) |> ignore

        let addOpenTelemetry (builder: TracerBuilder) =
            let useZipkin (options: ZipkinTraceExporterOptions) =
                options.ServiceName <- "TestHttpService"
                ()
            builder.AddRequestCollector() |> ignore
            builder.AddDependencyCollector() |> ignore
            builder.UseZipkin(Action<ZipkinTraceExporterOptions>(useZipkin)) |> ignore
            ()

        services.AddOpenTelemetry(Action<TracerBuilder>(addOpenTelemetry)) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment, serviceProvider: IServiceProvider) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore

        let mongoRepo = serviceProvider.GetService<MongoRepository>()
        mongoRepo.start() |> ignore

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