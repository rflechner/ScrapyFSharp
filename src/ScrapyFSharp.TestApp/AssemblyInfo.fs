namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("ScrapyFSharp.TestApp")>]
[<assembly: AssemblyProductAttribute("ScrapyFSharp")>]
[<assembly: AssemblyDescriptionAttribute("Reborn of Scrapysharp in FSharp")>]
[<assembly: AssemblyVersionAttribute("0.0.1")>]
[<assembly: AssemblyFileVersionAttribute("0.0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.1"
