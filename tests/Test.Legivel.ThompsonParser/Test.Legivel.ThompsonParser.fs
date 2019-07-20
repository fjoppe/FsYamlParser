﻿module Test.Legivel.ThompsonParser

open Legivel.Tokenizer
open NUnit.Framework
open FsUnitTyped
open Legivel.Utilities.RegexDSL
open Legivel.ThompsonParser



let assertFullMatch nfa str =
    let r = parseIt nfa str
    r |> ParseResult.IsMatch   |> shouldEqual true
    r |> ParseResult.FullMatch |> clts |> shouldEqual str

let assertPartialMatch nfa str strmatched =
    let r = parseIt nfa str
    r |> ParseResult.IsMatch   |> shouldEqual true
    r |> ParseResult.FullMatch |> clts |> shouldEqual strmatched

let assertNoMatch nfa str =
    let r = parseIt nfa "XYC"
    r |> ParseResult.IsMatch   |> shouldEqual false
    r |> ParseResult.FullMatch |> shouldEqual []
 
[<Test>]
let ``Simple Concat - match string``() =
    let nfa = rgxToNFA <| RGP("A", [Token.``c-printable``]) + RGP("A", [Token.``c-printable``]) + RGP("B", [Token.``c-printable``])

    assertFullMatch nfa "AAB"
    assertFullMatch nfa "aab"


[<Test>]
let ``Simple Or - match string``() =
    let nfa = rgxToNFA <|  (RGP("A", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``]))

    assertFullMatch nfa "A"
    assertFullMatch nfa "B"

    assertNoMatch nfa "C"


[<Test>]
let ``Simple Or with nested concat - match string``() =
    let nfa = rgxToNFA <|  (RGP("AC", [Token.``c-printable``]) ||| RGP("BC", [Token.``c-printable``]))

    assertFullMatch nfa "AC"
    assertFullMatch nfa "BC"

    assertNoMatch nfa "AB"
    assertNoMatch nfa "BD"
    assertNoMatch nfa "C"

[<Test>]
let ``Simple Or with concat before - match string``() =
    let nfa = rgxToNFA <|  RGP("A", [Token.``c-printable``]) + (RGP("C", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``]))

    assertFullMatch nfa "AC"
    assertFullMatch nfa "AB"
    
    assertNoMatch nfa "B"
    assertNoMatch nfa "AD"


[<Test>]
let ``Simple Or with concat after - match string``() =
    let nfa = rgxToNFA <|  (RGP("A", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``])) + RGP("GH", [Token.``c-printable``])  

    assertFullMatch nfa "AGH"
    assertFullMatch nfa "BGH"
    
    assertNoMatch nfa "C"
    assertNoMatch nfa "BA"


[<Test>]
let ``Complex Or with various nested concats - match string``() =
    let nfa = 
        rgxToNFA <| 
        RGP("XY", [Token.``c-printable``]) + 
        (RGP("A", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``])) + 
        RGP("GH", [Token.``c-printable``]) + 
        (RGP("ABD", [Token.``c-printable``]) ||| RGP("ABDAC", [Token.``c-printable``]))

    assertFullMatch nfa "XYAGHABD"
    assertFullMatch nfa "XYBGHABD"
    assertFullMatch nfa "XYBGHABDAC"

    assertNoMatch nfa "XYC"
    assertNoMatch nfa "XYCABE"


[<Test>]
let ``Complex Or with deep nested concats - match string``() =
    let nfa = 
        rgxToNFA <| 
        RGP("XY", [Token.``c-printable``]) + 
        (RGP("AB", [Token.``c-printable``]) ||| (RGP("BA", [Token.``c-printable``]) + 
            (RGP("CX", [Token.``c-printable``]) ||| RGP("DX", [Token.``c-printable``])))) + 
            RGP("GH", [Token.``c-printable``]) + 
            (RGP("ABD", [Token.``c-printable``]) ||| RGP("ABDAC", [Token.``c-printable``]))

    assertFullMatch nfa "XYABGHABD"
    assertFullMatch  nfa "XYBACXGHABDAC"
    assertFullMatch nfa "XYBADXGHABD"

    assertNoMatch nfa "XYC"
    assertNoMatch nfa "XYBADY"


let ``Simple Or with simple overlapping concat - match string``() =
    let nfa = rgxToNFA <|  (RGP("AB", [Token.``c-printable``]) ||| RGP("AC", [Token.``c-printable``]))

    assertFullMatch nfa "AB"
    assertFullMatch nfa "AC"

    assertNoMatch nfa "AD"
    assertNoMatch nfa "B"


let ``Simple Or with nested overlapping concat - match string``() =
    let nfa = rgxToNFA <|  
        (
            RGP("AAB",  [Token.``c-printable``]) 
        ||| RGP("AACA", [Token.``c-printable``]) 
        ||| RGP("AACB", [Token.``c-printable``]) 
        ||| RGP("AABA", [Token.``c-printable``]) 
        ||| RGP("BA",   [Token.``c-printable``])
        ||| RGP("BC",   [Token.``c-printable``])
        ||| RGP("CD",   [Token.``c-printable``])
        )

    assertFullMatch nfa "AAB"
    assertFullMatch nfa "AACA"
    assertFullMatch nfa "AACB"
    assertFullMatch nfa "AABA"
    assertFullMatch nfa "BA"
    assertFullMatch nfa "BC"
    assertFullMatch nfa "CD"

    assertPartialMatch nfa "AABD" "AAB"

    assertNoMatch nfa "AACD"
    assertNoMatch nfa "AAD"
    assertNoMatch nfa "AD"
    assertNoMatch nfa "D"


[<Test>]
let ``Conflicting Plain/OneOf within Or with simple concat - match string``() =
    let nfa = rgxToNFA <|  (
        (RGP("\n", [Token.NewLine]) + RGP("A", [Token.``c-printable``])) ||| 
        (RGO("B\n", [Token.``c-printable``;Token.NewLine]) + RGP("X", [Token.``c-printable``]))
    )

    assertFullMatch nfa "\nA"
    assertFullMatch nfa "\nX"
    assertFullMatch nfa "BX"

    assertNoMatch nfa "?"
    assertNoMatch nfa "\nB"
    assertNoMatch nfa "BA"


[<Test>]
let ``Conflicting Plain/OneOf within Or with canabalizing refactoring - match string``() =
    let nfa = 
        rgxToNFA <| (
            (RGP("\n", [Token.NewLine]) + RGP("A", [Token.``c-printable``])) ||| 
            (RGO("\t\n", [Token.``t-tab``;Token.NewLine]) + RGP("X", [Token.``c-printable``])) |||
            (RGP("\t", [Token.``t-tab``]) + RGP("Y", [Token.``c-printable``]))
        )

    assertFullMatch nfa "\nA"
    assertFullMatch nfa "\tX"
    assertFullMatch nfa "\nX"
    assertFullMatch nfa "\tY"

    assertNoMatch nfa "?"
    assertNoMatch nfa "\nY"
    assertNoMatch nfa "\nY"
    assertNoMatch nfa "BA"


[<Test>]
let ``Conflicting double OneOf within Or - match string``() =
    let nfa = 
        rgxToNFA <| (
            (RGP("\n", [Token.NewLine]) + RGP("A", [Token.``c-printable``])) ||| 
            (RGO("\t\n", [Token.``t-tab``;Token.NewLine]) + RGP("X", [Token.``c-printable``])) |||
            (RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``]) + RGP("Y", [Token.``c-printable``])) 
        )

    assertFullMatch nfa "\nA"
    assertFullMatch nfa "\tX"
    assertFullMatch nfa "\nX"
    assertFullMatch nfa "\tY"
    assertFullMatch nfa "-Y"

    assertNoMatch nfa "?"
    assertNoMatch nfa "\nY"
    assertNoMatch nfa "\nY"
    assertNoMatch nfa "-X"
    assertNoMatch nfa "BA"


[<Test>]
let ``Simple optional at the end - match string``() =
    let nfa = 
        rgxToNFA <| RGP("A", [Token.``c-printable``]) + OPT(RGP("X", [Token.``c-printable``]))
    
    assertFullMatch nfa "AX"
    assertFullMatch nfa "A"

    assertPartialMatch nfa "AY" "A"

    assertNoMatch nfa "B"


[<Test>]
let ``Simple optional at the beginnig - match string``() =
    let nfa = 
        rgxToNFA <| OPT(RGP("X", [Token.``c-printable``])) + RGP("A", [Token.``c-printable``]) 
    
    assertFullMatch nfa "XA"
    assertFullMatch nfa "A"
    assertPartialMatch nfa "AY" "A"

    assertNoMatch nfa "B"
    assertNoMatch  nfa "XB"



[<Test>]
let ``Complex optional with conflicting plain enter-and-exit paths - match string``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGP("AAC", [Token.``c-printable``])) + 
                RGP("AAB", [Token.``c-printable``]) 
    
    assertFullMatch nfa "AAB"
    assertFullMatch nfa "AACAAB"

    assertNoMatch nfa "AAD"
    assertNoMatch nfa "B"
    assertNoMatch nfa "AACAD"

[<Test>]
let ``Complex optional with conflicting oneinset enter-and-exit paths - match string``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + RGP("A", [Token.``c-printable``])) + 
                RGO("\t[0-9]", [Token.``c-printable``; Token.``ns-dec-digit``]) + RGP("B", [Token.``c-printable``]) 

    assertFullMatch nfa "\tA\tB"

    assertNoMatch nfa "\tC"