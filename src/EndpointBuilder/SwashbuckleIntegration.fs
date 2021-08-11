namespace EndpointBuilder

open System
open System.IO
open Microsoft.OpenApi.Models
open Microsoft.OpenApi.Writers
open Swashbuckle.AspNetCore.SwaggerGen


module SwashbuckleIntegration =

    let generateParameters generateSchema (inputSources : HandlerInputSource list) =
        inputSources
        |> List.choose (fun inputSource ->
            match inputSource with
            | QueryParameter(name, ty) ->
                OpenApiParameter(
                    Required = true,
                    In = Nullable(ParameterLocation.Query),
                    Schema = generateSchema ty,
                    Name = name)
                |> Some
            
            | PathParameter(name, ty) ->
                OpenApiParameter(
                    Required = true,
                    In = Nullable(ParameterLocation.Path),
                    Schema = generateSchema ty,
                    Name = name)
                |> Some

            | Header(header, ty) ->
                OpenApiParameter(
                    Required = true,
                    In = Nullable(ParameterLocation.Header),
                    Schema = generateSchema ty,
                    Name = header)
                |> Some
                
            | JsonBody _ ->
                None)


    let generateOpenApiPathItem generateSchema handlersWithHttpVerbs =
        let operations =
            handlersWithHttpVerbs
            |> Seq.map (fun (httpVerb, handler) ->
                let operationType =
                    match httpVerb with
                    | Some HttpVerb.GET -> OperationType.Get
                    | Some HttpVerb.POST -> OperationType.Post
                    | Some HttpVerb.PUT -> OperationType.Put
                    | Some HttpVerb.PATCH -> OperationType.Patch
                    | Some HttpVerb.DELETE -> OperationType.Delete
                    | Some HttpVerb.OPTIONS -> OperationType.Options
                    | Some HttpVerb.TRACE -> OperationType.Trace
                    | _ -> OperationType.Get // TODO: Add rest and figure out what to do with None

                let requestBody =
                    handler.InputSources
                    |> List.tryPick (function | JsonBody ty -> Some ty | _ -> None)
                    |> Option.map (fun ty ->
                        generateSchema ty |> ignore
                        OpenApiRequestBody(
                            Content = dict [
                                "application/json", OpenApiMediaType(Schema = generateSchema ty)
                            ],
                            Required = true))

                let responses = OpenApiResponses()
                responses.Add(
                    "200",
                    OpenApiResponse(
                        Description = "OK",
                        Content = dict [
                            match handler.ResponseType with
                            | ResponseType.Text ->
                                "text/plain", OpenApiMediaType()
                            | ResponseType.Json responseType ->
                                "application/json", OpenApiMediaType(Schema = generateSchema responseType)
                        ]))

                let operation =
                    OpenApiOperation(
                        Description = "Description test",
                        Parameters = ResizeArray(generateParameters generateSchema handler.InputSources),
                        RequestBody = Option.toObj requestBody,
                        Responses = responses)

                (operationType, operation) )
            |> dict

        OpenApiPathItem(Operations=operations)


    let generateOpenApiModel serializerOptions (endpoints : Endpoints list) =
        let document = OpenApiDocument()
        document.Info <- OpenApiInfo(Version = "1.0.0", Title = "Swagger Petstore (Simple)")
        document.Servers <- ResizeArray([ OpenApiServer(Url = "http://localhost:5000") ])
        document.Paths <- OpenApiPaths()

        let schemaGeneratorOptions = SchemaGeneratorOptions()
        let dataContractResolver = JsonSerializerDataContractResolver(serializerOptions)
        let schemaRepo = SchemaRepository()
        let schemaGenerator = SchemaGenerator(schemaGeneratorOptions, dataContractResolver)
        let generateSchema (ty : Type) = schemaGenerator.GenerateSchema(ty, schemaRepo)

        getEndpointHandlers endpoints
        |> Seq.filter (fun (_, httpVerb, _) -> httpVerb.IsSome)
        |> Seq.groupBy (fun (path, _, _) -> path)
        |> Seq.iter (fun (path, handlerGroup) ->
            let formattedPath = NSwagIntegration.formatPath path
            let pathItem =
                handlerGroup
                |> Seq.map (fun (_, httpVerb, handler) -> (httpVerb, handler))
                |> generateOpenApiPathItem generateSchema
            document.Paths.[formattedPath] <- pathItem)

        document.Components <- OpenApiComponents()
        document.Components.Schemas <- schemaRepo.Schemas

        // Swashbuckle generates all properties of F# records as "readonly", i.e. not present in
        // request bodies. This is a workaround to reset them to be not "readonly".
        for schema in document.Components.Schemas.Values do
            for prop in schema.Properties do
                prop.Value.ReadOnly <- false

        document


    let generateSwaggerJsonBytes serializerOptions (endpoints : Endpoints list) =
        let document = generateOpenApiModel serializerOptions endpoints
        use ms = new MemoryStream()
        use sw = new StreamWriter(ms)
        let writer = OpenApiJsonWriter(sw)
        document.SerializeAsV3(writer)
        sw.Flush()
        ms.ToArray()