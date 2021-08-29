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

`ErrorResponse` class contains methods for creating response handlers for request errors. It is worth noting that those response handlers are generic with respect to all type parameters. Reason for this is that this way a function can return successful reponse handler from one branch and error response handler from another branch, which results in the return type of the function to be determined by the succesful response handler, i.e. type parameters represent metadata of the successful response.

### `handler` computation expression

`EndpointHandler` is the type that contains the `HttpHandler` responsible for the complete handling of a HTTP request (excluding routing) and metadata about request parameters, responses, etc. `EndpointHandler` is composed from handler inputs, domain logic and possible response handlers using `handler` computation expression. Is is an applicative-style computation expression that binds `HandlerInput`s with `let!`and `and!`. The value returned from the `handler` CE must be of type `Task<ResponseHandler<_,_,_>>`, i.e. the response handler to be used should be returned asynchronously. `handler` then wraps everything into an `EndpointHandler`.

Example:

```fsharp
let endpointHandler =
    handler {
        let! x = fromQuery<int> "x"
        and! body = fromJsonBody<{| Y : int |}>
        return Task.FromResult(Response.Ok {| Result = x + body.Y |})
    }
```

### Routing

Routing tree is represented with `Endpoints` type, which can be composed using functions in `Routing` module.

Single endpoint without HTTP verb defined can be created with `route` function as follows:

```fsharp
let endpoint = route "/foo" endpointHandler
````

Single endpoint with a HTTP verb can be created with functions corresponding HTTP verb names:
```fsharp
let get path endpointHandler = ...
let post path endpointHandler = ...
let put path endpointHandler = ...
...
````

`path` should follow the format that is supported by [ASP.NET Core Routing](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0). Path parameters can be extracted using the standard ASP.NET Core route template format, such as `"/hello/{name:alpha}"`, and then creating input handler for the path parameter `fromPath<string> "name"`, but there is a better way...

`routef` can be used for defining a route with a path parameter and creating the path parameter at the same time:
```fsharp
routef "/test/{name:%s}" (fun nameFromPath ->
    handler {
        let! name = nameFromPath
        return Task.FromResult(Response.Ok name)
    })
```

Format identifier can be placed as that last constraint in the curly braces. It is replaced with corresponding ASP.NET Core route constraint. Currently only two format identifiers (`%s` and `%i`) are supported and they are replaced as follows:
``` 
"{foo:%s}" -> "{foo}"
"{foo:%i}" -> "{foo:int}
```

There are also functions like `routef` that specify HTTP verb: `getf`, `postf`, `putf`, etc.

With `subRoute` a common prefix can be added to a set of endpoints:
```fsharp
subRoute "/api" [
    get  "/foo" getFooHandler
    post "/foo" postFooHandler
]
````

With `subRoutef` common prefix can be added and a path parameter can be extracted from the prefix.

### Schema generation

### Setting up swagger.json generation and SwaggerUI


