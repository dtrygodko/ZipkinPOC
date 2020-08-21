namespace ZipkinPOC

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Newtonsoft.Json.Serialization
open Microsoft.OpenApi.Models
open POC.Common
open Elastic.Apm.AspNetCore
open Elastic.Apm.DiagnosticSource

type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =

        services.Configure<KafkaOptions>(this.Configuration.GetSection("KafkaOptions")) |> ignore
        
        services.AddSwaggerGen(fun c ->
            let info = OpenApiInfo()
            info.Title <- "TestMainService"
            info.Version <- "v1"
            c.SwaggerDoc("v1", info)) |> ignore

        // Add framework services.
        services.AddControllers().AddNewtonsoftJson(fun options -> options.SerializerSettings.ContractResolver <- new DefaultContractResolver()) |> ignore
                
        services.AddHttpClient() |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseElasticApm(this.Configuration, new HttpDiagnosticsSubscriber()) |> ignore

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
