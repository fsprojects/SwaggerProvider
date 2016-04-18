module Swagger.GitHub.Tests

open SwaggerProvider
open FSharp.Data
open NUnit.Framework
open FsUnitTyped

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/Schemas/GitHub.json"
type GitHubTy = SwaggerProvider<Schema>
let GitHub = GitHubTy("api.github.com", Headers = [|"User-Agent","Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"|])

[<Test; Explicit>]
let ``Get fsprojects from GitHub`` () =
    GitHub.OrgRepos("fsprojects").Length
    |> shouldBeGreaterThan 0

