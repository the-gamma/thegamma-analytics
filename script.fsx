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

let [<Literal>] turingSample = __SOURCE_DIRECTORY__ + "/samples/turing.json"
let turingRoot = __SOURCE_DIRECTORY__ + "/logs/turing"
type Turing = JsonProvider<turingSample>

let turingJson =
  [ for f in IO.Directory.GetFiles(turingRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let turing = Turing.Parse("[" + turingJson + "]")

turing
|> Seq.iter (fun e -> printfn "%O" (e.Time.ToString("d") + " " + e.Time.ToString("t"), e.Category, e.Event, e.Element, e.Url))


