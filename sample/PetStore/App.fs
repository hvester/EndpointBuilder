namespace PetStore

open System
open System.Threading.Tasks
open EndpointBuilder
open FSharp.Control.Tasks

module App =


    let endpoints = [
        subRoute "/pet" [
            put ""
                (handler {
                    let! pet = fromJsonBody<Pet>
                    and! petRepo = fromServices<PetRepo>
                    return task {
                        match! petRepo.UpdatePet pet with
                        | None -> return ClientError.notFound "Pet not found"
                        | Some updatedPet -> return Response.json updatedPet
                    }
                })

            post ""
                (handler {
                    let! pet = fromJsonBody<Pet>
                    and! petRepo = fromServices<PetRepo>
                    return task {
                        let! addedPet = petRepo.AddPet pet
                        return Response.json addedPet
                    }
                })
        ]
    ]
