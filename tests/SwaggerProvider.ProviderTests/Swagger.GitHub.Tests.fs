module Swagger.GitHub.Tests

open SwaggerProvider
open FSharp.Data
open Expecto

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/Schemas/GitHub.json"
type GitHub = SwaggerProvider<Schema>
let github = GitHub.Client("api.github.com", Headers = [|"User-Agent","Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"|])

[<Tests>] // Explicit
let githubTest =
    ptestCase "All/Get fsprojects from GitHub" <| fun _ ->
        Expect.isGreaterThan
            (github.OrgRepos("fsprojects").Length)
            0 "F# community is strong"

