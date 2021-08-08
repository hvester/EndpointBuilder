namespace SampleApp

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
                JsonUnionEncoding.InternalTag
                ||| JsonUnionEncoding.NamedFields
                ||| JsonUnionEncoding.UnwrapFieldlessTags
                ||| JsonUnionEncoding.UnwrapOption,
                unionTagName="kind"))

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member _.ConfigureServices(services: IServiceCollection) =
        services.AddGiraffe() |> ignore
        services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(options)) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        app
            .UseRouting()
            .UseSwaggerUI(fun c ->
                c.SwaggerEndpoint("/swagger.json", "My API V1"))
            .UseEndpointBuilder(options, App.endpoints) |> ignore
