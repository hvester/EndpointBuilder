namespace EndpointBuilder

open System.Text.RegularExpressions

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
        | EndpointList of string * Endpoints list


    let internal getEndpointHandlers (endpoints : Endpoints list) =
        let rec loop acc es =
            seq {
                for e in es do
                    match e with
                    | Endpoint(path, httpVerb, handler) ->
                        yield (acc + path, httpVerb, handler)

                    | EndpointList(path, innerEndpoints) ->
                        yield! loop (acc + path) innerEndpoints
            }
        loop "" endpoints


    let rec private updateHttpVerb httpVerb (endpoints : Endpoints list) =
        endpoints
        |> List.map (function
            | EndpointList(path, innerEndpoints) ->
                EndpointList(path, updateHttpVerb httpVerb innerEndpoints)

            | Endpoint(path, oldHttpVerb, handler) ->
                let newHttpVerb = if oldHttpVerb.IsSome then oldHttpVerb else Some httpVerb
                Endpoint(path, newHttpVerb, handler))


    let private withHttpVerb httpVerb (endpoints : Endpoints list) =
        EndpointList("", updateHttpVerb httpVerb endpoints)


    let GET endpoints = withHttpVerb HttpVerb.GET endpoints
    let POST endpoints = withHttpVerb HttpVerb.POST endpoints
    let PUT endpoints = withHttpVerb HttpVerb.PUT endpoints
    let PATCH endpoints = withHttpVerb HttpVerb.PATCH endpoints
    let DELETE endpoints = withHttpVerb HttpVerb.DELETE endpoints
    let HEAD endpoints = withHttpVerb HttpVerb.HEAD endpoints
    let OPTIONS endpoints = withHttpVerb HttpVerb.OPTIONS endpoints
    let TRACE endpoints = withHttpVerb HttpVerb.TRACE endpoints


    let route path (endpointHandler : EndpointHandler) = Endpoint (path, None, endpointHandler)


    let private routeWithVerb path httpVerb handler = Endpoint (path, Some httpVerb, handler)
    let get path endpointHandler = routeWithVerb path HttpVerb.GET endpointHandler
    let post path endpointHandler = routeWithVerb path HttpVerb.POST endpointHandler
    let put path endpointHandler = routeWithVerb path HttpVerb.PUT endpointHandler
    let patch path endpointHandler = routeWithVerb path HttpVerb.PATCH endpointHandler
    let delete path endpointHandler = routeWithVerb path HttpVerb.DELETE endpointHandler
    let head path endpointHandler = routeWithVerb path HttpVerb.HEAD endpointHandler
    let options path endpointHandler = routeWithVerb path HttpVerb.OPTIONS endpointHandler
    let trace path endpointHandler = routeWithVerb path HttpVerb.TRACE endpointHandler


    let internal pathParameterRegex = Regex("\{([a-zA-Z0-9_]+):([a-z]+:)*(\%s|\%i)\}")


    let routef (format : PrintfFormat<_,_,_,_, 'T>) (createEndpointHandler : HandlerInput<'T> -> EndpointHandler) =
        // TODO: Support more than one path parameter
        let path = format.Value
        let m = pathParameterRegex.Matches(path).[0]
        let variableName = m.Groups.[1].Value
        route path (createEndpointHandler (pathParameter<'T> variableName))


    let subRoute path (endpoints : Endpoints list) =
        EndpointList (path, endpoints)
