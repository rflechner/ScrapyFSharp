(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../packages/FSharp.Data/lib/net40"
#r "FSharp.Data.dll"

#load "../../src/ScrapyFSharp/CssParser.fs"

(** 
### Parsing a CSS file

CssParser module can parse and manipulate CSS style sheet.

*)
open System
open System.IO
open ScrapyFSharp
open ScrapyFSharp.CssParser

let css1 = 
    """
#smartbanner { position:absolute; left:0; top:-82px; 
    border-bottom:1px solid #e8e8e8; width:100%; height:78px; }

#smartbanner .sb-container { margin: 0 auto; }

#smartbanner .sb-close { position:absolute; left:5px; top:5px; 
    display:block; border:2px solid #fff; width:14px; 
    height:14px; font-family:'ArialRoundedMTBold',Arial; 
    font-size:15px; line-height:15px; text-align:center; color:#fff; 
    background:#070707; text-decoration:none; text-shadow:none; 
    border-radius:14px; box-shadow:0 2px 3px rgba(0,0,0,0.4); 
    -webkit-font-smoothing:subpixel-antialiased; }

#smartbanner .sb-close:active { font-size:13px; color:#aaa; }

#smartbanner .sb-icon { position:absolute; left:30px; top:10px; display:block; 
    width:57px; height:57px; background-color:white; background-size:cover; 
    border-radius:10px; box-shadow:0 1px 3px rgba(0,0,0,0.3); }

#smartbanner.no-icon .sb-icon { display:none; }

div.block1 {
    background-color: #661133;
    color: black;
}
    """
    |> parseCss

(**
Searching a CSS block from a selector
*)
let sbCloseActive = css1.Block "#smartbanner .sb-close:active"

(**
sbCloseActive value is
*)

(*** include-value:sbCloseActive ***)
(**
Find color property of sbCloseActive
*)

let color1 = 
    match sbCloseActive with
    | Some p -> p.Property "color"
    | None -> None

(**
color1 value is
*)

(*** include-value:color1 ***)

(*** hide ***)
#load "../../src/ScrapyFSharp/HtmlCssSelectors.fs"
#load "../../src/ScrapyFSharp.TestApp/HtmlRasterizer.fs"

open System
open System.IO
open ScrapyFSharp
open ScrapyFSharp.CssParser

(**
### Rendering a very simple HTML part

This part is experimental and just for fun.
It demonstrates we can implement CSS inheritance and HTML rendering.

Parsing a simple HTML with FSharp.Data:
*)

let html =
    """<!DOCTYPE html>
<html>
<head>
    <title>Html test 1</title>
    <style type="text/css">
		body
		{
			font-family: "Times New Roman";
			font-size: 12pt;
		}
        div.main
        {
            position: absolute;
            background-color: #a0cc9d;
            width: 200px;
        }
        div.main span
        {
            color: #00FF00;
        }
        div.main span.colorized
        {
            color: #b12121;
        }
    </style>
</head>
<body>
    <h1>Big title 1</h1>
    <div class="main">
        <span id="lorem1" class="colorized">
            Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            Nullam commodo fringilla mollis. 
            Aenean tempor gravida tellus quis elementum. 
            Maecenas finibus lectus id lectus consectetur, id ultricies risus molestie. 
            Nunc vulputate nibh velit, ut posuere arcu hendrerit eget. 
            Cras tincidunt nisl sit amet ultricies dignissim. 
            In consectetur nec odio sollicitudin consequat. 
            Maecenas elit dui, fringilla sed dolor in, scelerisque cursus ipsum. 
            Aliquam risus erat, sollicitudin vel ante vel, tristique venenatis nisl. 
            Morbi in commodo tortor.
        </span>
        <span>text 2</span>
    </div>
</body>
</html>""" |> FSharp.Data.HtmlDocument.Parse

(**
Checking HTML rendering in the Chrome browser:

![Image of chrome](img/chrome1.png)
*)


(**
Create a windows form an render a the div with class "main" inside it.
*)


open System.Windows.Forms
open ScrapyFSharp.HtmlCssSelectors
open ScrapyFSharp.CssSelectorExtensions
open HtmlRasterizer

let f = new Form()
f.Width <- 500
f.Height <- 500
f.Paint.Add (
    fun p -> 
        p.Graphics |> HtmlRasterizer.drawHtmlNode html "div.main"
        ()
)
f.Show()

(**
Checking HTML rendering in the winform:

![Image of rendergif](img/html_render1.gif)


We are far a perfect result, but it is ressembling.

*)


