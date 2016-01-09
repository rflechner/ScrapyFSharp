(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../packages/FSharp.Data/lib/net40"
#I "../../bin/ScrapyFSharp"

(**
ScrapyFSharp
======================

Documentation

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The ScrapyFSharp library can be <a href="https://nuget.org/packages/ScrapyFSharp">installed from NuGet</a>:
      <pre>PM> Install-Package ScrapyFSharp</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

This example demonstrates how to search old library repo and forks.

*)
#r "FSharp.Data.dll"
#r "ScrapyFSharp.dll"

open System
open System.Net
open FSharp.Data
open ScrapyFSharp.CssSelectorExtensions
open ScrapyFSharp.Network

let b = browser (fun c -> { c with UserAgent=FakeUserAgent.InternetExplorer8 })

let links =
    async {
        let! state1 = b.NavigateTo(Uri "https://bitbucket.org/repo/all/1",
                        Get, HttpRequestData.FormData ["name", "scrapysharp"])
        let homePage = state1.WebPage()
        return 
            match homePage.Html() with 
            | Some html ->
                [ for div in html.CssSelect "a.repo-link" do
                    yield "https://bitbucket.org" + (div.Attribute "href").Value() ]
            | None -> List.empty
    } |> Async.RunSynchronously

(** links value is: *)

(*** include-value:links ***)

(**
Some more info

Samples & documentation
-----------------------

The library comes with comprehensible documentation. 
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content]. 
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes.

  [content]: https://github.com/fsprojects/ScrapyFSharp/tree/master/docs/content
  [gh]: https://github.com/fsprojects/ScrapyFSharp
  [issues]: https://github.com/fsprojects/ScrapyFSharp/issues
  [readme]: https://github.com/fsprojects/ScrapyFSharp/blob/master/README.md
*)
