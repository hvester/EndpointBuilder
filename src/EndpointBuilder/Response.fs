namespace EndpointBuilder

open System
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
            unitTask {
                match! requestHandlerFunc ctx with
                | Some response ->
                    let! _ = ctx.WriteJsonAsync(response)
                    return ()
                | None ->
                    return ()
            }
        {
            InputSources = inputSources
            ResponseType = ResponseType.Json typeof<'T>
            RequestDelegate = new RequestDelegate(f)
        }


    let text (requestHandler : RequestHandler<string>) =
        let requestHandlerFunc, inputSources = requestHandler
        let f (ctx : HttpContext) =
            unitTask {
                match! requestHandlerFunc ctx with
                | Some responseString ->
                    let! _ = ctx.WriteTextAsync(responseString)
                    return ()
                | None ->
                    return ()
            }
        {
            InputSources = inputSources
            ResponseType = ResponseType.Text
            RequestDelegate = new RequestDelegate(f)
        }
