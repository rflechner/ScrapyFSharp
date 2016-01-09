open ScrapyFSharp
open ScrapyFSharp.CssSelectorExtensions

open System
open FSharp.Data
open FSharp.Data.HtmlNode
open FSharp.Data.HtmlAttribute


[<EntryPoint>]
let main argv = 
    printfn "%A" argv

    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeContainsPrefix demo</title>
          <style>
          a {
            display: inline-block;
          }
          </style>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
 
        <a href="example.html" hreflang="en">Some text</a>
        <a href="example.html" hreflang="en-UK">Some other text</a>
        <a href="example.html" hreflang="english">will not be outlined</a>
 
        <script>
        $( "a[hreflang|='en']" ).css( "border", "3px dotted green" );
        </script>
 
        </body>
        </html>""" |> HtmlDocument.Parse
    let link = html.CssSelect "a[hreflang|=en]" |> List.head
    let attrs = link.Attributes()

    printfn "attrs: %A" attrs

    0 // retourne du code de sortie entier
