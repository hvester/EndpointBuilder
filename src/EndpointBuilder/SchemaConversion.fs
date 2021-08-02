namespace EndpointBuilder

open System
open Microsoft.OpenApi.Models
open NJsonSchema
open FSharp.Data.JsonSchema

module SchemaConversion =

    let filterNullObjects (jsonSchemas : JsonSchema seq) =
        jsonSchemas
        |> Seq.filter (fun schema -> schema.Type <> JsonObjectType.Null)
        |> Seq.toList
        

    let getOpenApiSchemaType (jsonSchemaType : JsonObjectType) =
        if int jsonSchemaType &&& int JsonObjectType.Array > 0 then
            "array"
        elif int jsonSchemaType &&& int JsonObjectType.Boolean > 0 then
            "boolean"
        elif int jsonSchemaType &&& int JsonObjectType.Integer > 0 then
            "integer"
        elif int jsonSchemaType &&& int JsonObjectType.Number > 0 then
            "number"
        elif int jsonSchemaType &&& int JsonObjectType.Object > 0 then
            "object"
        elif int jsonSchemaType &&& int JsonObjectType.String > 0 then
            "string"
        elif int jsonSchemaType &&& int JsonObjectType.File > 0 then
            // According to Swagger docs "file" should be mapped to "string"
            "string"
        else
            null


    let rec convertToOpenApiSchema (jsonSchema : JsonSchema) =

        match jsonSchema.Type with
        | JsonObjectType.None when jsonSchema.OneOf.Count > 0 ->
            match filterNullObjects jsonSchema.OneOf with
            | [ singleSchema ] -> convertToOpenApiSchema singleSchema
            | jsonSchemas ->
                let openApiSchema = OpenApiSchema()
                for schema in jsonSchemas do
                    openApiSchema.OneOf.Add(convertToOpenApiSchema schema)
                openApiSchema

        | JsonObjectType.None when jsonSchema.AnyOf.Count > 0 ->
            match filterNullObjects jsonSchema.AnyOf with
            | [ singleSchema ] -> convertToOpenApiSchema singleSchema
            | jsonSchemas ->
                let openApiSchema = OpenApiSchema()
                for schema in jsonSchemas do
                    openApiSchema.AnyOf.Add(convertToOpenApiSchema schema)
                openApiSchema

        | JsonObjectType.None when jsonSchema.AllOf.Count > 0 ->
            let openApiSchema = OpenApiSchema()
            // Assume that AllOf does not contain Null objects
            for schema in jsonSchema.AllOf do
                openApiSchema.AllOf.Add(convertToOpenApiSchema schema)
            openApiSchema

        | JsonObjectType.None when jsonSchema.HasReference ->
            convertToOpenApiSchema jsonSchema.Reference

        | jsonObjectType ->
            let openApiSchema = OpenApiSchema(Type=getOpenApiSchemaType jsonObjectType)
            openApiSchema.MinLength <- jsonSchema.MinLength
            openApiSchema.MaxLength <- jsonSchema.MaxLength
            openApiSchema.Pattern <- jsonSchema.Pattern
            openApiSchema.Format <- jsonSchema.Format
            (*
            openApiSchema.MinItems <- jsonSchema.MinItems
            openApiSchema.MaxItems <- jsonSchema.MaxItems
            openApiSchema.MinProperties <- jsonSchema.MinProperties
            openApiSchema.MaxProperties <- jsonSchema.MaxProperties
            *)
            openApiSchema.MultipleOf <- jsonSchema.MultipleOf

            openApiSchema.Title <- jsonSchema.Title
            if not (isNull jsonSchema.AdditionalPropertiesSchema) then
                openApiSchema.AdditionalProperties <- convertToOpenApiSchema jsonSchema.AdditionalPropertiesSchema
            openApiSchema.AdditionalPropertiesAllowed <- jsonSchema.AllowAdditionalProperties

            if jsonSchema.ExclusiveMinimum.HasValue then
                openApiSchema.ExclusiveMinimum <- Nullable(true)
                openApiSchema.Minimum <- jsonSchema.ExclusiveMinimum.Value
            else
                openApiSchema.Minimum <- jsonSchema.Minimum

            if jsonSchema.ExclusiveMaximum.HasValue then
                openApiSchema.ExclusiveMaximum <- Nullable(true)
                openApiSchema.Maximum <- jsonSchema.ExclusiveMaximum.Value
            else
                openApiSchema.Maximum <- jsonSchema.Maximum

            for KeyValue(propertyName, property) in jsonSchema.Properties do
                if property.IsRequired then 
                    openApiSchema.Required.Add(propertyName) |> ignore
                openApiSchema.Properties.Add(propertyName, convertToOpenApiSchema property)

            openApiSchema.Nullable <- jsonSchema.IsNullable(SchemaType.OpenApi3)

            openApiSchema


    let private njsonSchemaGenerator = Generator.Create()


    let generateSchema (ty : Type) = njsonSchemaGenerator ty |> convertToOpenApiSchema
