namespace EndpointBuilder

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http


module MimeTypes =

    type MimeTypeAttribute(mimeType : string) =
        inherit Attribute()
        member _.MimeType = mimeType


    type MimeType() = class end


    [<MimeType("application/json")>]
    type ApplicationJson() = inherit MimeType()


    [<MimeType("text/plain")>]
    type TextPlain() = inherit MimeType()



module StatusCodeTypes =

    type StatusCodeAttribute(statusCode : int) =
        inherit Attribute()
        member _.StatusCode = statusCode


    type StatusCode() = class end


    [<StatusCode(200)>]
    type OK() = inherit StatusCode()


    [<StatusCode(201)>]
    type Created() = inherit StatusCode()


    [<StatusCode(202)>]
    type Accepted() = inherit StatusCode()



open FSharp.Control.Tasks
open Giraffe
open MimeTypes
open StatusCodeTypes


type ResponseHandler<'Response, 'StatusCode, 'MimeType when 'StatusCode :> StatusCode and 'MimeType :> MimeType> =
    | ResponseHandler of (HttpContext -> Task)
        

[<RequireQualifiedAccess>]
module Response =

    let json (response : 'T) : ResponseHandler<'T, OK, ApplicationJson> =
        ResponseHandler (fun ctx ->
            unitTask {
                let! _ = ctx.WriteJsonAsync(response)
                return ()
            })


    let text (response : string) : ResponseHandler<string, OK, TextPlain> =
        ResponseHandler (fun ctx ->
            unitTask {
                let! _ = ctx.WriteTextAsync(response)
                return ()
            })



[<RequireQualifiedAccess>]
module ClientError =

    let notFound (response : string) : ResponseHandler<'T, 'StatusCode, 'MimeType> =
        ResponseHandler (fun (ctx : HttpContext) ->
            unitTask {
                ctx.SetStatusCode(404)
                let! _ = ctx.WriteStringAsync(response)
                return ()
            })