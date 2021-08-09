namespace EndpointBuilder

open NJsonSchema
open NJsonSchema.Generation
open NSwag

module NSwagIntegration =

    let addParameters generateSchema (operation : OpenApiOperation) handlerInputSources =
        for inputSource in handlerInputSources do

            match inputSource with
            | QueryParameter(name, ty) ->
                let parameter = OpenApiParameter()
                parameter.IsRequired <- true
                parameter.Kind <- OpenApiParameterKind.Query
                parameter.Schema <- generateSchema ty
                parameter.Name <- name
                operation.Parameters.Add(parameter)
            
            | PathParameter(name, ty) ->
                let parameter = OpenApiParameter()
                parameter.IsRequired <- true
                parameter.Kind <- OpenApiParameterKind.Path
                parameter.Schema <- generateSchema ty
                parameter.Name <- name
                operation.Parameters.Add(parameter)
                
            | JsonBody _ -> ()


    let addRequestBody generateSchema (operation : OpenApiOperation) handlerInputSources =
        handlerInputSources
        |> List.tryPick (function | JsonBody ty -> Some ty | _ -> None)
        |> Option.iter (fun ty ->
            let requestBody = OpenApiRequestBody()
            operation.RequestBody <- requestBody
            requestBody.IsRequired <- true

            let openApiMediaType = OpenApiMediaType()
            openApiMediaType.Schema <- generateSchema ty
            requestBody.Content.Add("application/json", openApiMediaType))


    let addResponses generateSchema (operation : OpenApiOperation) responseType =
        let response = OpenApiResponse()
        operation.Responses.Add("200", response)

        match responseType with
        | ResponseType.Text ->
            let openApiMediaType = OpenApiMediaType()
            response.Content.Add("text/plain", openApiMediaType)

        | ResponseType.Json ty ->
            let openApiMediaType = OpenApiMediaType()
            openApiMediaType.Schema <- generateSchema ty
            response.Content.Add("application/json", openApiMediaType)


    let addOperation generateSchema (pathItem : OpenApiPathItem) endpointHandler =
        endpointHandler.HttpVerb
        |> Option.iter (fun httpVerb ->
            let operation = OpenApiOperation()
            pathItem.Add(string httpVerb, operation)

            addParameters generateSchema operation endpointHandler.InputSources
            addRequestBody generateSchema operation endpointHandler.InputSources
            addResponses generateSchema operation endpointHandler.ResponseType)


    let addPaths (doc : OpenApiDocument) generateSchema (endpoints : Endpoints list) =
        getEndpointHandlers endpoints
        |> Seq.filter (fun h -> h.HttpVerb.IsSome)
        |> Seq.groupBy (fun h -> h.RoutePattern)
        |> Seq.iter (fun (pattern, handlers) ->
            let pathItem = OpenApiPathItem()
            doc.Paths.Add(pattern, pathItem)
            pathItem.Description <- "Description" // TODO: From handler data

            for endpointHandler in handlers do
                addOperation generateSchema pathItem endpointHandler)


    let generateOpenApiDocument serializerOptions (endpoints : Endpoints list) =
        let settings =
            JsonSchemaGeneratorSettings(
                SerializerOptions=serializerOptions,
                SchemaType=SchemaType.OpenApi3)
        let generateSchema ty = JsonSchema.FromType(ty, settings)
        let doc = OpenApiDocument(SchemaType=SchemaType.OpenApi3)
        addPaths doc generateSchema endpoints
        doc


    let generateSwaggerJson serializerOptions endpoints =
        (generateOpenApiDocument serializerOptions endpoints).ToJson()