namespace PetStore

[<AutoOpen>]
module DomainModels =


    type Category =
        {
            Id : int
            Name : string
        }


    type Tag =
        {
            Id : int
            Name : string
        }


    type Status =
        | Available
        | Pending
        | Sold


    type Pet =
        {
            Id : int
            Name : string
            Category : Category
            PhotoUrls : string array
            Tags : Tag array
            Status : Status
        }