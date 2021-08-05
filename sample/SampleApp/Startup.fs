namespace SampleApp

open System
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open FSharp.Data
open EndpointBuilder

type Startup() =

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member _.ConfigureServices(services: IServiceCollection) =
        services.AddGiraffe() |> ignore
        services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(Json.DefaultOptions)) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        app
            .UseRouting()
            .UseSwaggerUI(fun c -> c.SwaggerEndpoint("/swagger.json", "My API V1"))
            .UseEndpointBuilder(App.endpoints) |> ignore
