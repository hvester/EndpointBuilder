namespace PetStore

open System.Threading.Tasks
open System.Collections.Generic

/// Dummy pet repository
type PetRepo() =

    let mutable nextIndex = 0
    let pets = Dictionary<int, Pet>()

    member _.GetPet(petId) =
        match pets.TryGetValue petId with
        | false, _ -> Task.FromResult(None)
        | true, pet -> Task.FromResult(Some pet)

    member _.AddPet(pet : Pet) =
        let petWithId = { pet with Id = nextIndex }
        pets.Add(nextIndex, petWithId)
        nextIndex <- nextIndex + 1
        Task.FromResult(petWithId)

    member _.UpdatePet(pet : Pet) =
        if pets.ContainsKey pet.Id then
            pets.[pet.Id] <- pet
            Task.FromResult(true)
        else
            Task.FromResult(false)

