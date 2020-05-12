namespace POC.Common

open Confluent.Kafka
open System.Runtime.Serialization.Formatters.Binary
open System.IO

type Deserializer() =
    interface IDeserializer<TestData> with
        member __.Deserialize(data, _, _) =
            let bf = new BinaryFormatter()
            let arr = data.ToArray()
            use memStream = new MemoryStream(arr)
            let o = bf.Deserialize(memStream)
            o :?> TestData