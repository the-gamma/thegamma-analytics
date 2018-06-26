#load "config.fsx"
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

dataviz
|> Seq.countBy (fun u -> u.User)

let events = 
  dataviz 
  |> Seq.filter (fun e -> e.User = Guid.Parse("871103a2-a5c4-49a0-9ec6-762c04e32157"))
  |> Seq.sortBy (fun e -> e.Time) 
  
for e in events do 
  printfn "[%s] %s (%s) - %A" e.Category e.Event (defaultArg e.Element "") e.Data