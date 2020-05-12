namespace POC.Common

open Confluent.Kafka
open System.Runtime.Serialization.Formatters.Binary
open System.IO

type Serializer() =
    interface ISerializer<TestData> with
        member __.Serialize(data: TestData, _: SerializationContext) =
            let bf = new BinaryFormatter()
            use memStream = new MemoryStream()
            bf.Serialize(memStream, data)
            memStream.ToArray()