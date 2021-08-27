namespace PetStore

open System
open System.Threading.Tasks
open FSharp.Control.Tasks
open Giraffe
open EndpointBuilder

module App =


    let endpoints = [
        subRoute "/pet" [
            put ""
                (handler {
                    let! pet = fromJsonBody<Pet>
                    and! petRepo = fromServices<PetRepo>
                    return task {
                        match! petRepo.UpdatePet pet with
                        | false -> return ErrorResponse.NotFound "Pet not found"
                        | true -> return Response.NoContent()
                    }
                }
                |> withSummary "Update an existing pet")

            post ""
                (handler {
                    let! pet = fromJsonBody<Pet>
                    and! petRepo = fromServices<PetRepo>
                    return task {
                        let! _ = petRepo.AddPet pet
                        return Response.Created()
                    }
                }
                |> withSummary "Add a new pet to the store")

            subRoutef "/{petId:%i}" (fun petIdFromPath -> [

                get ""
                    (handler {
                        let! petId = petIdFromPath
                        and! petRepo = fromServices<PetRepo>
                        return task {
                            match! petRepo.GetPet(petId) with
                            | None -> return ErrorResponse.NotFound "Pet not found"
                            | Some pet -> return Response.Ok pet
                        }
                    }
                    |> withSummary "Find pet by ID")
            ])
        ]
    ]
