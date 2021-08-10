namespace EndpointBuilder

open System.Text.RegularExpressions

[<AutoOpen>]
module Routing =  

    type Endpoints =
        | Endpoint of string * EndpointHandler
        | EndpointList of string * Endpoints list


    let internal getEndpointHandlers (endpoints : Endpoints list) =
        let rec loop acc es =
            seq {
                for e in es do
                    match e with
                    | Endpoint(path, handler) ->
                        yield (acc + path, handler)

                    | EndpointList(path, innerEndpoints) ->
                        yield! loop (acc + path) innerEndpoints
            }
        loop "" endpoints


    let rec private updateEndpointHandlers mapping (endpoints : Endpoints list) =
        endpoints
        |> List.map (function
            | EndpointList(path, innerEndpoints) ->
                EndpointList(path, updateEndpointHandlers mapping innerEndpoints)

            | Endpoint(path, handler) ->
                Endpoint(path, mapping handler))


    let private withHttpVerb httpVerb (endpoints : Endpoints list) =
        let endpointsWithHttpVerb =
            endpoints
            |> updateEndpointHandlers (fun endpointHandler ->
                match endpointHandler.HttpVerb with
                | Some _ -> endpointHandler
                | None -> { endpointHandler with HttpVerb = Some httpVerb })
        EndpointList("", endpointsWithHttpVerb)


    let GET endpoints = withHttpVerb HttpVerb.GET endpoints
    let POST endpoints = withHttpVerb HttpVerb.POST endpoints
    let PUT endpoints = withHttpVerb HttpVerb.PUT endpoints
    let PATCH endpoints = withHttpVerb HttpVerb.PATCH endpoints
    let DELETE endpoints = withHttpVerb HttpVerb.DELETE endpoints
    let HEAD endpoints = withHttpVerb HttpVerb.HEAD endpoints
    let OPTIONS endpoints = withHttpVerb HttpVerb.OPTIONS endpoints
    let TRACE endpoints = withHttpVerb HttpVerb.TRACE endpoints
    let CONNECT endpoints = withHttpVerb HttpVerb.CONNECT endpoints


    let route path (endpointHandler : EndpointHandler) = Endpoint (path, endpointHandler)


    let get pattern endpointHandler = GET [ route pattern endpointHandler ]
    let post pattern endpointHandler = POST [ route pattern endpointHandler ]
    let put pattern endpointHandler = PUT [ route pattern endpointHandler ]
    let patch pattern endpointHandler = PATCH [ route pattern endpointHandler ]
    let delete pattern endpointHandler = DELETE [ route pattern endpointHandler ]
    let head pattern endpointHandler = HEAD [ route pattern endpointHandler ]
    let options pattern endpointHandler = OPTIONS [ route pattern endpointHandler ]
    let trace pattern endpointHandler = TRACE [ route pattern endpointHandler ]
    let connect pattern endpointHandler = CONNECT [ route pattern endpointHandler ]


    let internal pathParameterRegex = Regex("\{([a-zA-Z0-9_]+):([a-z]+:)*(\%s|\%i)\}")


    let routef (format : PrintfFormat<_,_,_,_, 'T>) (createEndpointHandler : HandlerInput<'T> -> EndpointHandler) =
        // TODO: Support more than one path parameter
        let path = format.Value
        let m = pathParameterRegex.Matches(path).[0]
        let variableName = m.Groups.[1].Value
        route path (createEndpointHandler (pathParameter<'T> variableName))


    let subRoute path (endpoints : Endpoints list) =
        EndpointList (path, endpoints)
