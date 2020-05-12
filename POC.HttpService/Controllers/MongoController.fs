namespace POC.HttpService.Controllers

open Microsoft.AspNetCore.Mvc
open POC.Common.MongoRepository
open POC.Common
open OpenTelemetry.Trace.Configuration
open OpenTelemetry.Trace

[<ApiController>]
[<Route("[controller]")>]
type TestController (repo: MongoRepository,
                     tracerFactory: TracerFactory) =
    inherit ControllerBase()

    let tracer = tracerFactory.GetTracer("custom")

    [<HttpGet("{id}")>]
    member this.Get(id: int) = async {
        tracer.CurrentSpan.SetAttribute("id", id)
        let span = tracer.StartSpan("mongoGet", tracer.CurrentSpan, SpanKind.Internal)
        let! data = repo.get id
        span.End()
        match data with
        |Some d -> return this.Ok(d) :> IActionResult
        |None -> return this.NotFound() :> IActionResult
    }

    [<HttpPut>]
    member this.Update([<FromBody>] data: TestData) = async {
        do! repo.upsert data
        return this.Ok()
    }
