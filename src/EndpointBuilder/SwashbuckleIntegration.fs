namespace EndpointBuilder

open System
open System.IO
open System.Text.RegularExpressions
open System.Reflection
open FSharp.Reflection
open Microsoft.OpenApi.Models
open Microsoft.OpenApi.Any
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


    type NoReadOnlyPropertiesFilter() =
        interface ISchemaFilter with
            member _.Apply(schema, _) =
                for prop in schema.Properties do
                    prop.Value.ReadOnly <- false


    let isOptionType (ty : Type) =
        ty.GetTypeInfo().IsGenericType && ty.GetGenericTypeDefinition() = typedefof<option<_>>


    // TODO: Copy all properties manually without reflection
    let copyJsonSchemaProperties (source : OpenApiSchema) (target : OpenApiSchema) =
        for sourceProp in source.GetType().GetProperties() do
            if sourceProp.CanWrite then
                let value = sourceProp.GetValue(source)
                let targetProp = target.GetType().GetProperty(sourceProp.Name)
                targetProp.SetValue(target, value)


    let getActualSchema (schemaRepository : SchemaRepository) (schema : OpenApiSchema) =
        if isNull schema.Reference then
            schema
        else
            schemaRepository.Schemas.[schema.Reference.Id]


    type OptionsAsNullableValuesFilter() =
        interface ISchemaFilter with
            member _.Apply(schema, context) =
                if isOptionType context.Type then
                    let valueSchema = Seq.head schema.Properties.Values
                    copyJsonSchemaProperties valueSchema schema
                    schema.Nullable <- true


    type RequiredIfNotNullableFilter() =
        interface ISchemaFilter with
            member _.Apply(schema, context) =
                for KeyValue(propName, propSchema) in schema.Properties do
                    let actualPropSchema = getActualSchema context.SchemaRepository propSchema
                    if not actualPropSchema.Nullable then
                        schema.Required.Add(propName) |> ignore


    type FieldlessUnionsToEnumsFilter() =

        let createEnumCase caseName =
            { 
                new IOpenApiPrimitive with
                    member _.AnyType = AnyType.Primitive
                    member _.PrimitiveType = PrimitiveType.String
                    member _.Write(writer, _) =
                        writer.WriteRaw($"\"%s{caseName}\"")
            }


        interface ISchemaFilter with
            member _.Apply(schema, context) =
                // TODO: Check that all cases are fieldless.
                if FSharpType.IsUnion context.Type && not (isOptionType context.Type) then
                    copyJsonSchemaProperties (OpenApiSchema()) schema
                    schema.Type <- "string"
                    for case in FSharpType.GetUnionCases context.Type do
                        schema.Enum.Add(createEnumCase case.Name)


    let private formatPath path =
        let matchEvaluator = MatchEvaluator(fun m ->
            let parts = m.Value.Split(':')
            if parts.Length < 2 then
                m.Value
            else
                parts.[0] + "}")
        pathParameterRegex.Replace(path, matchEvaluator) 


    let generateOpenApiModel serializerOptions (endpoints : Endpoints list) =
        let document = OpenApiDocument()
        document.Info <- OpenApiInfo(Version = "1.0.0", Title = "Swagger Petstore (Simple)")
        document.Servers <- ResizeArray([ OpenApiServer(Url = "https://localhost:5001") ])
        document.Paths <- OpenApiPaths()

        let schemaGeneratorOptions = SchemaGeneratorOptions()
        schemaGeneratorOptions.SchemaFilters.Add(NoReadOnlyPropertiesFilter())
        schemaGeneratorOptions.SchemaFilters.Add(OptionsAsNullableValuesFilter())
        schemaGeneratorOptions.SchemaFilters.Add(RequiredIfNotNullableFilter())
        schemaGeneratorOptions.SchemaFilters.Add(FieldlessUnionsToEnumsFilter())
        let dataContractResolver = JsonSerializerDataContractResolver(serializerOptions)
        let schemaRepo = SchemaRepository()
        let schemaGenerator = SchemaGenerator(schemaGeneratorOptions, dataContractResolver)
        let generateSchema (ty : Type) = schemaGenerator.GenerateSchema(ty, schemaRepo)

        getEndpointHandlers endpoints
        |> Seq.choose (fun (path, httpVerbOpt, handler) ->
            httpVerbOpt |> Option.map (fun httpVerb -> (path, httpVerb, handler)))
        |> Seq.groupBy (fun (path, _, _) -> path)
        |> Seq.iter (fun (path, handlerGroup) ->
            let formattedPath = formatPath path
            let pathItem =
                handlerGroup
                |> Seq.map (fun (_, httpVerb, handler) -> (httpVerb, handler))
                |> generateOpenApiPathItem generateSchema
            document.Paths.[formattedPath] <- pathItem)

        document.Components <- OpenApiComponents(Schemas = schemaRepo.Schemas)
        document


    let generateSwaggerJsonBytes serializerOptions (endpoints : Endpoints list) =
        let document = generateOpenApiModel serializerOptions endpoints
        use ms = new MemoryStream()
        use sw = new StreamWriter(ms)
        let writer = OpenApiJsonWriter(sw)
        document.SerializeAsV3(writer)
        sw.Flush()
        ms.ToArray()