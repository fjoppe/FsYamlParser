﻿module Test.Legivel.ThompsonParser

open Legivel.Utilities.RegexDSL
open NUnit.Framework
open FsUnitTyped
open System.Collections.Generic
open Legivel.Tokenizer
open System.Text

let EndOfStream = TokenData.Create (Token.EOF) ""


let expected (str:string) =
    str.ToCharArray() 
    |> List.ofArray 
    |> List.map(fun c -> c.ToString())
    //|> List.rev

let stripTokenData (tdl:TokenData list) = tdl |> List.map(fun i -> i.Source)

[<Test>]
let ``Parse Plain Character - sunny day``() =
    let rgxst = RGP("A", [Token.``c-printable``]) |> CreatePushParser
    let yaml = "A"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true 
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Plain String - sunny day``() =
    let rgxst = RGP("ABC", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    streamReader.Get().Token|> shouldEqual Token.EOF


[<Test>]
let ``Parse Plain String - rainy day``() =
    let rgxst = RGP("ABC", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual false
    streamReader.Get().Source |> shouldEqual "A" 


[<Test>]
let ``Parse Plain String concats - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``]) + RGP("CD", [Token.``c-printable``]) + RGP("EF", [Token.``c-printable``])|> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Plain String Ored - sunny day``() =
    let rgxst = RGP("ABCE", [Token.``c-printable``]) ||| RGP("ABD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABDE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABD")
    streamReader.Get().Source |> shouldEqual "E"

    let yaml = "ABCE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst

    mr.IsMatch |> shouldEqual true
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse RGO Character - sunny day``() =
    let rgxst = RGO("A-", [Token.``c-printable``;Token.``t-hyphen``]) |> CreatePushParser

    let yaml = "-"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    streamReader.Get().Token |> shouldEqual Token.EOF

    let yaml = "A"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    ["A"] |> shouldEqual (stripTokenData mr.FullMatch)
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Plain String with optional end - nomatch option, with residu``() =
    let rgxst = RGP("ABC", [Token.``c-printable``])  + OPT(RGP("E", [Token.``c-printable``])) |> CreatePushParser

    let yaml = "ABCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABC")
    streamReader.Get().Source |> shouldEqual "D"


[<Test>]
let ``Parse Plain String with optional end - match option, with residu``() =
    let rgxst = RGP("ABC", [Token.``c-printable``])  + OPT(RGP("E", [Token.``c-printable``])) |> CreatePushParser
    let yaml = "ABCE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCE")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Plain String with optional middle - nomatch option``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OPT(RGP("CDF", [Token.``c-printable``])) + RGP("CDEF", [Token.``c-printable``])  |> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCDEF")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Plain String with optional middle - match option``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OPT(RGP("CDF", [Token.``c-printable``])) + RGP("CDEF", [Token.``c-printable``])  |> CreatePushParser
    let yaml = "ABCDFCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCDFCDEF")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Zero Or More in the middle - zero match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Zero Or More in the middle - zero match with residu``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser
    let yaml = "ABCDE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCD")
    streamReader.Get().Source |> shouldEqual "E"


[<Test>]
let ``Parse Zero Or More in the middle - one match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser
    let yaml = "ABECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABECD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Zero Or More in the middle - two match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEECD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Zero Or More in the middle - three match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser
    let yaml = "ABEEECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEEECD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Zero Or More at the end - nomatch``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) |> CreatePushParser

    let yaml = "AB"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "AB")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Zero Or More at the end - nomatch with residu``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) |> CreatePushParser
    let yaml = "ABC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "AB")
    streamReader.Get().Source |> shouldEqual "C"


[<Test>]
let ``Parse Zero Or More at the end - one match with residu``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) |> CreatePushParser
    let yaml = "ABEC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABE")
    streamReader.Get().Source |> shouldEqual "C"


[<Test>]
let ``Parse Zero Or More at the end - two match with residu``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) |> CreatePushParser
    let yaml = "ABEEC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEE")
    streamReader.Get().Source |> shouldEqual "C"


[<Test>]
let ``Parse Zero Or More at the end - two match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) |> CreatePushParser
    let yaml = "ABEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEE")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse One Or More in the middle - rainy day``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual false
    streamReader.Get().Source |> shouldEqual "A"


[<Test>]
let ``Parse One Or More in the middle - one match with residu``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABECDF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABECD")
    streamReader.Get().Source |> shouldEqual "F"


[<Test>]
let ``Parse One Or More in the middle - two match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEECD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse One Or More in the middle, with digraph - one match``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("EF", [Token.``c-printable``])) + RGP("ED", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEFED"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEFED")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse One Or More in the middle, with digraph - nomatch``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("EF", [Token.``c-printable``])) + RGP("ED", [Token.``c-printable``]) |> CreatePushParser
    let yaml = "ABED"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual false
    streamReader.Get().Source |> shouldEqual "A"


[<Test>]
let ``Parse Range at the end - match to minimum``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("E", [Token.``c-printable``]), 2,3) |> CreatePushParser

    let yaml = "ABEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEE")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Range at the end - match to maximum``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("E", [Token.``c-printable``]), 2,3) |> CreatePushParser
    let yaml = "ABEEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEEE")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Range at the end - match to maximum with residu``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("E", [Token.``c-printable``]), 2,3) |> CreatePushParser
    let yaml = "ABEEEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEEE")
    streamReader.Get().Source |> shouldEqual "E"


[<Test>]
let ``Parse Range in the middle - match to minimum``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("ED", [Token.``c-printable``]), 2,4) + RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEDEDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEDEDEF")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Range in the middle - match to middle``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("ED", [Token.``c-printable``]), 2,4) + RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEDEDEDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEDEDEDEF")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Range in the middle - match to max``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("ED", [Token.``c-printable``]), 2,4) + RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEDEDEDEDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEDEDEDEDEF")
    streamReader.Get().Token |> shouldEqual Token.EOF

[<Test>]
let ``Parse Group in Concat - middle match in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + GRP(RGP("CD", [Token.``c-printable``])) + RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCDEF")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "CD")
    streamReader.Get().Token |> shouldEqual Token.EOF

    
[<Test>]
let ``Parse Group in Concat - end match in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + RGP("EF", [Token.``c-printable``]) + GRP(RGP("CD", [Token.``c-printable``]))  |> CreatePushParser

    let yaml = "ABEFCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEFCD")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "CD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Group in Or - match in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + (RGP("AB", [Token.``c-printable``]) ||| GRP(RGP("CD", [Token.``c-printable``])) ||| RGP("HK", [Token.``c-printable``]) )+ RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCDEF")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "CD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Group in Option - middle match in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + (OPT(GRP(RGP("CD", [Token.``c-printable``]))))+ RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABCDEF")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "CD")
    streamReader.Get().Token |> shouldEqual Token.EOF

[<Test>]
let ``Parse Group in Option - end match in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``]) + RGP("EF", [Token.``c-printable``]) + (OPT(GRP(RGP("CD", [Token.``c-printable``])))) |> CreatePushParser

    let yaml = "ABEFCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEFCD")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "CD")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Group in Option - middle nomatch in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + (OPT(GRP(RGP("CD", [Token.``c-printable``]))))+ RGP("EF", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEF")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "")
    streamReader.Get().Token |> shouldEqual Token.EOF


[<Test>]
let ``Parse Group in Option - end nomatch in group``() =
    let rgxst = RGP("AB", [Token.``c-printable``]) + RGP("EF", [Token.``c-printable``]) + (OPT(GRP(RGP("CD", [Token.``c-printable``])))) |> CreatePushParser

    let yaml = "ABEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData |> shouldEqual (expected "ABEF")
    mr.GroupsResults.Length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "")
    streamReader.Get().Token |> shouldEqual Token.EOF



//  Complex regex parse test - these caused issues during dev
let ``c-byte-order-mark`` = RGP ("\ufeff", [Token.``byte-order-mark``])
let ``c-printable`` = 
        RGO (
            "\u0009\u000a\u000d\u0020-\u007e" +   // 8 - bit, #x9 | #xA | #xD | [#x20-#x7E]
            "\u0085\u00a0-\ud7ff\ue000-\ufffd",   // 16- bit, #x85 | [#xA0-#xD7FF] | [#xE000-#xFFFD]
                                                    //  32-bit -> currently not supported because .Net does not encode naturally. Yaml: [#x10000-#x10FFFF]
            [
            Token.``t-space``; Token.``t-tab``; Token.NewLine; Token.``c-printable``; Token.``t-hyphen``; Token.``t-plus``; Token.``t-questionmark`` 
            Token.``t-colon`` ; Token.``t-comma``; Token.``t-dot`` ; Token.``t-square-bracket-start`` ; Token.``t-square-bracket-end`` ; Token.``t-curly-bracket-start``
            Token.``t-curly-bracket-end`` ; Token.``t-hash`` ; Token.``t-ampersand``; Token.``t-asterisk``; Token.``t-quotationmark``; Token.``t-pipe``
            Token.``t-gt``; Token.``t-single-quote``; Token.``t-double-quote``; Token.``t-percent``; Token.``t-commat``;Token.``t-tick``; Token.``t-forward-slash``; Token.``t-equals``
            Token.``ns-dec-digit``; Token.``c-escape``
            ])

let ``nb-json`` = 
        RGO ("\u0009\u0020-\uffff",
            [
            Token.``t-space``; Token.``t-tab``; Token.NewLine; Token.``c-printable``; Token.``t-hyphen``; Token.``t-plus``; Token.``t-questionmark`` 
            Token.``t-colon`` ; Token.``t-comma``; Token.``t-dot`` ; Token.``t-square-bracket-start`` ; Token.``t-square-bracket-end`` ; Token.``t-curly-bracket-start``
            Token.``t-curly-bracket-end`` ; Token.``t-hash`` ; Token.``t-ampersand``; Token.``t-asterisk``; Token.``t-quotationmark``; Token.``t-pipe``
            Token.``t-gt``; Token.``t-single-quote``; Token.``t-double-quote``; Token.``t-percent``; Token.``t-commat``;Token.``t-tick``; Token.``t-forward-slash``; Token.``t-equals``
            Token.``ns-dec-digit``; Token.``c-escape``; Token.``nb-json``
            ])

let ``s-space`` : string = "\u0020"
let ``s-tab`` = "\u0009"  
let ``nb-char``  = ``c-printable`` - RGO("\u000a\u000d", [Token.NewLine]) 
let ``s-white`` = RGO(``s-space`` + ``s-tab``, [Token.``t-space``; Token.``t-tab``])
let ``ns-char`` = ``nb-char`` - ``s-white``
let ``start-of-line`` = RGP ("^", [Token.NoToken])
let ``b-line-feed`` = RGP ("\u000a", [Token.NewLine])
let ``b-carriage-return`` = RGP ("\u000d", [Token.NewLine])
let ``b-break`` = 
        (``b-carriage-return`` + ``b-line-feed``) |||  //  DOS, Windows
        ``b-carriage-return``                          |||  //  MacOS upto 9.x
        ``b-line-feed``                                     //  UNIX, MacOS X
let ``b-non-content`` = ``b-break``
let ``b-comment`` = ``b-non-content`` ||| RGP("\\z", [Token.EOF]) // EOF..
let ``s-separate-in-line`` = OOM(``s-white``) ||| ``start-of-line``
let ``c-nb-comment-text`` = RGP("#", [Token.``t-hash``]) + ZOM(``nb-char``)
let ``s-b-comment`` = OPT(``s-separate-in-line`` + OPT(``c-nb-comment-text``)) + ``b-comment`` 
let ``l-comment`` = ``s-separate-in-line`` + OPT(``c-nb-comment-text``) + ``b-comment``
let ``s-l-comments`` = (``s-b-comment`` ||| ``start-of-line``) + ZOM(``l-comment``)
let ``s-indent(n)`` ps = Repeat(RGP (``s-space``, [Token.``t-space``]), ps)
let ``s-indent(<n)`` ps = Range(RGP (``s-space``, [Token.``t-space``]), 0, (ps-1))
let ``s-indent(<=n)`` ps = Range(RGP (``s-space``, [Token.``t-space``]), 0, ps)
let ``s-flow-line-prefix`` ps = (``s-indent(n)`` ps) + OPT(``s-separate-in-line``)
let ``s-separate-lines`` ps = (``s-l-comments`` + (``s-flow-line-prefix`` ps)) ||| ``s-separate-in-line``
let ``l-document-prefix`` = OPT(``c-byte-order-mark``) + ZOM(``l-comment``)
let ``c-document-end`` =
        let dot = RGP("\\.", [Token.``t-dot``])
        dot + dot + dot
let ``l-document-suffix`` = ``c-document-end`` + ``s-l-comments``
let ``e-scalar`` = RGP (System.String.Empty, [])
let ``e-node`` = ``e-scalar``

let ``b-as-line-feed`` = ``b-break``
let ``s-block-line-prefix`` ps = ``s-indent(n)`` ps
let ``s-line-prefix block-in`` ps = ``s-block-line-prefix`` ps
let ``s-line-prefix flow-in`` ps = ``s-flow-line-prefix`` ps
let ``l-empty block-in`` ps = ((``s-line-prefix block-in`` ps) ||| (``s-indent(<n)`` ps)) + ``b-as-line-feed``
let ``l-empty flow-in`` ps = ((``s-line-prefix flow-in`` ps) ||| (``s-indent(<n)`` ps)) + ``b-as-line-feed``
let ``s-nb-folded-text`` ps = (``s-indent(n)`` ps) + ZOM(``nb-char``)
let ``b-l-trimmed block-in`` ps = ``b-non-content`` + OOM(``l-empty block-in`` ps)
let ``b-l-trimmed flow-in`` ps = ``b-non-content`` + OOM(``l-empty flow-in`` ps)
let ``b-as-space`` = ``b-break``
let ``b-l-folded block-in`` ps = (``b-l-trimmed block-in`` ps) ||| ``b-as-space``
let ``b-l-folded flow-in`` ps = (``b-l-trimmed flow-in`` ps) ||| ``b-as-space``
let ``l-nb-folded-lines`` ps = (``s-nb-folded-text`` ps) + ZOM((``b-l-folded block-in`` ps) + ``s-nb-folded-text`` ps)
let ``s-nb-spaced-text`` ps = (``s-indent(n)`` ps) + ``s-white`` + ZOM(``nb-char``)
let ``b-l-spaced`` ps = ``b-as-line-feed`` + ZOM(``l-empty block-in`` ps)
let ``l-nb-spaced-lines`` ps = (``s-nb-spaced-text`` ps) + ZOM((``b-l-spaced``ps) + (``s-nb-spaced-text`` ps))
let ``l-nb-same-lines`` ps = ZOM(``l-empty block-in`` ps) + ((``l-nb-folded-lines`` ps) ||| (``l-nb-spaced-lines`` ps))
let ``l-nb-diff-lines`` ps = (``l-nb-same-lines`` ps) + ZOM(``b-as-line-feed`` + (``l-nb-same-lines`` ps))
let ``b-chomped-last`` ps = ``b-as-line-feed`` ||| RGP("\\z", [Token.EOF])
let ``l-trail-comments`` ps = (``s-indent(<n)`` ps) + ``c-nb-comment-text`` + ``b-comment`` + ZOM(``l-comment``)
let ``l-strip-empty`` ps = ZOM((``s-indent(<=n)`` ps) + ``b-non-content``) + OPT(``l-trail-comments`` ps)
let ``l-chomped-empty`` ps = ``l-strip-empty`` ps
let ``l-folded-content`` ps = GRP(OPT((``l-nb-diff-lines`` ps) + (``b-chomped-last`` ps))) + (``l-chomped-empty`` ps)


let ``c-indicator`` = 
    RGO  (
        "\-\?:,\[\]\{\}#&\*!;>\'\"%@`", 
        [ 
        Token.``t-hyphen``; Token.``t-questionmark``; Token.``t-colon``
        Token.``t-comma``; Token.``t-square-bracket-start``; Token.``t-square-bracket-end``
        Token.``t-curly-bracket-start``; Token.``t-curly-bracket-end``; Token.``t-hash``
        Token.``t-ampersand``; Token.``t-asterisk``; Token.``t-quotationmark``; Token.``t-pipe``
        Token.``t-gt``; Token.``t-single-quote``; Token.``t-double-quote``
        Token.``t-percent``; Token.``t-commat``;Token.``t-tick``
        ])

let ``c-sequence-entry`` = RGP ("-", [Token.``t-hyphen``])
let ``c-mapping-key`` = RGP ("\\?", [Token.``t-questionmark``])
let ``c-mapping-value`` = RGP (":", [Token.``t-colon``])
let ``ns-plain-safe-block-key`` ps = ``ns-char``
let ``c-comment`` = RGP ("#", [Token.``t-hash``])
let ``ns-plain-char`` ps = (``ns-char`` + ``c-comment``) ||| ((``ns-plain-safe-block-key`` ps) - (RGO (":#", [Token.``t-colon``; Token.``t-hash``]))) ||| (``c-mapping-value`` + (``ns-plain-safe-block-key`` ps))
let ``nb-ns-plain-in-line`` ps = ZOM(ZOM(``s-white``) + (``ns-plain-char`` ps))
let ``ns-plain-first`` ps = (``ns-char`` - ``c-indicator``) ||| (``c-mapping-key`` ||| ``c-mapping-value`` ||| ``c-sequence-entry``) + (``ns-plain-safe-block-key`` ps)
let ``ns-plain-one-line`` ps = (``ns-plain-first`` ps) + (``nb-ns-plain-in-line`` ps)

let ``c-single-quote`` = RGP ("\'", [Token.``t-single-quote``])
let ``c-quoted-quote`` = ``c-single-quote`` + ``c-single-quote``
let ``ns-single-char`` = // ``nb-single-char`` - ``s-white``
    ``c-quoted-quote`` ||| (``nb-json`` - ``c-single-quote`` - ``s-white``)
let ``nb-ns-single-in-line`` = ZOM(ZOM(``s-white``) + ``ns-single-char``)
let ``s-flow-folded`` ps =
        OPT(``s-separate-in-line``) + (``b-l-folded flow-in`` ps) + (``s-flow-line-prefix`` ps)

let ``s-single-next-line`` ps = 
        (ZOM((``s-flow-folded`` ps) + ``ns-single-char`` + ``nb-ns-single-in-line``) + (``s-flow-folded`` ps)) |||
        (OOM((``s-flow-folded`` ps) + ``ns-single-char`` + ``nb-ns-single-in-line``) + ZOM(``s-white``))
let ``nb-single-multi-line`` ps = ``nb-ns-single-in-line`` + ((``s-single-next-line`` ps) ||| ZOM(``s-white``))

[<Test>]
let ``Parse Group and Once or More - should match``() =
    let icp = GRP(ZOM(RGP (``s-space``, [Token.``t-space``]))) + OOM(``ns-char``)
    let rgxst = icp |> CreatePushParser

    let yaml = "- value"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "-")
    mr.GroupsResults |> List.length |> shouldEqual 1
    mr.GroupsResults.Head.Match |> stripTokenData |> shouldEqual (expected "")

    let yaml = " value"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected " value")


[<Test>]
let ``Parse s-separate - should match``() =
    let rgx = (``s-l-comments`` + (``s-flow-line-prefix`` 0)) ||| ``s-separate-in-line``
    let rgxst = rgx |> CreatePushParser

    let yaml = "- value"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "")


[<Test>]
let ``Parse s-l-comments - should match``() =

    let rgx = ``s-l-comments``
    let rgxst = rgx |> CreatePushParser

    let yaml = " \n  # C\n"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    streamReader.Position <- 1
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "\n  # C\n")

[<Test>]
let ``Parse l-document-prefix - should match``() =

    let rgx = ``l-document-prefix``
    let rgxst = rgx |> CreatePushParser

    let yaml = "\n"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "\n")


[<Test>]
let ``Parse s-l-comments - should not match``() =

    let rgx = ``s-l-comments``
    let rgxst = rgx |> CreatePushParser

    let yaml = "- M"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    streamReader.Position <- 1
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual false
    mr.FullMatch |> shouldEqual []


[<Test>]
let ``Parse s-l-comments oldway - should not match``() =
    let rgx = ``s-l-comments``

    let yaml = "- M"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    streamReader.Position <- 1
    let (mr,pr) = AssesInputPostParseCondition (fun _ -> true)  streamReader rgx
    mr |> shouldEqual false
    pr.Match |> shouldEqual []

    //pr.Match 
    //|>  List.map(fun i -> i.Source)
    //|>  List.fold(fun (sb:StringBuilder) (i:string) -> sb.Append(i)) (new StringBuilder())
    //|>  fun sb -> sb.ToString()
    //|>  shouldEqual (yaml.Replace("\r", ""))





[<Test>]
let ``Parse s-indent(2) - should not match``() =
    let rgx = ``s-indent(n)`` 2
    let rgxst = rgx |> CreatePushParser

    let yaml = "r"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual false
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "")


[<Test>]
let ``Parse s-indent(0) - should match``() =
    let rgx = ``s-indent(n)`` 0
    let rgxst = rgx |> CreatePushParser

    let yaml = "- r"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "")


[<Test>]
let ``Parse s-separate-lines - should match``() =
    let rgx = ``s-separate-lines`` 1
    let rgxst = rgx |> CreatePushParser

    let yaml = ":    # Comment
        # lines
  value"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    streamReader.Position <- 1
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "    # Comment\n        # lines\n  ")



[<Test>]
let ``Parse ns-plain - should match``() =
    let rgx = ``ns-plain-one-line`` 2
    let rgxst = rgx |> CreatePushParser

    let yaml = "Mark McGwire's"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "Mark McGwire's")

[<Test>]
let ``Parse nb-single-multi-line - should match``() =
    let rgx = ``c-single-quote`` + GRP(``nb-single-multi-line`` 1) + ``c-single-quote``
    let rgxst = rgx |> CreatePushParser

    let yaml = "'text'"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "'text'")


[<Test>]
let ``Parse e-node - should match``() =

    let rgx = ``e-node``
    let rgxst = rgx |> CreatePushParser

    let yaml = ""
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected "")


[<Test>]
let ``Parse l-folded-content - should match``() =

    let rgx = ``l-folded-content`` 1
    let rgxst = rgx |> CreatePushParser

    let yaml = "  Mark McGwire's
  year was crippled
  by a knee injury."
  //  let yaml = "  Ma
  //ya
  //ba"

    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let mr = MatchRegexState streamReader rgxst
    mr.IsMatch |> shouldEqual true
    mr.FullMatch |> stripTokenData  |> shouldEqual (expected (yaml.Replace("\r", "")))


[<Test>]
let ``Parse l-folded-content oldway - should be an example``() =
    let rgx = ``l-folded-content`` 1

    let yaml = "  Mark McGwire's
  year was crippled
  by a knee injury."
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (mr,pr) = AssesInputPostParseCondition (fun _ -> true)  streamReader rgx
    mr |> shouldEqual true
    //pr.Match |> shouldEqual []

    pr.Match 
    |>  List.map(fun i -> i.Source)
    |>  List.fold(fun (sb:StringBuilder) (i:string) -> sb.Append(i)) (new StringBuilder())
    |>  fun sb -> sb.ToString()
    |>  shouldEqual (yaml.Replace("\r", ""))
