namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("ScrapyFSharp")>]
[<assembly: AssemblyProductAttribute("ScrapyFSharp")>]
[<assembly: AssemblyDescriptionAttribute("Reborn of Scrapysharp in FSharp")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
