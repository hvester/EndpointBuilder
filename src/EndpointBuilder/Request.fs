namespace EndpointBuilder

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Giraffe

[<AutoOpen>]
module Request =


    type SourceInfo =
        | JsonBody of Type
        | QueryParameter of string
        | PathParameter of string


    type SourceError =
        | JsonDeserializationError of Exception
        | SourceValueMissing of SourceInfo


    type Source<'T> = Source of (HttpContext -> Task<Result<'T, SourceError list>>) * SourceInfo list


    type RequestHandler<'T> = (HttpContext -> Task<Result<'T, SourceError list>>) * SourceInfo list


    let jsonBody<'T> () =
        let getValue (ctx : HttpContext) =
            task {
                try
                    let! value = ctx.BindJsonAsync<'T>()
                    return Ok value
                with ex ->
                    return Error [ JsonDeserializationError ex ]
            }

        Source(getValue, [ JsonBody(typeof<'T>) ])


    let queryParameter parameterName =
        let sourceInfo = QueryParameter parameterName

        let getValue (ctx : HttpContext) =
            task {
                match ctx.TryGetQueryStringValue(parameterName) with
                | None -> return Error [ SourceValueMissing sourceInfo ]
                | Some value -> return Ok (string value)
            }

        Source(getValue, [ sourceInfo ])


    let pathParameter<'T> parameterName =
        let sourceInfo = PathParameter parameterName

        let convertValue : obj -> obj =
            if typeof<'T> = typeof<int> then fun v -> System.Int32.Parse(v :?> string) |> box
            elif typeof<'T> = typeof<string> then box
            else failwith "BOOM"

        let getValue (ctx : HttpContext) =
            task {
                match ctx.Request.RouteValues.TryGetValue(parameterName) with
                | false, _ -> return Error [ SourceValueMissing sourceInfo ]
                | true, value -> return Ok (convertValue value :?> 'T)
            }

        Source(getValue, [ sourceInfo ])


    type RequestHandlerBuilder() =

        member _.MergeSources(aSource : Source<_>, bSource : Source<_>) =
            let (Source(getAValue, aInfo)) = aSource
            let (Source(getBValue, bInfo)) = bSource

            let getValues ctx =
                task {
                    let! aResult = getAValue ctx
                    let! bResult = getBValue ctx
                    match aResult, bResult with
                    | Ok aValue, Ok bValue -> return Ok (aValue, bValue)
                    | Error aErrors, Error bErrors -> return Error (aErrors @ bErrors)
                    | Error errors, _ | _, Error errors -> return Error (errors)
                }

            Source(getValues, aInfo @ bInfo)

        member _.BindReturn(source: Source<_>, mapping: 'T -> Task<_>) : RequestHandler<_> =
            let (Source(getValue, sourceInfo)) = source

            let requestHandler ctx =
                task {
                    match! getValue ctx with
                    | Ok value ->
                        let! output = mapping value
                        return Ok output
                    | Error errors ->
                        return Error errors
                }

            (requestHandler, sourceInfo)


    let handler = RequestHandlerBuilder()
