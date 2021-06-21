namespace EndpointBuilder

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Giraffe

[<AutoOpen>]
module Request =


    type SourceInfo =
        | QueryParameter of string
        | PathParameter of string


    type SourceError = SourceValueMissing of SourceInfo


    type Source<'T> = Source of (HttpContext -> Result<'T, SourceError list>) * SourceInfo list


    type RequestHandler<'T> = (HttpContext -> Task<Result<'T, SourceError list>>) * SourceInfo list


    let queryParameter parameterName =
        let sourceInfo = QueryParameter parameterName

        let getValue (ctx : HttpContext) =
            match ctx.TryGetQueryStringValue(parameterName) with
            | None -> Error [ SourceValueMissing sourceInfo ]
            | Some value -> Ok (string value)

        Source(getValue, [ sourceInfo ])


    let pathParameter<'T> parameterName =
        let sourceInfo = PathParameter parameterName

        let convertValue : obj -> obj =
            if typeof<'T> = typeof<int> then fun v -> System.Int32.Parse(v :?> string) |> box
            elif typeof<'T> = typeof<string> then box
            else failwithf "BOOM"

        let getValue (ctx : HttpContext) =
            match ctx.Request.RouteValues.TryGetValue(parameterName) with
            | false, _ -> Error [ SourceValueMissing sourceInfo ]
            | true, value -> Ok (convertValue value :?> 'T)

        Source(getValue, [ sourceInfo ])


    type RequestHandlerBuilder() =

        member _.MergeSources(aSource : Source<_>, bSource : Source<_>) =
            let (Source(getAValue, aInfo)) = aSource
            let (Source(getBValue, bInfo)) = bSource

            let getValues ctx =
                match getAValue ctx, getBValue ctx with
                | Ok aValue, Ok bValue -> Ok (aValue, bValue)
                | Error aErrors, Error bErrors -> Error (aErrors @ bErrors)
                | Error errors, _ | _, Error errors -> Error (errors)

            Source(getValues, aInfo @ bInfo)

        member _.BindReturn(source: Source<_>, mapping: 'T -> Task<_>) : RequestHandler<_> =
            let (Source(getValue, sourceInfo)) = source

            let requestHandler ctx =
                match getValue ctx with
                | Ok value ->
                    task {
                        let! output = mapping value
                        return Ok output
                    }
                | Error errors ->
                    Task.FromResult(Error errors)

            (requestHandler, sourceInfo)


    let handler = RequestHandlerBuilder()
