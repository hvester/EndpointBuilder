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


    let endpoints = [
        subRoute "/api" [
            GET [
                route "/hello" helloHandler
            ]
            subRoute "/something" [
                POST [
                    route "/test"
                        (handler {
                            let! p1 = queryParameter "param1"
                            and! p2 = queryParameter "param2"
                            return Task.FromResult({| Response = p1 + p2 |})
                        }
                        |> json)
                ]
            ]
        ]
    ]