namespace POC.KafkaService

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open POC.Common
open Microsoft.Extensions.Configuration
open POC.Common.MongoRepository
open MongoDB.Driver
open Microsoft.Extensions.Options
open System
open POC.KafkaSubscriber
open OpenTelemetry.Trace.Configuration
open OpenTelemetry.Exporter.Zipkin

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member this.ConfigureServices(services: IServiceCollection) =
        
        services.Configure<KafkaOptions>(this.Configuration.GetSection("KafkaOptions")) |> ignore
        services.Configure<MongoOptions>(this.Configuration.GetSection("MongoOptions")) |> ignore
        
        services.AddSingleton<IMongoDatabase>(fun provider ->
            let options = provider.GetService<IOptions<MongoOptions>>()
            let client = new MongoClient(options.Value.ConnectionString)
            client.GetDatabase(options.Value.DBName)) |> ignore
        
        services.AddSingleton<MongoRepository>(fun provider ->
            let options = provider.GetService<IOptions<MongoOptions>>()
            let database = provider.GetService<IMongoDatabase>()
            new MongoRepository(database, options.Value.CollectionName)) |> ignore

        services.AddSingleton<KafkaSubscriber>() |> ignore

        let addOpenTelemetry (builder: TracerBuilder) =
            let useZipkin (options: ZipkinTraceExporterOptions) =
                options.ServiceName <- "TestKafkaService"
                ()
            builder.AddRequestCollector() |> ignore
            builder.AddDependencyCollector() |> ignore
            builder.UseZipkin(Action<ZipkinTraceExporterOptions>(useZipkin)) |> ignore
            ()

        services.AddOpenTelemetry(Action<TracerBuilder>(addOpenTelemetry)) |> ignore
        
        ()

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment, serviceProvider: IServiceProvider) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        let mongoRepo = serviceProvider.GetService<MongoRepository>()
        mongoRepo.start() |> ignore
        let kafkaSubscriber = serviceProvider.GetService<KafkaSubscriber>()
        kafkaSubscriber.Start() |> ignore

        app.UseRouting() |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapGet("/", fun context -> context.Response.WriteAsync("Hello World!")) |> ignore
            ) |> ignore

    member val Configuration : IConfiguration = null with get, set
