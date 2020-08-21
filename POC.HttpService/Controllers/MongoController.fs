namespace POC.HttpService.Controllers

open Microsoft.AspNetCore.Mvc
open POC.Common.MongoRepository
open POC.Common
open Elastic.Apm
open System
open System.Threading.Tasks

[<ApiController>]
[<Route("[controller]")>]
type TestController (repo: MongoRepository) =
    inherit ControllerBase()

    [<HttpGet("{id}")>]
    member this.Get(id: int) = async {
        let! data = repo.get id
        match data with
        |Some d -> return this.Ok(d) :> IActionResult
        |None -> return this.NotFound() :> IActionResult
    }

    [<HttpPut>]
    member this.Update([<FromBody>] data: TestData) = async {
        do! repo.upsert data
        return this.Ok()
    }
