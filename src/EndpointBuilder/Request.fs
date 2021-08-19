namespace EndpointBuilder

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Giraffe
open MimeTypes
open StatusCodeTypes

[<AutoOpen>]
module Request =


    type HandlerInputSource =
        | JsonBody of Type
        | QueryParameter of string * Type
        | PathParameter of string * Type
        | Header of string * Type


    type HandlerInputError =
        | InputValueMissing of HandlerInputSource
        | ValueConversionError of HandlerInputSource * string
        | JsonDeserializationError of Exception


    type HandlerInput<'T> =
        { GetInputValue : HttpContext -> Task<Result<'T, HandlerInputError list>>
          InputSources : HandlerInputSource list }


    type EndpointHandler =
        {
            InputSources : HandlerInputSource list
            StatusCode : int
            MimeType : string
            ResponseType : Type
            RequestDelegate : RequestDelegate
        }


    let private createConverterResult inputSource str (parseResult : (bool * 'T)) =
        let converterResult =
            match parseResult with
            | false, _ ->
                let errorMessage = $"""Cannot convert "{str}" to {typeof<'T>}"""
                Error [ ValueConversionError(inputSource, errorMessage) ]

            | true, value ->
                Ok value

        box converterResult


    let private getValueConverter (ty : Type) inputSource : string -> obj =
        if ty = typeof<string> then
            fun str -> createConverterResult inputSource str (true, str)

        elif ty = typeof<int> then
            fun str -> Int32.TryParse(str) |> (createConverterResult inputSource str)
            
        elif ty = typeof<float> then
            fun str -> Double.TryParse(str) |> (createConverterResult inputSource str)
             
        elif ty = typeof<Guid> then
            fun str -> Guid.TryParse(str) |> (createConverterResult inputSource str)

        else
            failwithf "%A has unsupported type: %O" inputSource ty 


    let fromPath<'T> parameterName =
        let ty = typeof<'T>
        let inputSource = PathParameter(parameterName, ty)
        let convertValue = getValueConverter ty inputSource
        {
            GetInputValue = fun ctx ->
                task {
                    match ctx.Request.RouteValues.TryGetValue(parameterName) with
                    | false, _ -> return Error [ InputValueMissing inputSource ]
                    | true, o ->
                        let str = o :?> string
                        return convertValue str :?> Result<'T, HandlerInputError list>
                }
            InputSources = [ inputSource ]
        }


    let fromQuery<'T> parameterName =
        let ty = typeof<'T>
        let inputSource = QueryParameter(parameterName, ty)
        let convertValue = getValueConverter ty inputSource
        {
            GetInputValue = fun ctx ->
                task {
                    match ctx.TryGetQueryStringValue(parameterName) with
                    | None -> return Error [ InputValueMissing inputSource ]
                    | Some str -> return convertValue str :?> Result<'T, HandlerInputError list>
                }
            InputSources = [ inputSource ]
        }


    let fromHeader<'T> headerName =
        let ty = typeof<'T>
        let inputSource = Header(headerName, ty)
        let convertValue = getValueConverter ty inputSource
        {
            GetInputValue = fun ctx ->
                task {
                    match ctx.Request.Headers.TryGetValue(headerName) with
                    | false, _ -> return Error [ InputValueMissing inputSource ]
                    | true, value -> return convertValue (string value) :?> Result<'T, HandlerInputError list>
                }
            InputSources = [ inputSource ]
        }


    let fromJsonBody<'T> =
        {
            GetInputValue = fun ctx ->
                task {
                    try
                        let! value = ctx.BindJsonAsync<'T>()
                        return Ok value
                    with ex ->
                        return Error [ JsonDeserializationError ex ]
                }
            InputSources = [ JsonBody(typeof<'T>) ]
        }


    let getHttpContext : HandlerInput<HttpContext> =
        {
            GetInputValue = fun ctx -> Task.FromResult(Ok ctx)
            InputSources = []
        }


    let fromServices<'T> : HandlerInput<'T> =
        {
            GetInputValue = fun ctx ->
                let service = ctx.GetService<'T>()
                Task.FromResult(Ok service)
            InputSources = []
        }

      
    let private getStatusCode (ty : Type) =
        let attribute = ty.GetCustomAttributes(typeof<StatusCodeAttribute>, true).[0]
        (attribute :?> StatusCodeAttribute).StatusCode


    let private getMimeType (ty : Type) =
        let attribute = ty.GetCustomAttributes(typeof<MimeTypeAttribute>, true).[0]
        (attribute :?> MimeTypeAttribute).MimeType


    type RequestHandlerBuilder() =

        member _.MergeSources(input1 : HandlerInput<_>, input2 : HandlerInput<_>) =
            {
                GetInputValue = fun ctx ->
                    task {
                        let! result1 = input1.GetInputValue ctx
                        let! result2 = input2.GetInputValue ctx
                        match result1, result2 with
                        | Ok value1, Ok value2 -> return Ok (value1, value2)
                        | Error errors1, Error errors2 -> return Error (errors1 @ errors2)
                        | Error errors, _ | _, Error errors -> return Error (errors)
                    }
                InputSources = input1.InputSources @ input2.InputSources
            }

        member _.BindReturn
            (
                input: HandlerInput<'Input>,
                mapping : 'Input -> Task<ResponseHandler<'Response, 'StatusCode, 'MimeType>>
            )
            : EndpointHandler =

            let handler ctx =
                unitTask {
                    match! input.GetInputValue ctx with
                    | Ok inputValue ->
                        let! ResponseHandler responseHandler = mapping inputValue
                        do! responseHandler ctx

                    | Error errors ->
                        ctx.SetStatusCode(400)
                        let! _ = ctx.WriteStringAsync(sprintf "%A" errors) // TODO: ProblemsDetails maybe?
                        return ()
                }
            {
                InputSources = input.InputSources
                StatusCode = getStatusCode typeof<'StatusCode>
                MimeType = getMimeType typeof<'MimeType>
                ResponseType = typeof<'Response>
                RequestDelegate = new RequestDelegate(handler)
            }


    let handler = RequestHandlerBuilder()
