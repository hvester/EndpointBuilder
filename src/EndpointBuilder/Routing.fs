namespace EndpointBuilder

[<AutoOpen>]
module Routing =

    let rec private updateEndpointHandlers mapping endpointsList =
        endpointsList
        |> List.map (fun endpoints ->
            match endpoints with
            | EndpointList el -> EndpointList (updateEndpointHandlers mapping el)
            | Endpoint endpointHandler -> Endpoint (mapping endpointHandler))


    let private withHttpVerb httpVerb endpointsList =
        endpointsList
        |> updateEndpointHandlers (fun endpointHandler ->
            match endpointHandler.HttpVerb with
            | Some _ -> endpointHandler
            | None -> { endpointHandler with HttpVerb = Some httpVerb })
        |> EndpointList


    let GET endpoints = withHttpVerb HttpVerb.GET endpoints
    let POST endpoints = withHttpVerb HttpVerb.POST endpoints
    let PUT endpoints = withHttpVerb HttpVerb.PUT endpoints
    let PATCH endpoints = withHttpVerb HttpVerb.PATCH endpoints
    let DELETE endpoints = withHttpVerb HttpVerb.DELETE endpoints
    let HEAD endpoints = withHttpVerb HttpVerb.HEAD endpoints
    let OPTIONS endpoints = withHttpVerb HttpVerb.OPTIONS endpoints
    let TRACE endpoints = withHttpVerb HttpVerb.TRACE endpoints
    let CONNECT endpoints = withHttpVerb HttpVerb.CONNECT endpoints


    let route pattern (endpointHandler : EndpointHandler) =
        Endpoint { endpointHandler with RoutePattern = pattern + endpointHandler.RoutePattern }


    let get pattern endpointHandler = GET [ route pattern endpointHandler ]
    let post pattern endpointHandler = POST [ route pattern endpointHandler ]
    let put pattern endpointHandler = PUT [ route pattern endpointHandler ]
    let patch pattern endpointHandler = PATCH [ route pattern endpointHandler ]
    let delete pattern endpointHandler = DELETE [ route pattern endpointHandler ]
    let head pattern endpointHandler = HEAD [ route pattern endpointHandler ]
    let options pattern endpointHandler = OPTIONS [ route pattern endpointHandler ]
    let trace pattern endpointHandler = TRACE [ route pattern endpointHandler ]
    let connect pattern endpointHandler = CONNECT [ route pattern endpointHandler ]


    let subRoute pattern (endpoints : Endpoints list) =
        endpoints
        |> updateEndpointHandlers (fun endpointHandler ->
            { endpointHandler with RoutePattern = pattern + endpointHandler.RoutePattern })
        |> EndpointList
