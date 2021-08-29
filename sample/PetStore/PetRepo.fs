namespace PetStore

open System.Threading.Tasks
open System.Collections.Generic

/// Dummy pet repository
type PetRepo() =

    let mutable nextIndex = 1
    let pets = Dictionary<int, Pet>()

    do
        let frida =
            {
                Id = 0
                Name = "Frida"
                Category = { Id = 0; Name = "Dachshund (Wire Haired)" }
                PhotoUrls = [||]
                Tags = [||]
                Status = Sold
            }
        pets.Add(0, frida)

    member _.GetPet(petId) =
        match pets.TryGetValue petId with
        | false, _ -> Task.FromResult(None)
        | true, pet -> Task.FromResult(Some pet)

    member _.AddPet(pet : Pet) =
        let petId = nextIndex
        nextIndex <- nextIndex + 1
        let petWithId = { pet with Id = petId }
        pets.Add(nextIndex, petWithId)
        Task.FromResult(petId)

    member _.UpdatePet(pet : Pet) =
        if pets.ContainsKey pet.Id then
            pets.[pet.Id] <- pet
            Task.FromResult(true)
        else
            Task.FromResult(false)

