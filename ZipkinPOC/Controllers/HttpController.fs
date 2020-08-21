namespace ZipkinPOC.Controllers

open Microsoft.AspNetCore.Mvc
open System.Net.Http
open Microsoft.Extensions.Configuration
open System.Net
open POC.Common
open Microsoft.Extensions.Options
open Confluent.Kafka
open BetLab.Infrastructure.Messaging.Kafka.Producer
open BetLab.Infrastructure.Messaging.Kafka
open Microsoft.Extensions.Logging.Abstractions
open BetLab.Infrastructure.Messaging.Kafka.Observers

[<ApiController>]
[<Route("[controller]")>]
type HttpController (clientFactory: IHttpClientFactory,
                     configuration: IConfiguration,
                     kafkaOptions: IOptions<POC.Common.KafkaOptions>) =
    inherit ControllerBase()

    let config = ["auto.offset.reset", "smallest";"queue.buffering.max.ms", "500";"compression.codec", "lz4";"message.max.bytes", "10000000";"acks", "1"] |> dict
    let testDataSerialzer = new Serializer()

    [<HttpGet("{id}")>]
    member this.Get(id: int) = async {
        let client = clientFactory.CreateClient()
        let url = configuration.GetValue<string>("HttpService")
        let request = new HttpRequestMessage(HttpMethod.Get, url + "/Test/" + sprintf "%i" id)
        let! resp = client.SendAsync(request) |> Async.AwaitTask
        if resp.StatusCode <> HttpStatusCode.OK then return this.NotFound() :> IActionResult
        else
            let! response = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
            return this.Ok(response) :> IActionResult
    }

    [<HttpPost()>]
    member this.Post([<FromBody>] data: TestData) = async {
        let producer = new KafkaProducer<int, TestData>(
            "producer",
            new KafkaProducerOptions(
                "127.0.0.1:9092",
                "murmur2old",
                Seq.empty,
                VerbosityLevel.Normal,
                config),
            Serializers.Int32,
            testDataSerialzer,
            new NullObserver(),
            NullLogger.Instance)
        let! _ = producer.ProduceAsync(
            data.Id,
            data,
            kafkaOptions.Value.TopicName) |> Async.AwaitTask
        return this.Ok()
    }
