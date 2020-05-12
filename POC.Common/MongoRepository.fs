namespace POC.Common

open MongoDB.Driver
module MongoRepository =

    type MongoOptions() =
        member val ConnectionString: string = null with get, set
        member val DBName: string = null with get, set
        member val CollectionName: string = null with get, set

    let asyncMap f =
        let f x = async.Bind (x,  (fun x -> async.Return (f x)))
        f
    let asyncBind f x = async.Bind(x, f)

    let asyncIgnore<'a> : Async<'a> -> Async<unit> = asyncMap ignore

    type MongoRepository
        (db: IMongoDatabase, 
         collectionName: string) =
        let mutable _collection: IMongoCollection<TestData> = null

        member __.start () =
            _collection <- db.GetCollection<TestData> collectionName

        member __.get (id: int) = async {
            let! collection = _collection.Find(fun x -> x.Id = id).ToListAsync() |> Async.AwaitTask
            return collection |> Seq.tryFind (fun _ -> true) 
        }

        member __.upsert (data: TestData) = async {
            let options = new ReplaceOptions()
            options.IsUpsert <- true
            let! _ = _collection.ReplaceOneAsync((fun x -> x.Id = data.Id), data, options) |> Async.AwaitTask
            ()
        }

        member this.upsertMany (rows: TestData seq) =
            rows
            |> Seq.map (fun row -> this.upsert row)
            |> Async.Parallel |> asyncIgnore

        member this.delete (id: int) = async {
            let! row = this.get id
            match row with
            |Some _ -> let! _ = _collection.DeleteOneAsync(fun x -> x.Id = id) |> Async.AwaitTask
                       ()
            |None -> ()
        }