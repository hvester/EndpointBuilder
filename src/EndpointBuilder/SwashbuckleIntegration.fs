namespace EndpointBuilder

open System
open System.IO
open Microsoft.OpenApi.Models
open Microsoft.OpenApi.Writers
open Swashbuckle.AspNetCore.SwaggerGen


module SwashbuckleIntegration =

    let generateParameters generateSchema inputSources =
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

            | Header(name, ty) ->
                OpenApiParameter(
                    Required = true,
                    In = Nullable(ParameterLocation.Header),
                    Schema = generateSchema ty,
                    Name = name)
                |> Some
                
            | JsonBody _ ->
                None)


    let generateRequestBody generateSchema inputSources =
        inputSources
        |> List.tryPick (function | JsonBody ty -> Some ty | _ -> None)
        |> Option.map (fun ty ->
            generateSchema ty |> ignore
            OpenApiRequestBody(
                Content = dict [
                    "application/json", OpenApiMediaType(Schema = generateSchema ty)
                ],
                Required = true))    


    let httpVerbToOperationType httpVerb =
        match httpVerb with
        | HttpVerb.GET -> OperationType.Get
        | HttpVerb.POST -> OperationType.Post
        | HttpVerb.PUT -> OperationType.Put
        | HttpVerb.PATCH -> OperationType.Patch
        | HttpVerb.DELETE -> OperationType.Delete
        | HttpVerb.HEAD -> OperationType.Head
        | HttpVerb.OPTIONS -> OperationType.Options
        | HttpVerb.TRACE -> OperationType.Trace


    let responseTypeToOpenApiMediaType generateSchema responseType =
        match responseType with
        | ResponseType.Text ->
            "text/plain", OpenApiMediaType()

        | ResponseType.Json responseType ->
            "application/json", OpenApiMediaType(Schema = generateSchema responseType)      


    let generateResponses generateSchema responseType =
        let responses = OpenApiResponses()
        let response =
            OpenApiResponse(
                Description = "OK", // TODO: Get from somewhere
                Content = dict [ responseTypeToOpenApiMediaType generateSchema responseType ])

        responses.Add("200", response)
        responses


    let generateOperation generateSchema handler =
        let parameters = generateParameters generateSchema handler.InputSources
        let requestBody = generateRequestBody generateSchema handler.InputSources
        let responses = generateResponses generateSchema handler.ResponseType 
        OpenApiOperation(
            Description = "Description test",
            Parameters = ResizeArray(parameters),
            RequestBody = Option.toObj requestBody,
            Responses = responses)


    let generateOpenApiPathItem generateSchema handlersWithHttpVerbs =
        let operations = dict [
            for httpVerb, handler in handlersWithHttpVerbs do
                let operationType = httpVerbToOperationType httpVerb
                let operation = generateOperation generateSchema handler
                operationType, operation
        ]
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
        |> Seq.choose (fun (path, httpVerbOpt, handler) ->
            httpVerbOpt |> Option.map (fun httpVerb -> (path, httpVerb, handler)))
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