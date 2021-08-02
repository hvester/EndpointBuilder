namespace EndpointBuilder

open System
open System.IO
open System.Threading.Tasks
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Routing
open Microsoft.OpenApi
open Microsoft.OpenApi.Writers
open FSharp.Control.Tasks
open Giraffe
open Microsoft.OpenApi.Models
open SchemaConversion

module OpenApiGeneration =

    let rec getEndpointHandlers (endpoints : Endpoints list) =
        seq {
            for e in endpoints do
                match e with
                | Endpoint h -> yield h
                | EndpointList es -> yield! getEndpointHandlers es
        }

    let generateParameters (inputSources : HandlerInputSource list) =
        inputSources
        |> List.choose (fun inputSource ->
            match inputSource with
            | QueryParameter name ->
                OpenApiParameter(
                    Required = true,
                    In = Nullable(ParameterLocation.Query),
                    Name = name)
                |> Some
            
            | PathParameter name ->
                OpenApiParameter(
                    Required = true,
                    In = Nullable(ParameterLocation.Path),
                    Name = name)
                |> Some
                
            | JsonBody _ ->
                None)


    let generateOpenApiPathItem (handlers : EndpointHandler seq) =
        let operations =
            handlers
            |> Seq.map (fun h ->
                let operationType =
                    match h.HttpVerb with
                    | Some HttpVerb.GET -> OperationType.Get
                    | Some HttpVerb.POST -> OperationType.Post
                    | _ -> OperationType.Get // TODO: Add rest and figure out what to do with None

                let responses = OpenApiResponses()
                responses.Add("200", OpenApiResponse(Description = "OK"))

                let requestBody =
                    h.InputSources
                    |> List.tryPick (function | JsonBody ty -> Some ty | _ -> None)
                    |> Option.map (fun ty ->
                        OpenApiRequestBody(
                            Content = dict [
                                "application/json", OpenApiMediaType(Schema = generateSchema ty)
                            ]))

                let operation =
                    OpenApiOperation(
                        Description = "Description test",
                        Parameters = ResizeArray(generateParameters h.InputSources),
                        RequestBody = Option.toObj requestBody,
                        Responses = responses)

                (operationType, operation) )
            |> dict

        OpenApiPathItem(Operations=operations)


    let generateOpenApiModel (endpoints : Endpoints list) =
        let document = OpenApiDocument()
        document.Info <- OpenApiInfo(Version = "1.0.0", Title = "Swagger Petstore (Simple)")
        document.Servers <- ResizeArray([ OpenApiServer(Url = "http://localhost:5000") ])
        document.Paths <- OpenApiPaths()

        getEndpointHandlers endpoints
        |> Seq.groupBy (fun h -> h.RoutePattern)
        |> Seq.iter (fun (pattern, handlers) ->
            document.Paths.[pattern] <- generateOpenApiPathItem handlers)

        document


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
    static member MapSwaggerEndpoint(builder : IEndpointRouteBuilder, endpoints : Endpoints list) =
        let document = OpenApiGeneration.generateOpenApiModel(endpoints)
        use ms = new MemoryStream()
        use sw = new StreamWriter(ms)
        let writer = OpenApiJsonWriter(sw)
        document.SerializeAsV3(writer)
        sw.Flush()
        let responseBytes = ms.ToArray()

        let getSwaggerJson (ctx : HttpContext) =
            unitTask {
                ctx.SetContentType "application/json"
                do! ctx.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length)
            }

        builder.MapGet("swagger.json", new RequestDelegate(getSwaggerJson)) |> ignore


[<Extension>]
type ApplicationBuilderExtensions() =

    [<Extension>]
    static member UseEndpointBuilder(builder : IApplicationBuilder, endpoints : Endpoints list) =
        builder.UseEndpoints(fun e ->
            e.MapEndpointBuilderEndpoints(endpoints)
            e.MapSwaggerEndpoint(endpoints))
