namespace EndpointBuilder

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Giraffe

[<AutoOpen>]
module Response =


    [<RequireQualifiedAccess>]
    type HttpVerb =
        | GET
        | POST
        | PUT
        | PATCH
        | DELETE
        | HEAD
        | OPTIONS
        | TRACE
        | CONNECT


    [<RequireQualifiedAccess>]
    type ResponseType =
        | Text
        | Json of Type


    type EndpointHandler =
        {
            HttpVerb : HttpVerb option
            RoutePattern : string
            InputSources : HandlerInputSource list
            ResponseType : ResponseType
            RequestDelegate : RequestDelegate
        }


    type Endpoints =
        | Endpoint of EndpointHandler
        | EndpointList of Endpoints list 
        

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
                    let! _ = ctx.WriteJsonAsync(errors) // TODO: ProblemsDetails maybe?
                    return ()
            }
            :> Task
        {
            HttpVerb = None
            RoutePattern = ""
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
                    let! _ = ctx.WriteJsonAsync(errors) // TODO: ProblemsDetails maybe?
                    return ()
            }
            :> Task
        {
            HttpVerb = None
            RoutePattern = ""
            InputSources = inputSources
            ResponseType = ResponseType.Text
            RequestDelegate = new RequestDelegate(f)
        }