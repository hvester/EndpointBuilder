namespace EndpointBuilder

open System.Text.RegularExpressions
open Giraffe

[<AutoOpen>]
module Routing =  


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


    type Endpoints =
        | Endpoint of string * HttpVerb option * EndpointHandler
        | EndpointList of Endpoints list


    let internal getEndpointHandlers (endpoints : Endpoints list) =
        let rec loop endpointList =
            seq {
                for innerEndpoints in endpointList do
                    match innerEndpoints with
                    | Endpoint(path, httpVerb, handler) ->
                        yield (path, httpVerb, handler)

                    | EndpointList innerEndpointList ->
                        yield! loop innerEndpointList
            }
        loop endpoints


    let rec private updateEndpoints mapping (endpoints : Endpoints list) =
        endpoints
        |> List.map (function
            | EndpointList endpointList ->
                EndpointList (updateEndpoints mapping endpointList)

            | Endpoint(path, httpVerb, handler) ->
                Endpoint (mapping (path, httpVerb, handler)))


    let private withHttpVerb httpVerb (endpoints : Endpoints list) =
        let updateHttpVerb (path, oldHttpVerb, handler) =
            let newHttpVerb =
                match oldHttpVerb with
                | Some _ -> oldHttpVerb
                | None -> Some httpVerb
            (path, newHttpVerb, handler)
        EndpointList (updateEndpoints updateHttpVerb endpoints)


    let GET endpoints       = withHttpVerb HttpVerb.GET endpoints
    let POST endpoints      = withHttpVerb HttpVerb.POST endpoints
    let PUT endpoints       = withHttpVerb HttpVerb.PUT endpoints
    let PATCH endpoints     = withHttpVerb HttpVerb.PATCH endpoints
    let DELETE endpoints    = withHttpVerb HttpVerb.DELETE endpoints
    let HEAD endpoints      = withHttpVerb HttpVerb.HEAD endpoints
    let OPTIONS endpoints   = withHttpVerb HttpVerb.OPTIONS endpoints
    let TRACE endpoints     = withHttpVerb HttpVerb.TRACE endpoints


    let route path (endpointHandler : EndpointHandler) = Endpoint (path, None, endpointHandler)


    let get path endpointHandler        = Endpoint (path, Some HttpVerb.GET, endpointHandler)
    let post path endpointHandler       = Endpoint (path, Some HttpVerb.POST, endpointHandler)
    let put path endpointHandler        = Endpoint (path, Some HttpVerb.PUT, endpointHandler)
    let patch path endpointHandler      = Endpoint (path, Some HttpVerb.PATCH, endpointHandler)
    let delete path endpointHandler     = Endpoint (path, Some HttpVerb.DELETE, endpointHandler)
    let head path endpointHandler       = Endpoint (path, Some HttpVerb.HEAD, endpointHandler)
    let options path endpointHandler    = Endpoint (path, Some HttpVerb.OPTIONS, endpointHandler)
    let trace path endpointHandler      = Endpoint (path, Some HttpVerb.TRACE, endpointHandler)


    let internal pathParameterRegex = Regex("\{([a-zA-Z0-9_]+):([a-z]+:)*(\%s|\%i)\}")


    let routef (format : PrintfFormat<_,_,_,_, 'T>) (createEndpointHandler : HandlerInput<'T> -> EndpointHandler) =
        // TODO: Support more than one path parameter
        let path = format.Value
        let m = pathParameterRegex.Matches(path).[0]
        let variableName = m.Groups.[1].Value
        route path (createEndpointHandler (fromPath<'T> variableName))


    let subRoute path (endpoints : Endpoints list) =
        let updatePath (oldPath, httpVerb, handler) =
            (path + oldPath, httpVerb, handler)
        EndpointList (updateEndpoints updatePath endpoints)


    let subRoutef (format : PrintfFormat<_,_,_,_, 'T>) (createEndpointHandler : HandlerInput<'T> -> Endpoints list) =
        // TODO: Support more than one path parameter
        let path = format.Value
        let m = pathParameterRegex.Matches(path).[0]
        let variableName = m.Groups.[1].Value
        subRoute path (createEndpointHandler (fromPath<'T> variableName))


    let applyHandler httpHandler (endpoints : Endpoints list) =
        let updateHttpHandler (path, httpVerb, handler) =
            let newHttpHandler = httpHandler >=> handler.HttpHandler
            (path, httpVerb, { handler with HttpHandler = newHttpHandler })  
        EndpointList (updateEndpoints updateHttpHandler endpoints)
