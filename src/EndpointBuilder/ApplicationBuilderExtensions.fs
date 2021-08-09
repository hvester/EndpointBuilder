namespace EndpointBuilder

open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Routing
open FSharp.Control.Tasks
open Giraffe


[<Extension>]
type EndpointRouteBuilderExtensions() =

    [<Extension>]
    static member MapEndpointBuilderEndpoints(builder : IEndpointRouteBuilder, endpoints : Endpoints list) =

        endpoints
        |> List.iter (function
            | Endpoint h ->
                match h.HttpVerb with
                | Some httpVerb ->
                    builder.MapMethods(h.RoutePattern, [ httpVerb.ToString() ], h.RequestDelegate)
                    |> ignore
                
                | None ->
                    builder.Map(h.RoutePattern, h.RequestDelegate)
                    |> ignore              

            | EndpointList endpointsList ->
                builder.MapEndpointBuilderEndpoints(endpointsList))

    [<Extension>]
    static member MapSwaggerEndpoint(builder : IEndpointRouteBuilder, serializerOptions, endpoints) =
        (*
        let responseBytes = SwashbuckleIntegration.generateSwaggerJsonBytes serializerOptions endpoints

        let getSwaggerJson (ctx : HttpContext) =
            unitTask {
                ctx.SetContentType "application/json"
                do! ctx.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length)
            }
        *)

        let swaggerJson = NSwagIntegration.generateSwaggerJson serializerOptions endpoints

        let getSwaggerJson (ctx : HttpContext) =
            unitTask {
                ctx.SetContentType "application/json"
                let! _ = ctx.WriteStringAsync(swaggerJson)
                return ()
            }

        builder.MapGet("swagger.json", new RequestDelegate(getSwaggerJson)) |> ignore


[<Extension>]
type ApplicationBuilderExtensions() =

    [<Extension>]
    static member UseEndpointBuilder(builder : IApplicationBuilder, serializerOptions, endpoints) =
        builder.UseEndpoints(fun e ->
            e.MapEndpointBuilderEndpoints(endpoints)
            e.MapSwaggerEndpoint(serializerOptions, endpoints))
