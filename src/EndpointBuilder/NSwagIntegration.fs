namespace EndpointBuilder

open System.Text.RegularExpressions
open System.Reflection
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

            | Header(name, ty) ->
                let parameter = OpenApiParameter()
                parameter.IsRequired <- true
                parameter.Kind <- OpenApiParameterKind.Header
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


    let addOperation generateSchema (pathItem : OpenApiPathItem) httpVerb endpointHandler =
        httpVerb
        |> Option.iter (fun httpVerb ->
            let operation = OpenApiOperation()
            pathItem.Add(string httpVerb, operation)

            addParameters generateSchema operation endpointHandler.InputSources
            addRequestBody generateSchema operation endpointHandler.InputSources
            addResponses generateSchema operation endpointHandler.ResponseType)


    let internal formatPath path =
        let matchEvaluator = MatchEvaluator(fun m ->
            let parts = m.Value.Split(':')
            if parts.Length < 2 then
                m.Value
            else
                parts.[0] + "}")
        pathParameterRegex.Replace(path, matchEvaluator) 


    let addPaths (doc : OpenApiDocument) generateSchema (endpoints : Endpoints list) =
        getEndpointHandlers endpoints
        |> Seq.filter (fun (_, httpVerb, _) -> httpVerb.IsSome)
        |> Seq.groupBy (fun (path, _, _) -> path)
        |> Seq.iter (fun (path, handlerGroup) ->
            let pathItem = OpenApiPathItem()

            doc.Paths.Add(formatPath path, pathItem)
            pathItem.Description <- "Description" // TODO: From handler data

            for _, httpVerb, handler in handlerGroup do
                addOperation generateSchema pathItem httpVerb handler)


    let copyJsonSchemaProperties (source : JsonSchema) (target : JsonSchema) =
        for sourceProp in source.GetType().GetProperties() do
            if sourceProp.CanWrite && not (sourceProp.Name.StartsWith("Parent")) then
                let value = sourceProp.GetValue(source)
                let targetProp = target.GetType().GetProperty(sourceProp.Name)
                targetProp.SetValue(target, value)


    type OptionSchemaProcessor() =
        interface ISchemaProcessor with   
            member _.Process(context) =
                let ty = context.Type
                if ty.GetTypeInfo().IsGenericType && ty.GetGenericTypeDefinition() = typedefof<option<_>> then
                    let innerType = ty.GenericTypeArguments.[0]
                    let innerSchema = context.Generator.Generate(innerType)
                    copyJsonSchemaProperties innerSchema context.Schema


    let generateOpenApiDocument serializerOptions (endpoints : Endpoints list) =
        let settings =
            JsonSchemaGeneratorSettings(
                SerializerOptions=serializerOptions,
                SchemaType=SchemaType.OpenApi3)

        settings.SchemaProcessors.Add(OptionSchemaProcessor())
        let generateSchema ty = JsonSchema.FromType(ty, settings)

        let doc = OpenApiDocument(SchemaType=SchemaType.OpenApi3)
        addPaths doc generateSchema endpoints
        doc


    let generateSwaggerJson serializerOptions endpoints =
        (generateOpenApiDocument serializerOptions endpoints).ToJson()