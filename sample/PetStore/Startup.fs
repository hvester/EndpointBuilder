namespace PetStore

open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open EndpointBuilder

type Startup() =

    let options = JsonSerializerOptions(PropertyNamingPolicy=JsonNamingPolicy.CamelCase)
    do
        options.Converters.Add(JsonStringEnumConverter())
        options.Converters.Add(
            JsonFSharpConverter(
                JsonUnionEncoding.ExternalTag
                ||| JsonUnionEncoding.NamedFields
                ||| JsonUnionEncoding.UnwrapFieldlessTags
                ||| JsonUnionEncoding.UnwrapOption))

    member _.ConfigureServices(services: IServiceCollection) =
        services.AddGiraffe() |> ignore
        services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(options)) |> ignore
        services.AddSingleton<PetRepo>() |> ignore

    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        app
            .UseRouting()
            .UseSwaggerUI(fun c -> c.SwaggerEndpoint("/swagger.json", "My API V1"))
            .UseEndpointBuilder(options, App.endpoints) |> ignore
