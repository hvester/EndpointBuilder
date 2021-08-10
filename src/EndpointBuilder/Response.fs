namespace EndpointBuilder

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Giraffe

[<AutoOpen>]
module Response =


    [<RequireQualifiedAccess>]
    type ResponseType =
        | Text
        | Json of Type


    type EndpointHandler =
        {
            InputSources : HandlerInputSource list
            ResponseType : ResponseType
            RequestDelegate : RequestDelegate
        }
        

    let json (requestHandler : RequestHandler<'T>) =
        let requestHandlerFunc, inputSources = requestHandler
        let f (ctx : HttpContext) =
            task {
                match! requestHandlerFunc ctx with
                | Ok value ->
                    let! _ = ctx.WriteJsonAsync(value)
                    return ()
                | Error errors ->
                    ctx.SetStatusCode(400)
                    let! _ = ctx.WriteStringAsync(sprintf "%A" errors) // TODO: ProblemsDetails maybe?
                    return ()
            }
            :> Task
        {
            InputSources = inputSources
            ResponseType = ResponseType.Json typeof<'T>
            RequestDelegate = new RequestDelegate(f)
        }


    let text (requestHandler : RequestHandler<string>) =
        let requestHandlerFunc, inputSources = requestHandler
        let f (ctx : HttpContext) =
            task {
                match! requestHandlerFunc ctx with
                | Ok responseString ->
                    let! _ = ctx.WriteTextAsync(responseString)
                    return ()
                | Error errors ->
                    ctx.SetStatusCode(400)
                    let! _ = ctx.WriteStringAsync(sprintf "%A" errors) // TODO: ProblemsDetails maybe?
                    return ()
            }
            :> Task
        {
            InputSources = inputSources
            ResponseType = ResponseType.Text
            RequestDelegate = new RequestDelegate(f)
        }