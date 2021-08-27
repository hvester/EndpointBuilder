namespace EndpointBuilder

open System


module MimeTypes =

    type MimeTypeAttribute(mimeType : string) =
        inherit Attribute()
        member _.MimeType = mimeType


    type MimeType() = class end


    type NoResponseBody() = inherit MimeType()


    [<MimeType("text/plain")>]
    type TextPlain() = inherit MimeType()


    [<MimeType("application/json")>]
    type ApplicationJson() = inherit MimeType()



module StatusCodeTypes =

    type StatusCodeAttribute(statusCode : int) =
        inherit Attribute()
        member _.StatusCode = statusCode


    [<StatusCode(200)>]   
    type StatusCode() = class end


    [<StatusCode(200)>]
    type OK() = inherit StatusCode()


    [<StatusCode(201)>]
    type Created() = inherit StatusCode()


    [<StatusCode(202)>]
    type Accepted() = inherit StatusCode()


    [<StatusCode(204)>]
    type NoContent() = inherit StatusCode()



open Giraffe
open MimeTypes
open StatusCodeTypes


type ResponseHandler<'Response, 'StatusCode, 'MimeType when 'StatusCode :> StatusCode and 'MimeType :> MimeType> =
    | ResponseHandler of HttpHandler
        


type Response private () =

    static member Ok(response : string) : ResponseHandler<string, OK, TextPlain> =
        ResponseHandler (text response)
        
    static member Ok(response : 'T) : ResponseHandler<'T, OK, ApplicationJson> =
        ResponseHandler (json response)

    static member Created() : ResponseHandler<unit, Created, NoResponseBody> =
        ResponseHandler (Successful.created (fun _ -> earlyReturn))
    
    static member Created(response : string) : ResponseHandler<string, Created, TextPlain> =
        ResponseHandler (Successful.created (text response))    

    static member Created(response : 'T) : ResponseHandler<'T, Created, ApplicationJson> =
        ResponseHandler (Successful.created (json response))

    static member Accepted(response : string) : ResponseHandler<string, Accepted, TextPlain> =
        ResponseHandler (Successful.accepted (text response))
        
    static member Accepted(response : 'T) : ResponseHandler<'T, Accepted, ApplicationJson> =
        ResponseHandler (Successful.accepted (json response))

    static member NoContent() : ResponseHandler<unit, NoContent, NoResponseBody> =
        ResponseHandler Successful.NO_CONTENT



type ErrorResponse private () =

    static member BadRequest(response : string) =
        ResponseHandler (RequestErrors.badRequest (text response))

    static member BadRequest(response : 'T) =
        ResponseHandler (RequestErrors.badRequest (json response))

    static member Unauthorized(scheme, realm, response : string) =
        ResponseHandler (RequestErrors.unauthorized scheme realm (text response))

    static member Unauthorized(scheme, realm, response : 'T) =
        ResponseHandler (RequestErrors.unauthorized scheme realm (json response))

    static member Forbidden(response : string) =
        ResponseHandler (RequestErrors.forbidden (text response))

    static member Forbidden(response : 'T) =
        ResponseHandler (RequestErrors.forbidden (json response))

    static member NotFound(response : string) =
        ResponseHandler (RequestErrors.notFound (text response))

    static member NotFound(response : 'T) =
        ResponseHandler (RequestErrors.notFound (json response))
