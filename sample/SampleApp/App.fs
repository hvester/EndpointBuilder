namespace SampleApp

open System.Threading.Tasks
open EndpointBuilder

module App =

    let hello name =
        let msg = "Hello " + name + "!"
        Task.FromResult {| Message = msg |}


    let helloHandler =
        handler {
            let! name = queryParameter "name"
            return hello name
        }
        |> json


    let otherNameHandler (source : Source<string>) =
        handler {
            let! otherName = source
            return Task.FromResult({| OtherName = otherName |})
        }
        |> json


    let endpoints = [
        subRoute "/api" [
            get "/hello" helloHandler
            routef "/{otherName:%s}" otherNameHandler
            subRoute "/something" [
                GET [
                    route "/{name}/jou"
                        (handler {
                            let! p1 = queryParameter "param1"
                            and! p2 = pathParameter<int> "name"
                            return Task.FromResult({| nameNumber = p2 |})
                        }
                        |> json)
                ]
            ]
        ]
    ]