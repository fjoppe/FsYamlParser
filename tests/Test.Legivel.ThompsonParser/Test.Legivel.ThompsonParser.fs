﻿module Test.Legivel.ThompsonParser

open Legivel.Tokenizer
open NUnit.Framework
open FsUnitTyped
open Legivel.Utilities.RegexDSL
open Legivel.ThompsonParser

let ``start-of-line`` = RGP ("^", [Token.NoToken])
let ``end-of-file`` = RGP ("\\z", [Token.NoToken])


let assertFullMatch nfa yaml =
    let stream = RollingStream<_>.Create (tokenProcessor yaml) (TokenData.Create (Token.EOF) "\x00")
    let r = parseIt nfa stream
    r |> ParseResult.IsMatch   |> shouldEqual true
    r |> ParseResult.FullMatch |> clts |> shouldEqual yaml


let assertGroupMatch nfa yaml gn mt =
    let stream = RollingStream<_>.Create (tokenProcessor yaml) (TokenData.Create (Token.EOF) "\x00")
    let r = parseIt nfa stream
    r |> ParseResult.IsMatch |> shouldEqual true
    r.Groups |> List.item gn |> clts |> shouldEqual mt


let assertPartialMatch nfa yaml strmatched =
    let stream = RollingStream<_>.Create (tokenProcessor yaml) (TokenData.Create (Token.EOF) "\x00")
    let r = parseIt nfa stream
    r |> ParseResult.IsMatch   |> shouldEqual true
    r |> ParseResult.FullMatch |> clts |> shouldEqual strmatched

    let rest = yaml.Substring(strmatched.Length)
    let c = stream.Get()
    c.Source.[0] |> shouldEqual rest.[0]

let assertNoMatch nfa yaml =
    let stream = RollingStream<_>.Create (tokenProcessor yaml) (TokenData.Create (Token.EOF) "\x00")
    let r = parseIt nfa stream
    r |> ParseResult.IsMatch   |> shouldEqual false
    r |> ParseResult.FullMatch |> shouldEqual []
 
[<Test>]
let ``Simple Concat``() =
    let nfa = rgxToNFA <| RGP("A", [Token.``c-printable``]) + RGP("A", [Token.``c-printable``]) + RGP("B", [Token.``c-printable``])

    assertFullMatch nfa "AAB"
    assertNoMatch nfa "aab"


[<Test>]
let ``Simple Or``() =
    let nfa = rgxToNFA <|  (RGP("A", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``]))

    assertFullMatch nfa "A"
    assertFullMatch nfa "B"

    assertNoMatch nfa "C"


[<Test>]
let ``Simple Or with nested concat``() =
    let nfa = rgxToNFA <|  (RGP("AC", [Token.``c-printable``]) ||| RGP("BC", [Token.``c-printable``]))

    assertFullMatch nfa "AC"
    assertFullMatch nfa "BC"

    assertNoMatch nfa "AB"
    assertNoMatch nfa "BD"
    assertNoMatch nfa "C"

[<Test>]
let ``Simple Or with concat before``() =
    let nfa = rgxToNFA <|  RGP("A", [Token.``c-printable``]) + (RGP("C", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``]))

    assertFullMatch nfa "AC"
    assertFullMatch nfa "AB"
    
    assertNoMatch nfa "B"
    assertNoMatch nfa "AD"


[<Test>]
let ``Simple Or with concat after``() =
    let nfa = rgxToNFA <|  (RGP("A", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``])) + RGP("GH", [Token.``c-printable``])  

    assertFullMatch nfa "AGH"
    assertFullMatch nfa "BGH"
    
    assertNoMatch nfa "C"
    assertNoMatch nfa "BA"


[<Test>]
let ``Simple Or with conflicting start characters``() =
    let nfa = rgxToNFA <|  (RGP("AB", [Token.``nb-json``]) ||| RGP("A", [Token.``nb-json``]))

    assertFullMatch nfa "AB"
    assertFullMatch nfa "A"
    
    assertNoMatch nfa "C"
    
    assertPartialMatch nfa "ABA" "AB"
    assertPartialMatch nfa "ACA" "A"


[<Test>]
let ``Complex Or with various nested concats``() =
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
let ``Complex Or with deep nested concats``() =
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


let ``Simple Or with simple overlapping concat``() =
    let nfa = rgxToNFA <|  (RGP("AB", [Token.``c-printable``]) ||| RGP("AC", [Token.``c-printable``]))

    assertFullMatch nfa "AB"
    assertFullMatch nfa "AC"

    assertNoMatch nfa "AD"
    assertNoMatch nfa "B"


let ``Simple Or with nested overlapping concat``() =
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
let ``Colliding Plain/OneOf within Or with simple concat``() =
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
let ``Colliding Plain/OneOf within Or with canabalizing refactoring``() =
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
let ``Colliding double OneOf within Or``() =
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
let ``Simple optional at the end``() =
    let nfa = 
        rgxToNFA <| RGP("A", [Token.``c-printable``]) + OPT(RGP("X", [Token.``c-printable``]))
    
    assertFullMatch nfa "AX"
    assertFullMatch nfa "A"

    assertPartialMatch nfa "AY" "A"

    assertNoMatch nfa "B"


[<Test>]
let ``Simple optional at the beginnig``() =
    let nfa = 
        rgxToNFA <| OPT(RGP("X", [Token.``c-printable``])) + RGP("A", [Token.``c-printable``]) 
    
    assertFullMatch nfa "XA"
    assertFullMatch nfa "A"
    assertPartialMatch nfa "AY" "A"

    assertNoMatch nfa "B"
    assertNoMatch  nfa "XB"



[<Test>]
let ``Complex optional with Colliding plain enter-and-exit paths``() =
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
let ``Complex optional with Colliding plain enter-and-exit paths to MultiPath``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGP("AA", [Token.``c-printable``]) + (RGP("B", [Token.``c-printable``]) ||| RGP("C", [Token.``c-printable``]))) + 
               (RGP("AA", [Token.``c-printable``]) + (RGP("D", [Token.``c-printable``]) ||| RGP("E", [Token.``c-printable``])))
    
    assertFullMatch nfa "AABAAD"
    assertFullMatch nfa "AACAAD"
    assertFullMatch nfa "AABAAE"
    assertFullMatch nfa "AACAAE"

    assertFullMatch nfa "AAD"
    assertFullMatch nfa "AAE"

    assertNoMatch nfa "AABAABAAD"
    assertNoMatch nfa "AAF"
    assertNoMatch nfa "AAB"
    assertNoMatch nfa "AABAA"


[<Test>]
let ``Complex optional with Colliding oneinset enter-and-exit paths``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + RGP("A", [Token.``c-printable``])) + 
                RGO("\t[0-9]", [Token.``t-tab``; Token.``ns-dec-digit``]) + RGP("B", [Token.``c-printable``]) 

    assertFullMatch nfa "\tA\tB"
    assertFullMatch nfa "\nA\tB"

    assertFullMatch nfa "\tA0B"
    assertFullMatch nfa "\tA9B"
    assertFullMatch nfa "\nA0B"
    assertFullMatch nfa "\nA9B"

    assertFullMatch nfa "\tB"
    assertFullMatch nfa "0B"
    assertFullMatch nfa "9B"

    assertPartialMatch nfa "\tBcc" "\tB"

    assertNoMatch nfa "\tC"
    assertNoMatch nfa "\tA\nA0B"


[<Test>]
let ``Complex optional with Colliding oneinset enter-and-exit paths, splitting in MultiPaths``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + (RGP("A", [Token.``c-printable``]) ||| RGP("B", [Token.``c-printable``]))) + 
                RGO("\t[0-9]", [Token.``t-tab``; Token.``ns-dec-digit``]) + (RGP("C", [Token.``c-printable``]) ||| RGP("D", [Token.``c-printable``])) 

    assertFullMatch nfa "\tA\tC"
    assertFullMatch nfa "\tB\tC"
    assertFullMatch nfa "\nA\tC"
    assertFullMatch nfa "\nB\tC"

    assertFullMatch nfa "\tA0C"
    assertFullMatch nfa "\tB1C"
    assertFullMatch nfa "\nA2C"
    assertFullMatch nfa "\nB3C"

    assertFullMatch nfa "\tA\tD"
    assertFullMatch nfa "\tB\tD"
    assertFullMatch nfa "\nA\tD"
    assertFullMatch nfa "\nB\tD"

    assertFullMatch nfa "\tA0D"
    assertFullMatch nfa "\tB1D"
    assertFullMatch nfa "\nA2D"
    assertFullMatch nfa "\nB3D"

    assertFullMatch nfa "\tC"
    assertFullMatch nfa "0C"
    assertFullMatch nfa "1D"
    assertFullMatch nfa "2C"
    assertFullMatch nfa "3D"

    assertPartialMatch nfa "\nB3Czzzz" "\nB3C"

    assertNoMatch nfa "\tA\tB0C"


[<Test>]
let ``Complex optional with Colliding oneinset/plain enter-and-exit paths with plain in exit-path``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + RGP("A", [Token.``c-printable``])) + 
                (RGP("\t", [Token.``t-tab``]) + RGP("D", [Token.``c-printable``])) 

    assertFullMatch nfa "\tA\tD"
    assertFullMatch nfa "\nA\tD"
    assertFullMatch nfa "\tD"

    assertPartialMatch nfa "\tA\tD123" "\tA\tD"

    assertNoMatch nfa "\tA\tA\tD"
    assertNoMatch nfa "\nA\tA\tD"
    assertNoMatch nfa "\tA\nA\tD"

[<Test>]
let ``Complex optional with Colliding oneinset/plain enter-and-exit paths with plain in iter-path``() =
    let nfa = 
        rgxToNFA <| 
            OPT(RGP("\t", [Token.``t-tab``]) + RGP("D", [Token.``c-printable``])) + 
               (RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + RGP("A", [Token.``c-printable``])) 

    assertFullMatch nfa "\tD\tA"
    assertFullMatch nfa "\tD\nA"
    assertFullMatch nfa "\tA"
    assertFullMatch nfa "\nA"

    assertPartialMatch nfa "\tD\tA123" "\tD\tA"

    assertNoMatch nfa "\tD\tD\tA"
    assertNoMatch nfa "\tD\tD\nA"
    assertNoMatch nfa "\nD"
    assertNoMatch nfa "\nD\tA"

[<Test>]
let ``Simple zero or more test``() =
    let nfa = 
        rgxToNFA <| 
                ZOM(RGP("CD", [Token.``c-printable``])) + RGP("CE", [Token.``c-printable``]) 

    assertFullMatch nfa "CE"
    assertFullMatch nfa "CDCE"
    assertFullMatch nfa "CDCDCE"

    assertNoMatch  nfa "CD"


[<Test>]
let ``Zero or more with non-distinctive iter/exit repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                ZOM(RGP("A", [Token.``c-printable``])) + RGP("AB", [Token.``c-printable``]) 

    assertFullMatch nfa "AB"
    assertFullMatch nfa "AAB"
    assertFullMatch nfa "AAAB"

    assertNoMatch nfa "X"
    assertNoMatch nfa "A"

[<Test>]
let ``Simple one or more test``() =
    let nfa = 
        rgxToNFA <| 
                OOM(RGP("CD", [Token.``c-printable``])) + RGP("CE", [Token.``c-printable``]) 

    assertFullMatch nfa "CDCE"
    assertFullMatch nfa "CDCDCE"

    assertNoMatch nfa "CE"
    assertNoMatch nfa "C"
    assertNoMatch nfa "A"

[<Test>]
let ``One or more with non-distinctive iter/exit repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OOM(RGP("A", [Token.``c-printable``])) + RGP("AB", [Token.``c-printable``]) 

    assertFullMatch nfa "AAB"
    assertFullMatch nfa "AAAB"
    assertFullMatch nfa "AAAAB"

    assertNoMatch nfa "AB"
    assertNoMatch nfa "A"
    assertNoMatch nfa "X"


[<Test>]
let ``Simple Overlapping plain multipaths in iter/exit repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    RGP("A", [Token.``c-printable``]) |||
                    RGP("B", [Token.``c-printable``]) 
                    ) +
                    (
                    RGP("A", [Token.``c-printable``]) |||
                    RGP("C", [Token.``c-printable``]) 
                    )

    assertFullMatch nfa "AA"
    assertFullMatch nfa "BA"
    assertFullMatch nfa "AC"
    assertFullMatch nfa "BC"

    assertFullMatch nfa "A"
    assertFullMatch nfa "C"

    assertPartialMatch nfa "BAC" "BA"

[<Test>]
let ``Complex Overlapping plain multipaths in iter/exit repeat-paths``() =
    
    let nfa = 
        rgxToNFA <| 
                OPT(
                    (RGP("A", [Token.``c-printable``]) |||
                     RGP("B", [Token.``c-printable``])) + RGP("E", [Token.``c-printable``])
                    ) +
                    (
                    RGP("A", [Token.``c-printable``]) |||
                    RGP("C", [Token.``c-printable``]) 
                    ) + RGP("F", [Token.``c-printable``])

    assertFullMatch nfa "AEAF"
    assertFullMatch nfa "BEAF"
    assertFullMatch nfa "AECF"
    assertFullMatch nfa "BECF"

    assertFullMatch nfa "AF"
    assertFullMatch nfa "CF"

    assertNoMatch nfa "BEAEAF"
    assertNoMatch nfa "BEAECF"


[<Test>]
let ``Simple Overlapping OneInSet multipaths in iter/exit repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    RGP("A", [Token.``c-printable``]) |||
                    RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``]) 
                    ) +
                    (
                    RGP("B", [Token.``c-printable``]) |||
                    RGO("\t\n", [Token.``t-tab``; Token.NewLine])
                    )

    assertFullMatch nfa "AB"
    assertFullMatch nfa "\tB"
    assertFullMatch nfa "-B"

    assertFullMatch nfa "A\t"
    assertFullMatch nfa "\t\t"
    assertFullMatch nfa "-\t"

    assertFullMatch nfa "A\n"
    assertFullMatch nfa "\t\n"
    assertFullMatch nfa "-\n"


    assertFullMatch nfa "B"
    assertFullMatch nfa "\t"
    assertFullMatch nfa "\n"


    assertNoMatch nfa "AAA"


[<Test>]
let  ``Complex Overlapping OneInSet multipaths in iter/exit repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    (RGP("A", [Token.``c-printable``]) |||
                     RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``]))
                    + RGP("E", [Token.``c-printable``])
                    ) +
                    (
                    RGP("B", [Token.``c-printable``]) |||
                    RGO("\t\n", [Token.``t-tab``; Token.NewLine])
                    ) + RGP("F", [Token.``c-printable``])


    assertFullMatch nfa "AEBF"
    assertFullMatch nfa "\tEBF"
    assertFullMatch nfa "-EBF"

    assertFullMatch nfa "AE\tF"
    assertFullMatch nfa "\tE\tF"
    assertFullMatch nfa "-E\tF"

    assertFullMatch nfa "AE\nF"
    assertFullMatch nfa "\tE\nF"
    assertFullMatch nfa "-E\nF"


    assertFullMatch nfa "BF"
    assertFullMatch nfa "\tF"
    assertFullMatch nfa "\nF"

    assertNoMatch nfa "\tE\tE\tF"


[<Test>]
let ``Complex double refactor overlapping OneInSet,plains multipaths in iter/exit repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    RGP("A", [Token.``c-printable``]) |||
                    RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``]) 
                    ) +
                    (
                    RGP("A", [Token.``c-printable``]) |||
                    RGO("\t\n", [Token.``t-tab``; Token.NewLine])
                    )

    assertFullMatch nfa "AA"
    assertFullMatch nfa "\tA"
    assertFullMatch nfa "-A"

    assertFullMatch nfa "A\t"
    assertFullMatch nfa "\t\t"
    assertFullMatch nfa "-\t"

    assertFullMatch nfa "A\n"
    assertFullMatch nfa "\t\n"
    assertFullMatch nfa "-\n"


    assertFullMatch nfa "A"
    assertFullMatch nfa "\t"
    assertFullMatch nfa "\n"



[<Test>]
let  ``Complex Overlapping I-OneInSet and X-Plain in I/X multipaths repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    (RGP("A", [Token.``c-printable``]) |||
                     RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``]))
                    + RGP("E", [Token.``c-printable``])
                    ) +
                    (
                    RGP("B", [Token.``c-printable``]) |||
                    RGP("\t", [Token.``t-tab``])
                    ) + RGP("F", [Token.``c-printable``])

    assertFullMatch nfa "AEBF"
    assertFullMatch nfa "\tEBF"
    assertFullMatch nfa "-EBF"

    assertFullMatch nfa "AE\tF"
    assertFullMatch nfa "\tE\tF"
    assertFullMatch nfa "-E\tF"

    assertFullMatch nfa "BF"
    assertFullMatch nfa "\tF"

    assertNoMatch nfa "AE\tEBF"
    assertNoMatch nfa "\tE\tE\tF"


[<Test>]
let ``Complex Overlapping I-Plain and X-OneInSet in I/X multipaths repeat-paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    (RGP("A", [Token.``c-printable``]) |||
                     RGP("\t", [Token.``t-tab``]))
                    + RGP("E", [Token.``c-printable``])
                    ) +
                    (
                    RGP("B", [Token.``c-printable``]) |||
                    RGO("\t\n", [Token.``t-tab``; Token.NewLine])
                    ) + RGP("F", [Token.``c-printable``])


    assertFullMatch nfa "AEBF"
    assertFullMatch nfa "\tEBF"

    assertFullMatch nfa "AE\tF"
    assertFullMatch nfa "\tE\tF"

    assertFullMatch nfa "AE\nF"
    assertFullMatch nfa "\tE\nF"

    assertFullMatch nfa "BF"
    assertFullMatch nfa "\tF"
    assertFullMatch nfa "\nF"

    assertNoMatch nfa "\tE\tE\tF"

[<Test>]
let ``Simple non-colliding nested Repeater paths``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("A", [Token.``c-printable``]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGP("C", [Token.``c-printable``]) 
    
    assertFullMatch nfa "ABC"
    assertFullMatch nfa "BC"
    assertFullMatch nfa "C"

    assertNoMatch nfa "AAC"
    assertNoMatch nfa "AABC"
    assertNoMatch nfa "ABBC"
    assertNoMatch nfa "BAC"
    assertNoMatch nfa "BBC"


[<Test>]
let ``Colliding plains in nested Repeater I-path with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("A", [Token.``c-printable``]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGP("A", [Token.``c-printable``]) 
    
    assertFullMatch nfa "ABA"
    assertFullMatch nfa "BA"
    assertFullMatch nfa "A"

    assertPartialMatch nfa "ABABA" "ABA"
    assertPartialMatch nfa "BABA" "BA"


[<Test>]
let ``Colliding plains in nested Repeater I-path with two states deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("AC", [Token.``c-printable``]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGP("AD", [Token.``c-printable``])
    
    assertFullMatch nfa "ACBAD"
    assertFullMatch nfa "BAD"
    assertFullMatch nfa "AD"

    assertNoMatch nfa "ACBABAD" 
    assertNoMatch nfa "BACBAD"


[<Test>]
let ``Colliding OiS in nested Repeater I-path with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGO("-\t", [Token.``t-hyphen``; Token.``t-tab``]) 
    
    assertFullMatch nfa "\tB\t"
    assertFullMatch nfa "\nB\t"

    assertFullMatch nfa "\tB-"
    assertFullMatch nfa "\nB-"

    assertFullMatch nfa "-"
    assertFullMatch nfa "\t"

    assertPartialMatch nfa "\tB\tB-" "\tB\t"
    assertPartialMatch nfa "\nB\tB-" "\nB\t"


[<Test>]
let ``Colliding OiS in nested Repeater I-paths with two states deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + RGP("A", [Token.``c-printable``]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGO("-\t", [Token.``t-hyphen``; Token.``t-tab``]) + RGP("D", [Token.``c-printable``])
    
    assertFullMatch nfa "\tAB\tD"
    assertFullMatch nfa "\nAB\tD"

    assertFullMatch nfa "B-D"
    assertFullMatch nfa "B\tD"

    assertFullMatch nfa "-D"
    assertFullMatch nfa "\tD"


[<Test>]
let ``Colliding Plain/OiS in nested Repeater I-paths with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("\t", [Token.``t-tab``]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGO("-\t", [Token.``t-hyphen``; Token.``t-tab``]) 
    
    assertFullMatch nfa "\tB\t"

    assertFullMatch nfa "\tB-"

    assertFullMatch nfa "-"
    assertFullMatch nfa "\t"

    assertPartialMatch nfa "\tB\tB-" "\tB\t"


[<Test>]
let ``Colliding Plain/OiS in nested Repeater I-path with two states deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGO("\t\n", [Token.``t-tab``; Token.NewLine]) + RGP("A", [Token.``c-printable``]))+ RGP("B", [Token.``c-printable``])
                ) +
                RGP("\t", [Token.``t-tab``]) + RGP("D", [Token.``c-printable``])
    
    assertFullMatch nfa "\tAB\tD"
    assertFullMatch nfa "\nAB\tD"
    assertFullMatch nfa "B\tD"
    assertFullMatch nfa "\tD"



[<Test>]
let ``Colliding plains in nested Repeater X-path with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("B", [Token.``c-printable``]))+ RGP("A", [Token.``c-printable``])
                ) +
                RGP("A", [Token.``c-printable``]) 
    
    assertFullMatch nfa "BAA"
    assertFullMatch nfa "AA"
    assertFullMatch nfa "A"

    assertNoMatch nfa "BABAA"
    assertPartialMatch nfa "ABAA" "A"


[<Test>]
let ``Colliding plains in nested Repeater X-path with two states deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("B", [Token.``c-printable``]))+ RGP("AC", [Token.``c-printable``])
                ) +
                RGP("AD", [Token.``c-printable``])
    
    assertFullMatch nfa "BACAD"
    assertFullMatch nfa "ACAD"
    assertFullMatch nfa "AD"

    assertNoMatch nfa "BACACAD" 
    assertNoMatch nfa "BACBACAD"


[<Test>]
let ``Colliding Plain/OiS in nested Repeater X-paths with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("B", [Token.``c-printable``]))+ RGP("\t", [Token.``t-tab``])
                ) +
                RGO("-\t", [Token.``t-hyphen``; Token.``t-tab``]) 
    
    assertFullMatch nfa "B\t\t"
    assertFullMatch nfa "B\t-"


    assertFullMatch nfa "\t\t"
    assertFullMatch nfa "\t-"

    assertFullMatch nfa "-"
    assertFullMatch nfa "\t"

    assertNoMatch nfa "B\tB\t-"


[<Test>]
let ``Colliding Plain/OiS in nested Repeater X-path with two states deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    OPT(RGP("B", [Token.``c-printable``]) + RGP("A", [Token.``c-printable``]))+ RGO("\t\n", [Token.``t-tab``; Token.NewLine])
                ) +
                RGP("\t", [Token.``t-tab``]) + RGP("D", [Token.``c-printable``])
    
    assertFullMatch nfa "BA\t\tD"
    assertFullMatch nfa "BA\n\tD"
    assertFullMatch nfa "\t\tD"
    assertFullMatch nfa "\n\tD"

    assertFullMatch nfa "\tD"
    assertFullMatch nfa "\tD"



[<Test>]
let ``Colliding plains in main path with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    RGP("A", [Token.``c-printable``]) +
                    OPT(RGP("B", [Token.``c-printable``]))+ RGP("C", [Token.``c-printable``])
                ) +
                RGP("AD", [Token.``c-printable``]) 
    
    assertFullMatch nfa "ABCAD"
    assertFullMatch nfa "ACAD"
    assertFullMatch nfa "AD"



[<Test>]
let ``Colliding plains in main path with two state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    RGP("AE", [Token.``c-printable``]) +
                    OPT(RGP("B", [Token.``c-printable``]))+ RGP("C", [Token.``c-printable``])
                ) +
                RGP("AED", [Token.``c-printable``]) 
    
    assertFullMatch nfa "AEBCAED"
    assertFullMatch nfa "AECAED"
    assertFullMatch nfa "AED"


[<Test>]
let ``Colliding plains in main path into the I-path with one state deep``() =
    let nfa = 
        rgxToNFA <| 
                OPT(
                    RGP("A", [Token.``c-printable``]) +
                    OPT(RGP("B", [Token.``c-printable``]))+ RGP("C", [Token.``c-printable``])
                ) +
                RGP("ABD", [Token.``c-printable``]) 
    
    assertFullMatch nfa "ABCABD"
    assertFullMatch nfa "ACABD"
    assertFullMatch nfa "ABD"


[<Test>]
let ``Partial Repeat match After Fullmatch``() =
    let nfa = 
        rgxToNFA <| 
                ZOM(RGP("ABC", [Token.``c-printable``]))
    
    assertPartialMatch nfa "ABCABCABD" "ABCABC"
    assertPartialMatch nfa "ABCAB" "ABC"



module Groups =
    [<Test>]
    let ``Simple group test``() =
        let nfa = 
            rgxToNFA <| 
                    RGP("AB", [Token.``c-printable``]) + GRP(RGP("CD", [Token.``c-printable``])) + RGP("AB", [Token.``c-printable``])
    
        assertFullMatch nfa "ABCDAB" 
        assertGroupMatch nfa "ABCDAB" 0 "CD"


    [<Test>]
    let ``Group with nested Repeat test``() =
        let nfa = 
            rgxToNFA <| 
                    RGP("AB", [Token.``c-printable``]) + GRP(ZOM(RGP("CD", [Token.``c-printable``]))) + RGP("AB", [Token.``c-printable``])
    
        assertFullMatch nfa "ABAB" 
        assertFullMatch nfa "ABCDAB" 

        assertGroupMatch nfa "ABCDAB" 0 "CD"
        assertGroupMatch nfa "ABCDCDAB" 0 "CDCD"

    [<Test>]
    let ``Ambigious group test plain/plain direct with one char``() =
        let nfa = 
            rgxToNFA <| (RGP("AB", [Token.``c-printable``]) ||| GRP(RGP("AC", [Token.``c-printable``])))
    

        assertGroupMatch nfa "AC" 0 "AC"
        assertGroupMatch nfa "AB" 0 ""


        assertFullMatch nfa "AB" 
        assertFullMatch nfa "AC" 

    [<Test>]
    let ``Ambigious group test plain/plain direct with multiple chars``() =
        let nfa = 
            rgxToNFA <| (RGP("ABC", [Token.``c-printable``]) ||| GRP(RGP("ABD", [Token.``c-printable``])))
    

        assertGroupMatch nfa "ABD" 0 "ABD"
        assertGroupMatch nfa "ABC" 0 ""


        assertFullMatch nfa "ABC" 
        assertFullMatch nfa "ABD" 



    [<Ignore("Not sure if we need this, but .Net Regex provides a spec")>]
    [<Test>]
    let ``Ambigious group test plain/plain indicisive``() =
        let nfaGroupAfter = 
            rgxToNFA <| (RGP("A", [Token.``c-printable``]) ||| GRP(RGP("A", [Token.``c-printable``])))
    
        assertGroupMatch nfaGroupAfter "A" 0 "A"
        assertFullMatch nfaGroupAfter "A" 

        let nfaGroupBefore = 
            rgxToNFA <| (GRP(RGP("A", [Token.``c-printable``])) ||| RGP("A", [Token.``c-printable``]))
    
        assertGroupMatch nfaGroupBefore "A" 0 "A"
        assertFullMatch nfaGroupBefore "A" 


    [<Test>]
    let ``Ambigious group test plain/plain with joined preceeding path``() =
        let nfa = 
            rgxToNFA <| 
                RGP("D", [Token.``c-printable``]) +
                (RGP("AB", [Token.``c-printable``]) ||| GRP(RGP("AC", [Token.``c-printable``])))
    

        assertGroupMatch nfa "DAC" 0 "AC"
        assertGroupMatch nfa "DAB" 0 ""


        assertFullMatch nfa "DAB" 
        assertFullMatch nfa "DAC" 

    [<Test>]
    let ``Ambigious group test plain/plain with joined succeeding path``() =
        let nfa = 
            rgxToNFA <| 
                (RGP("AB", [Token.``c-printable``]) ||| GRP(RGP("AC", [Token.``c-printable``])))
                + RGP("D", [Token.``c-printable``])
    

        assertGroupMatch nfa "ACD" 0 "AC"
        assertGroupMatch nfa "ABD" 0 ""


        assertFullMatch nfa "ABD" 
        assertFullMatch nfa "ACD" 


    [<Test>]
    let ``Ambigious group test ois/ois direct``() =
        let nfa = 
            rgxToNFA <| 
                (
                    RGO("\t\n", [Token.``t-tab``;Token.NewLine]) + RGP("A", [Token.``c-printable``]) ||| 
                    GRP(RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``])) + RGP("B", [Token.``c-printable``])
                ) 
    

        assertGroupMatch nfa "-B" 0 "-"
        assertGroupMatch nfa "\tB" 0 "\t"
        assertGroupMatch nfa "\tA" 0 ""
        assertGroupMatch nfa "\nA" 0 ""

        assertFullMatch nfa "\tA" 
        assertFullMatch nfa "\tB" 
        assertFullMatch nfa "\nA" 
        assertFullMatch nfa "-B" 


    [<Test>]
    let ``Ambigious group test ois/ois  with joined preceeding path``() =
        let nfa = 
            rgxToNFA <| 
                RGP("D", [Token.``c-printable``]) +
                (
                    RGO("\t\n", [Token.``t-tab``;Token.NewLine]) + RGP("A", [Token.``c-printable``]) ||| 
                    GRP(RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``])) + RGP("B", [Token.``c-printable``])
                ) 
    

        assertGroupMatch nfa "D-B" 0 "-"
        assertGroupMatch nfa "D\tB" 0 "\t"
        assertGroupMatch nfa "D\tA" 0 ""
        assertGroupMatch nfa "D\nA" 0 ""

        assertFullMatch nfa "D\tA" 
        assertFullMatch nfa "D\tB" 
        assertFullMatch nfa "D\nA" 
        assertFullMatch nfa "D-B" 


    [<Test>]
    let ``Ambigious group test ois/ois  with joined succeeding path``() =
        let nfa = 
            rgxToNFA <| 
                (
                    RGO("\t\n", [Token.``t-tab``;Token.NewLine]) + RGP("A", [Token.``c-printable``]) ||| 
                    GRP(RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``])) + RGP("B", [Token.``c-printable``])
                ) +
                RGP("D", [Token.``c-printable``])
    

        assertGroupMatch nfa "-BD" 0 "-"
        assertGroupMatch nfa "\tBD" 0 "\t"
        assertGroupMatch nfa "\tAD" 0 ""
        assertGroupMatch nfa "\nAD" 0 ""

        assertFullMatch nfa "\tAD" 
        assertFullMatch nfa "\tBD" 
        assertFullMatch nfa "\nAD" 
        assertFullMatch nfa "-BD" 


    [<Test>]
    let ``Ambigious group test ois/plain direct``() =
        let nfa = 
            rgxToNFA <| 
                (
                    RGP("\t", [Token.``t-tab``;Token.NewLine]) + RGP("A", [Token.``c-printable``]) ||| 
                    GRP(RGO("\t-", [Token.``t-tab``; Token.``t-hyphen``])) + RGP("B", [Token.``c-printable``])
                ) 
    

        assertGroupMatch nfa "-B" 0 "-"
        assertGroupMatch nfa "\tB" 0 "\t"
        assertGroupMatch nfa "\tA" 0 ""

        assertFullMatch nfa "\tA" 
        assertFullMatch nfa "\tB" 
        assertFullMatch nfa "-B" 


    [<Test>]
    let ``Ambigious Repeats on Group test``() =
        let ``s-indent(n)`` = Repeat(RGP (HardValues.``s-space``, [Token.``t-space``]), 1)
        let ``s-indent(<=n)`` = Range(RGP (HardValues.``s-space``, [Token.``t-space``]), 0, 1)  (* Where m ≤ n *)
        

        let nfap1 = 
            rgxToNFA <| OPT(ZOM(HardValues.``b-as-line-feed`` + ``s-indent(n)``))

        let nfap2 =
            rgxToNFA <| ZOM((``s-indent(<=n)``) + HardValues.``b-non-content``)


        let nfa = 
            rgxToNFA <|
            GRP(
                OPT(
                    ZOM(HardValues.``b-as-line-feed`` + ``s-indent(n)``)
                )
            )
            + ZOM((``s-indent(<=n)``) + HardValues.``b-non-content``)
        

        //  combos made with: OPT(ZOM(HardValues.``b-as-line-feed`` + ``s-indent(n)``))
        assertFullMatch nfap1 ""
        assertFullMatch nfap1 "\n "
        assertFullMatch nfap1 "\n \n "  // <-- ambigious

        //  combos made with: ZOM((``s-indent(<=n)``) + HardValues.``b-non-content``)
        assertFullMatch nfap2 ""
        assertFullMatch nfap2 "\n"
        assertFullMatch nfap2 " \n"
        assertFullMatch nfap2 "\n\n"
        assertFullMatch nfap2 "\n \n"   // <-- ambigious
        assertFullMatch nfap2 " \n \n"


        // combos made with the full rgx
        assertFullMatch nfa ""
        assertFullMatch nfa "\n "
        assertFullMatch nfa "\n \n "
        
        assertFullMatch nfa "\n"
        assertFullMatch nfa " \n"
        assertFullMatch nfa "\n\n"
        assertFullMatch nfa "\n \n"
        assertFullMatch nfa " \n \n"

        assertGroupMatch nfa "\n \n" 1 "\n \n"

    [<Test>]
    let ``Two groups with nested Repeat test``() =
        let nfa = 
            rgxToNFA <| 
                    RGP( "AB", [Token.``c-printable``]) + 
                    GRP(ZOM(RGP("CD", [Token.``c-printable``]))) + 
                    RGP("AB", [Token.``c-printable``]) + 
                    GRP(ZOM(RGP("EF", [Token.``c-printable``]))) + 
                    RGP("AB", [Token.``c-printable``])
    
        assertFullMatch nfa "ABABAB" 
        assertFullMatch nfa "ABCDABAB" 
        assertFullMatch nfa "ABABEFAB" 

        assertGroupMatch nfa "ABCDABAB" 0 "CD"
        assertGroupMatch nfa "ABCDCDABAB" 0 "CDCD"

        assertGroupMatch nfa "ABCDABEFAB" 1"EF"
        assertGroupMatch nfa "ABCDCDABEFEFAB" 1 "EFEF"


module ``Start of Line`` =
    [<Test>]
    let ``Start of Line optional match``() =
        let nfa = 
            rgxToNFA <|
                    (RGO("-\n", [Token.``t-hyphen``; Token.NewLine])) +
                    (RGP("AB", [Token.``nb-json``]) ||| 
                        ``start-of-line`` + RGP("CD", [Token.``nb-json``])) +
                    RGP("EF", [Token.``nb-json``])

        assertFullMatch nfa "-ABEF"
        assertFullMatch nfa "\nABEF"
        assertFullMatch nfa "\nCDEF"

        assertNoMatch nfa "-CDEF"

    [<Test>]
    let ``Start of Line Concat``() =
        let nfa = 
            rgxToNFA <|
                (RGO("-\n", [Token.``t-hyphen``; Token.NewLine])) +
                ``start-of-line`` + RGP("CD", [Token.``nb-json``])
        
        assertFullMatch nfa "\nCD"
        assertNoMatch nfa "-CD"

    [<Test>]
    let ``Start of Line Concat in group``() =
        let nfa = 
            rgxToNFA <|
                RGO("-\n", [Token.``t-hyphen``; Token.NewLine]) +

                ((``start-of-line`` + RGP("CD", [Token.``nb-json``])) |||
                    (RGP("FG", [Token.``nb-json``])))
                + RGP("HI", [Token.``nb-json``])
        
        assertFullMatch nfa "\nCDHI"
        assertFullMatch nfa "-FGHI"
        assertFullMatch nfa "\nFGHI"

        assertNoMatch nfa "-CDHI"


    [<Test>]
    let ``Zero Or More Start of Line in group``() =
        let nfa = 
            rgxToNFA <|
            ZOM(RGO("-\n", [Token.``t-hyphen``; Token.NewLine]) +
            
                            ((``start-of-line`` + RGP("CD", [Token.``nb-json``])) |||
                                (RGP("FG", [Token.``nb-json``])))
                            + RGP("HI", [Token.``nb-json``]))

        assertFullMatch nfa "\nCDHI"
        assertFullMatch nfa "-FGHI"
        assertFullMatch nfa "\nFGHI"

        assertFullMatch nfa "\nCDHI-FGHI"
        
        assertPartialMatch nfa "\nCDHI-A" "\nCDHI"
        assertPartialMatch nfa "-CDHI" ""

[<Test>]
let ``Nested loops - test infinite parsing``() =
    let nfa = 
        rgxToNFA <|
            ZOM(OPT(RGP("A", [Token.``nb-json``])) + ZOM(RGP("C", [Token.``nb-json``])))


    assertFullMatch nfa "A"
    assertFullMatch nfa "AC"
    assertFullMatch nfa "C"

    assertFullMatch nfa "CC"
    assertFullMatch nfa "CA"
    assertPartialMatch nfa "CD" "C"


[<Test>]
let ``Nested option in ZOM loop - test infinite parsing``() =
    let nfa = 
        rgxToNFA <|
            ZOM(OPT(RGP("A", [Token.``nb-json``])))

    assertFullMatch nfa "A"
    assertPartialMatch nfa "AB" "A"

