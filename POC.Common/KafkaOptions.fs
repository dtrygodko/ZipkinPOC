namespace POC.Common

[<AllowNullLiteral>]
type KafkaOptions() =
    member val Brokers: string = null with get, set
    member val TopicName: string = null with get, set