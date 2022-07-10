let createQuote(id: string) =
    printfn "Creating Quote %A" id

let closeQuote(id: string) =
    printfn "Closing Quote %A" id


let tryF before after f input =
    before input

    try
        f input
    finally
        after input

let forQuoteVerify = tryF createQuote closeQuote

let myAction id =
    printfn "My action for Quote %A" id
    id

forQuoteVerify myAction "QUOTE_ID"
