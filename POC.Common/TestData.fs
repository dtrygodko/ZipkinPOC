namespace POC.Common

[<AllowNullLiteral>]
type TestData (id: int, data: string) =
    member val Id = id with get, set
    member val Data = data with get, set