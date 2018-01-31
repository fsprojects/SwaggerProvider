module Swagger.GitHub.Tests

open SwaggerProvider
open Expecto

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/Schemas/GitHub.json"
let [<Literal>] Host = "https://api.github.com"
type GitHub = SwaggerProvider<Schema, PreferAsync = true>
let github = GitHub.Client(Host, Headers = [|"User-Agent","Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"|])

type TaskGitHub = SwaggerProvider<Schema>
let taskGitHub = TaskGitHub.Client(Host, Headers = [|"User-Agent","Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"|])

[<Tests>] // Explicit
let githubTest =
    ptestCaseAsync "All/Get fsprojects from GitHub" <| async {
        let! repos = github.OrgRepos("fsprojects")
        Expect.isGreaterThan repos.Length 0 "F# community is strong"
    }        

[<Tests>]       
let taskGitHubTest =
    ptestCase "All/Get fsproject from GitHub with Task" <| fun _ -> 
        let length = taskGitHub.OrgRepos("fsprojects").Result.Length
        Expect.isGreaterThan length 0 "F# community is strong"
