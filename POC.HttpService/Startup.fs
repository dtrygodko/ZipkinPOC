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
open Elastic.Apm.AspNetCore
open Elastic.Apm.DiagnosticSource
open Elastic.Apm.Mongo
open POC.Common
open BetLab.Infrastructure.Messaging.Kafka.Listener
open BetLab.Infrastructure.Messaging.Kafka
open Confluent.Kafka
open BetLab.Infrastructure.Messaging.Kafka.Observers
open Microsoft.Extensions.Logging.Abstractions
open System.Threading.Tasks

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        services.Configure<MongoOptions>(this.Configuration.GetSection("MongoOptions")) |> ignore

        services.AddSingleton<IMongoDatabase>(fun provider ->
            let options = provider.GetService<IOptions<MongoOptions>>()
            let settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString)
            settings.ClusterConfigurator <- fun builder -> (builder.Subscribe(new MongoEventSubscriber()) |> ignore)
            let client = new MongoClient(settings)
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

        

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment, serviceProvider: IServiceProvider) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseElasticApm(this.Configuration, new HttpDiagnosticsSubscriber(), new MongoDiagnosticsSubscriber()) |> ignore

        let mongoRepo = serviceProvider.GetService<MongoRepository>()
        mongoRepo.start() |> ignore

        let config = ["auto.offset.reset", "smallest";"queue.buffering.max.ms", "500";"compression.codec", "lz4";"message.max.bytes", "10000000";"acks", "1";"group.id", "dev"] |> dict

        let testDataDeserialzer = new Deserializer()

        let subscriber = new KafkaListener<int, TestData>(
            "listener",
            new KafkaListenerOptions(
                "localhost:9092",
                "testDataTopic",
                "dev",
                VerbosityLevel.Normal,
                config),
            (fun _ _ -> Task.Delay(5000)),
            Deserializers.Int32,
            testDataDeserialzer,
            new NullObserver(),
            NullLogger.Instance)

        subscriber.Start()

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
