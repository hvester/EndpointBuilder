namespace EndpointBuilder

open System.Text.RegularExpressions
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
        let matchEvaluator = MatchEvaluator(fun m ->
            m.Value
                .Replace(":%s", "")
                .Replace(":%i", ":int"))

        let toRequestDelegate (handler : HttpHandler) =
            RequestDelegate(fun (ctx : HttpContext) ->
                unitTask {
                    let! _ = handler earlyReturn ctx
                    return ()
                })

        for path, httpVerbOpt, handler in getEndpointHandlers endpoints do
            let pattern = pathParameterRegex.Replace(path, matchEvaluator) 
            match httpVerbOpt with
            | Some httpVerb ->
                builder.MapMethods(pattern, [ httpVerb.ToString() ], toRequestDelegate handler.HttpHandler)
                |> ignore
            
            | None ->
                builder.Map(pattern, toRequestDelegate handler.HttpHandler)
                |> ignore


    [<Extension>]
    static member MapSwaggerEndpoint(builder : IEndpointRouteBuilder, serializerOptions, endpoints) =
        let responseBytes = SwashbuckleIntegration.generateSwaggerJsonBytes serializerOptions endpoints

        let getSwaggerJson (ctx : HttpContext) =
            unitTask {
                ctx.SetContentType "application/json"
                do! ctx.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length)
            }

        builder.MapGet("swagger.json", new RequestDelegate(getSwaggerJson)) |> ignore


[<Extension>]
type ApplicationBuilderExtensions() =

    [<Extension>]
    static member UseEndpointBuilder(builder : IApplicationBuilder, serializerOptions, endpoints) =
        builder.UseEndpoints(fun e ->
            e.MapEndpointBuilderEndpoints(endpoints)
            e.MapSwaggerEndpoint(serializerOptions, endpoints))
