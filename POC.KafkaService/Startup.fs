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
open Elastic.Apm.AspNetCore
open Elastic.Apm.DiagnosticSource
open BetLab.Infrastructure.Messaging.Kafka.Listener
open Confluent.Kafka
open BetLab.Infrastructure.Messaging.Kafka
open Microsoft.Extensions.Logging.Abstractions
open BetLab.Infrastructure.Messaging.Kafka.Observers
open System.Threading.Tasks

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
        
        ()

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment, serviceProvider: IServiceProvider) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseElasticApm(this.Configuration, new HttpDiagnosticsSubscriber()) |> ignore

        let config = ["auto.offset.reset", "smallest";"queue.buffering.max.ms", "500";"compression.codec", "lz4";"message.max.bytes", "10000000";"acks", "1";"group.id", "dev"] |> dict
        let testDataSerialzer = new Deserializer()

        let subscriber = new KafkaListener<int, TestData>(
            "listener",
            new KafkaListenerOptions(
                "localhost:9092",
                "murmur2old",
                "dev",
                VerbosityLevel.Normal,
                config),
            (fun _ _ -> async.Zero() |> Async.StartAsTask :> Task),
            Deserializers.Int32,
            testDataSerialzer,
            new NullObserver(),
            NullLogger.Instance)

        subscriber.Start()
        
        let mongoRepo = serviceProvider.GetService<MongoRepository>()
        mongoRepo.start() |> ignore

        app.UseRouting() |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapGet("/", fun context -> context.Response.WriteAsync("Hello World!")) |> ignore
            ) |> ignore

    member val Configuration : IConfiguration = null with get, set
