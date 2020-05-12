namespace POC.KafkaSubscriber

open Microsoft.Extensions.Options
open POC.Common
open Confluent.Kafka
open POC.Common.MongoRepository
open System
open OpenTelemetry.Trace.Configuration
open OpenTelemetry.Trace
open System.Diagnostics

type KafkaSubscriber(kafkaOptions: IOptions<KafkaOptions>, mongoRepo: MongoRepository, tracerFactory: TracerFactory) =

    let config = new ConsumerConfig(BootstrapServers = kafkaOptions.Value.Brokers,
                                    GroupId = "abc",
                                    EnableAutoCommit = Nullable(true),
                                    StatisticsIntervalMs = Nullable(5000),
                                    SessionTimeoutMs = Nullable(6000),
                                    AutoOffsetReset = Nullable(AutoOffsetReset.Earliest),
                                    EnablePartitionEof = Nullable(true))

    let tracer = tracerFactory.GetTracer("custom")
    
    member __.Start() =
        let task = async {
            use consumer = (new ConsumerBuilder<int, TestData>(config)).SetValueDeserializer(new Deserializer()).Build()
            consumer.Subscribe(kafkaOptions.Value.TopicName)
            while true do
                let msg = consumer.Consume(500)
                if msg = null || msg.IsPartitionEOF then return ()
                else
                    let data = msg.Message.Value
                    let traceId =
                        if msg.Message.Headers.Count > 0 then
                            let headers = msg.Message.Headers.GetLastBytes("TraceId")
                            ActivityTraceId.CreateFromUtf8String(ReadOnlySpan<_>headers)
                        else ActivityTraceId.CreateRandom()
                    let spanId = ActivitySpanId.CreateRandom()
                    let spanContext = new SpanContext(&traceId, &spanId, ActivityTraceFlags.None)
                    let span = tracer.StartSpan("mongo", &spanContext)
                    do! mongoRepo.upsert data
                    span.End()
        }
        do Async.StartImmediate task
        ()