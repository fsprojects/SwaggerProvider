module Swagger.GitHub.Tests

open SwaggerProvider
open FSharp.Data
open NUnit.Framework
open FsUnit

let [<Literal>] schema = __SOURCE_DIRECTORY__ + "/Schemes/GitHub.json"
let [<Literal>] headers = "User-Agent=Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"
type GitHub = SwaggerProvider<schema, headers>

[<Test; Explicit>]
let ``Get fsprojects from GitHub`` () =
    GitHub.Repos.OrgRepos("fsprojects").Length
    |> should be (greaterThan 0)

