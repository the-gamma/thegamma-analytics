﻿#load "config.fsx"
#load "packages/FsLab/FsLab.fsx"
#load "packages/FSharp.Azure.StorageTypeProvider/StorageTypeProvider.fsx"
open System
open FSharp.Data
open Microsoft.WindowsAzure.Storage

// ------------------------------------------------------------------------------------------------
// Azure connection
// ------------------------------------------------------------------------------------------------

// Sending sample request to the logging service
Http.RequestString
  ( "http://thegamma-logs.azurewebsites.net/log/test/", httpMethod="POST", 
    body=HttpRequestBody.TextRequest "testing..." )

// Read the logs from Azure storage
let getContainer key = 
  let account = CloudStorageAccount.Parse(Connection.TheGammaLogStorage)
  let client = account.CreateCloudBlobClient()
  client.GetContainerReference(key)

// List all log files
let logs = getContainer "turing"
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn " - %s" (Seq.last log.Uri.Segments) 

// List all log files & read their contents
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn "\n\n%s\n%s" (Seq.last log.Uri.Segments) (Seq.last log.Uri.Segments |> String.map (fun _ -> '-'))
  logs.GetAppendBlobReference(log.Uri.Segments |> Seq.last).DownloadText() |> printfn "%s"

// Download all files to a local folder with logs
let downloadAll key = 
  let root = __SOURCE_DIRECTORY__ + "/logs/" + key
  if not (IO.Directory.Exists root) then IO.Directory.CreateDirectory root |> ignore
  let logs = getContainer key
  for log in logs.GetDirectoryReference("").ListBlobs() do
    printfn " - %s" (Seq.last log.Uri.Segments) 
    let file = Seq.last log.Uri.Segments
    let localFile = root + "/" + file
    if not (IO.File.Exists localFile) then
      printfn "   ...downloading..."
      logs.GetBlobReference(file).DownloadToFile(localFile, IO.FileMode.Create)

downloadAll "olympics"
downloadAll "turing"
downloadAll "datavizstudy"

// ------------------------------------------------------------------------------------------------
// Logs analaysis for Olympics
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts

let [<Literal>] olympicsSample = __SOURCE_DIRECTORY__ + "/samples/olympics.json"
let olympicsRoot = __SOURCE_DIRECTORY__ + "/logs/olympics"
type Olympics = JsonProvider<olympicsSample>

let olympicsJson =
  [ for f in IO.Directory.GetFiles(olympicsRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let olympics = Olympics.Parse("[" + olympicsJson + "]")

olympics
|> Seq.filter (fun a -> a.Event.IsNone)
|> Seq.iter (fun e -> printfn "%O" e.Time.Date)

olympics
|> Seq.countBy (fun a -> defaultArg a.Event "?")
|> Chart.Column

olympics
|> Seq.countBy (fun d -> d.Time.Day)
|> Chart.Column

olympics
|> Seq.countBy (fun d -> d.Category + ", " + (defaultArg d.Event ""))
|> Seq.filter (fun (e, c) -> c > 200)
|> Chart.Column

let topUsers =
  olympics
  |> Seq.countBy (fun d -> d.User.ToString())
  |> Seq.filter (fun (u, c) -> c > 50)
  |> Seq.map fst |> set

olympics 
|> Seq.filter (fun e -> e.Event = Some "loaded" && topUsers.Contains (e.User.ToString()))
|> Seq.map (fun e -> e.Data.String, e.User)
|> Seq.distinct
|> Seq.iter (printfn "%O")

// ------------------------------------------------------------------------------------------------
// Logs analaysis for Turing
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts
open System.Windows.Forms

let play strings = Async.StartImmediate <| async {
  use f = new Form(TopMost=true, Visible=true, Width=1200, Height=800)
  use t = new Label(Dock=DockStyle.Fill, Font = new Drawing.Font("consolas", 16.f))
  f.Controls.Add(t)
  do! Async.Sleep(2000)
  let mutable i = 0
  let len = Seq.length strings
  for s in strings do
    t.Text <- s
    f.Text <- sprintf "%d/%d" i len
    i <- i + 1
    do! Async.Sleep(250) }
    
let [<Literal>] turingSample = __SOURCE_DIRECTORY__ + "/samples/turing.json"
let turingRoot = __SOURCE_DIRECTORY__ + "/logs/turing"
type Turing = JsonProvider<turingSample>

let turingJson =
  [ for f in IO.Directory.GetFiles(turingRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let turing = 
  Turing.Parse("[" + turingJson + "]")
  |> Array.filter (fun e -> e.Url.Contains("gamma.turing.ac.uk"))

turing
|> Seq.countBy (fun e -> e.Time.Month, e.Time.Day)
|> Seq.map (fun ((m,d), n) -> sprintf "%d/%d" d m, n)
|> Chart.Bar

turing
|> Seq.filter (fun e -> e.Event="completions")
|> Seq.countBy (fun e -> e.User)
|> Seq.sortBy snd
|> Seq.iter (printfn "%A")

turing
|> Seq.filter (fun e -> e.Event="completions")
|> Seq.map (fun e -> e.Data.Record.Value.Source.Value)
|> play

turing
|> Seq.countBy (fun e -> e.Category, e.Event)
|> Seq.sortByDescending snd
|> Seq.iter (fun ((c,e),n) -> printfn "(%d) %s - %s" n c e)

turing
|> Seq.filter (fun e -> e.Category = "source" && e.Event = "update")
|> Seq.map (fun e -> e.Data.String.Value)
|> Seq.countBy (fun s -> s.Contains "let data", s.Contains "topThreeSpendings", s.Contains "Top4SpendsForSocialProtection")

turing
|> Seq.filter (fun e -> e.Category = "source" && e.Event = "update")
|> Seq.map (fun e -> e.Data.String.Value)
|> Seq.filter (fun s -> not (s.Contains "let data"))

turing
|> Seq.filter (fun e -> e.Category = "interactive" && e.Event = "completed")
|> Seq.countBy (fun e -> e.Element)

turing
|> Seq.filter (fun e -> e.Event = "exception")

let realign actual guesses = 
  let guesses = dict guesses
  let mutable last = None
  [| for k, v in actual ->
        if guesses.ContainsKey k then last <- Some guesses.[k]; k, guesses.[k]
        elif last.IsSome then k, last.Value
        else k, v |]

let lightColors = seq {
  while true do
    for c1 in ["ff"; "dd"; "bb"] do
      for c2 in ["dd"; "ff"; "bb"] do
        for c3 in ["99"; "bb"; "dd"] do
          yield "#" + c1 + c2 + c3 }

let shuffle s = 
  let rnd = System.Random()
  s |> Seq.map (fun v -> rnd.Next(), v) |> Seq.sortBy fst |> Seq.map snd

let lineGuesses id = 
  let all = 
    turing
    |> Seq.filter (fun e -> e.Category = "interactive" && e.Event = "completed")
    |> Seq.map (fun e -> e.Element.Value, e.Data.Record) 
    |> Seq.filter (fun (e, v) -> e = id)
  let actual = 
    all |> Seq.map (fun (_, v) -> v.Value.Values |> Array.map (fun g -> int g.Numbers.[0], float g.Numbers.[1])) |> Seq.head
  let guesses = 
    all |> Seq.map (fun (_, v) -> v.Value.Guess |> Array.map (fun g -> int g.Numbers.[0], float g.Numbers.[1]) |> realign actual) 
  let colors = lightColors |> Seq.take (Seq.length guesses)
  Seq.append guesses [actual] 
  |> Chart.Line
  |> Chart.WithOptions(Options(colors=Array.append (Array.ofSeq colors) [| "black" |] ))

let barGuesses id = 
  let all = 
    turing
    |> Seq.filter (fun e -> e.Category = "interactive" && e.Event = "completed")
    |> Seq.map (fun e -> e.Element.Value, e.Data.Record)
    |> Seq.filter (fun (e, v) -> e = id)
  let actual = 
    all |> Seq.map (fun (_, v) -> v.Value.Values |> Array.map (fun g -> g.JsonValue.AsArray().[0].AsString(), g.JsonValue.AsArray().[1].AsFloat())) |> Seq.head 
  let guesses = 
    all |> Seq.map (fun (_, v) -> v.Value.Guess |> Array.map (fun g -> g.JsonValue.AsArray().[0].AsString(), g.JsonValue.AsArray().[1].AsFloat()) |> dict) 
  let colors = Seq.append ["black"] (lightColors |> Seq.take (Seq.length guesses) |> shuffle)
  Chart.Bar
    [ yield actual
      for g in guesses -> [| for k, _ in actual -> k, g.[k] |] ]
    |> Chart.WithOptions(Options(colors=Array.ofSeq colors ))


lineGuesses "thegamma-scienceAndTech-out"
lineGuesses "thegamma-greenGovernment-out"
barGuesses "thegamma-grossDomesticProduct-out"


// ------------------------------------------------------------------------------------------------
// Logs analaysis for Dataviz study
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts

let [<Literal>] datavizSample = __SOURCE_DIRECTORY__ + "/samples/dataviz.json"
let datavizRoot = __SOURCE_DIRECTORY__ + "/logs/datavizstudy"
type Dataviz = JsonProvider<datavizSample>

let datavizJson =
  [ for f in IO.Directory.GetFiles(datavizRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let dataviz = 
  Dataviz.Parse("[" + datavizJson + "]")
  |> Array.filter (fun e -> e.Url.Contains("dataviz-study.azurewebsites.net"))

// Unique Prolific IDs
dataviz
|> Seq.choose (fun e -> e.Data.Record)
|> Seq.choose (fun e -> e.Prolificid)
|> Seq.distinct
|> Seq.iter (printfn "%s")


let pids = 
  [ "596e1af7a09655000197d4bb"
    "593ab35d51244c00013dcb69"
    "58effc65c31d4d00015aa5b9"
    "5b2465d4007d870001c7926a"
    "5b0c8052444cef0001ca5b6b"
    "5a6c0147d5d4cb0001d659e4"
    "5b7341a7543d1c0001c80a26"
    "55b8d0bffdf99b0f2859fe73"
    "5b6cfa80a1fda800015fff52"
    "5b2c68517297750001c79f1c"
    "589f4b4b4d580c0001e0a155"
    "5b54b7622680970001a9a7c9"
    "56c90db0722239000cba5b8a"
    "5a91fdd96219a30001d2e82c"
    "59fd920f9b760100013a6412"
    "580a6d0d5773b50001aab1d4"
    "59ff47d47ecfc50001be0555"
    "5b33ce040670a50001a3a265"
    "5abc517c1667e40001d80d68"
    "5af5bcf9b4dfd600018fc68e" ]

let pid = Seq.head pids

type Answers =      
  { share1: int
    share2: int
    q1: string
    q2: string
    q3: string
    q4: string }

type Info = 
  { mode: string 
    time: float 
    answers: Answers }

let info = 
  [ for pid in pids do
    printfn "\n%s" pid
    if pid <> "5b2465d4007d870001c7926a" && pid <> "5b7341a7543d1c0001c80a26" then
      let info = 
        dataviz |> Array.find (fun e -> 
          e.Category = "user" && e.Event = "info" && 
            e.Data.Record.IsSome && e.Data.Record.Value.Prolificid = Some pid)
      let user = info.User

      let events = 
        dataviz |> Array.filter (fun e -> e.User = user)
      let times = 
        events |> Array.map (fun e -> e.Time)
      let time = (Array.max times) - (Array.min times)

      let mode = 
        if not (events |> Seq.choose (fun e -> e.Data.Record) |> Seq.pick (fun r -> r.Visual)) then "image"
        elif not (events |> Seq.choose (fun e -> e.Data.Record) |> Seq.pick (fun r -> r.Interactive)) then "chart"
        else "interactive"

      let answers = 
        events |> Seq.choose (fun e -> e.Data.Record) |> Seq.pick (fun r ->
          match r.Question1, r.Question2, r.Question3, r.Question4, r.Question5, r.Share1, r.Share2 with
          | Some q1, Some q2a, Some q2b, Some q3, Some q4, Some s1, Some s2 ->
              Some { share1=s1; share2=s2; q1=q1; q2=q2a+" "+q2b; q3=q3; q4=q4 }
          | _ -> None)

      printfn "  events: %d" (events |> Seq.length)
      printfn "  time: %.2f" (time.TotalMinutes)
      printfn "  mode: %s" mode 
      yield { mode=mode; time=time.TotalMinutes; answers=answers } ]

let parseNumber (n:string) =
  let i = n |> String.filter Char.IsNumber |> int64
  if i < 10000L then float i else float (i / 1000000000000L)

// TIME: How long it took people to complete?
 
let modes = ["interactive";"image";"chart"]
let data1 = 
  [ for mode in modes -> 
    [ for i, v in Seq.indexed info do
        if v.mode = mode then yield i, v.time ] ]
Chart.Scatter(data1) |> Chart.WithLabels modes

// Q1: Average guess of exports from US to China

let data2 = 
  [ for mode in modes -> 
    [ for i, v in Seq.indexed info do
        let f = parseNumber v.answers.q1
        if f < 700.0 then
          if v.mode = mode then yield i, f ] ]
Chart.Scatter(data2) |> Chart.WithLabels modes

data2 |> Seq.map (Seq.averageBy snd) |> Seq.zip modes

// Q2: How many people got this right?

let data3 = 
  [ for mode in modes -> 
    [ for i, v in Seq.indexed info do
        if v.mode = mode then yield i, v.answers.q2 = "machines transportation" ] ]

data3 |> Seq.map (fun d -> 
  let right = Seq.filter snd d |> Seq.length
  float right / float (Seq.length d) ) |> Seq.zip modes

// SHARE: How likely are people to share?

let data4 = 
  [ [ for i, v in Seq.indexed info do
      yield i, v.answers.share2 ] ] @
  [ for mode in modes ->
    [ for i, v in Seq.indexed info do
        if v.mode = mode then yield i, v.answers.share1 ] ] 
let labels = "Share 2" :: (List.map (sprintf "Share 1 (%s)") modes)
Chart.Scatter(data4) 
|> Chart.WithLabels labels
|> Chart.WithOptions(Options(colors=[|"#e0e0e0"; "blue";"red";"orange"|]))



