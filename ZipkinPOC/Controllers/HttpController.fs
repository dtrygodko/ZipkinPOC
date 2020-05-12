namespace ZipkinPOC.Controllers

open Microsoft.AspNetCore.Mvc
open System.Net.Http
open Microsoft.Extensions.Configuration
open System.Net
open POC.Common
open Microsoft.Extensions.Options
open Confluent.Kafka
open OpenTelemetry.Trace.Configuration

[<ApiController>]
[<Route("[controller]")>]
type HttpController (clientFactory: IHttpClientFactory,
                     configuration: IConfiguration,
                     kafkaOptions: IOptions<KafkaOptions>,
                     tracerFactory: TracerFactory) =
    inherit ControllerBase()

    let config = new ProducerConfig(BootstrapServers = kafkaOptions.Value.Brokers)
    let tracer = tracerFactory.GetTracer("custom")
    let testDataSerialzer = new Serializer()
    let stringSerializer = Serializers.Utf8

    [<HttpGet("{id}")>]
    member this.Get(id: int) = async {
        tracer.CurrentSpan.SetAttribute("id", id)
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
        use producer = (new ProducerBuilder<int, TestData>(config)).SetValueSerializer(testDataSerialzer).Build()
        let traceId = new Header("TraceId", stringSerializer.Serialize(tracer.CurrentSpan.Context.TraceId.ToHexString(), new SerializationContext()))
        let headers = (new Headers())
        headers.Add(traceId)
        let! _ = producer.ProduceAsync(kafkaOptions.Value.TopicName,
                                       new Message<int, TestData>(Key = data.Id,
                                                                  Value = data,
                                                                  Headers = headers)) |> Async.AwaitTask
        return this.Ok()
    }
