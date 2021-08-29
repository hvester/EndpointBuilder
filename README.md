# EndpointBuilder

EndpointBuilder is an experimental project exploring ways to add [OpenAPI](https://swagger.io/docs/specification/about/) generation support for a programming model like [Giraffe Endpoint Routing](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#endpoint-routing). This library (in its current state) provides an alternative for Endpoint Routing that is incompatible with it but integrates with rest of Giraffe.

Design goals of the project:
- Support OpenAPI generation with minimal effort in a way that both the functionality and corresponding documentation are defined at the same time
- Building blocks should be composable and extensible
- Composition of building blocks should be type-safe
- Avoid "magic"

Note: This library is **not ready for real world use**. Some features are only partially implemented and most of the functionality is not properly tested! 

## Example

```fsharp
let endpoints = [
    subRoute "/api" [

        post "/pet"
            (handler {
                let! pet = fromJsonBody<Pet>
                and! petRepo = fromServices<PetRepo>
                return task {
                    let! petId = petRepo.AddPet pet
                    return Response.Created petId
                }
            }
            |> withSummary "Add a new pet to the store")

        getf "/pet/{petId:%i}" (fun petIdFromPath ->
            (handler {
                let! petId = petIdFromPath
                and! petRepo = fromServices<PetRepo>
                return task {
                    match! petRepo.GetPet(petId) with
                    | None -> return ErrorResponse.NotFound "Pet not found"
                    | Some pet -> return Response.Ok pet
                }
            }
            |> withSummary "Find pet by ID"))
    ]
]
```
![OpenAPI documentation for POST endpoint](/docs/images/example_screenshot_post.png)
![OpenAPI documentation for GET endpoint](/docs/images/example_screenshot_get.png)

## Try it out

[.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) is required.

To run the sample application:
 1. Go to folder `sample/PetStore`
 2. Run `dotnet run` or `dotnet watch run`

## Documentation

High level overview how EndpointBuilder is used:
1. Compose handler inputs, domain logic and response handler together into an endpoint handler using `handler` computation expression.
2. Plug endpoint handlers in the leaf positions of the routing tree created with routing combinators.
3. Configure endpoints to be used in Startup class.

The concepts are explained in more detail below.

### Handler inputs

Handler inputs are building blocks for extracting data from HTTP request that the endpoint requires, such as query parameters, path parameters or request body. They are represented by `HandlerInput` type,which is a record type consisting of a function to get the value from `HttpContext` and metadata what is being extracted. The metadata is represented by a list of `HandlerInputSource`, which is a discriminated union of different "recognized" input sources.

```fsharp
type HandlerInput<'T> =
    {
        GetInputValue : HttpContext -> Task<Result<'T, HandlerInputError list>>
        InputSources : HandlerInputSource list
    }
```

Following handler inputs are provided for getting data from path parameters, from query or from header. They support `string`, `int`, `float` and `Guid` as type parameters.

```fsharp
let fromPath<'T> (parameterName : string) : HandlerInput<'T> = ...

let fromQuery<'T> (parameterName : string) : HandlerInput<'T> = ...

let fromHeader<'T> (headerName : string) : HandlerInput<'T> = ...
```

Following can be used to get json from request body and to deserialize it to `'T`.

```fsharp
let fromJsonBody<'T> : HandlerInput<'T> = ...
```

There are also the handler inputs that are not related to OpenAPI.

To get access to `HttpContext` use

```fsharp
let getHttpContext : HandlerInput<HttpContext> =
```

To retrieve a dependency registered to ASP.NET Core service container use

```fsharp
let fromServices<'T> : HandlerInput<'T> =
```

### Response handler

A response handler is a wrapped Giraffe `HttpHandler` that is responsible for setting HTTP status code and MIME type of the response and writing the response body to `HttpContext`. Response handler also carries corresponding metadata in its type parameters. Status code and MIME type are represented by marker classes. For each status code and MIME type there is a corresponding class.

```fsharp
type ResponseHandler<'Response, 'StatusCode, 'MimeType when 'StatusCode :> StatusCode and 'MimeType :> MimeType> =
    | ResponseHandler of HttpHandler
```

Response handlers can be created by static methods in `Response` class. Method names correspond to HTTP status codes. MIME type is selected based the type of the response. Here are few examples:

```fsharp
static member Ok(response : string) : ResponseHandler<string, OK, TextPlain> = ...

static member Ok(response : 'T) : ResponseHandler<'T, OK, ApplicationJson> = ...

static member NoContent() : ResponseHandler<unit, NoContent, NoResponseBody> = ...
```

### `handler`computation expression

### Routing

### Schema generation

### Setting up swagger.json generation and SwaggerUI


