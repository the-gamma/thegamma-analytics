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
  ( "http://thegamma-logs.azurewebsites.net/log/olympics/", httpMethod="POST", 
    body=HttpRequestBody.TextRequest "testing..." )

// Read the logs from Azure storage
let account = CloudStorageAccount.Parse(Connection.TheGammaLogStorage)
let client = account.CreateCloudBlobClient()
let logs = client.GetContainerReference("olympics")

// List all log files
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn " - %s" (Seq.last log.Uri.Segments) 

// List all log files & read their contents
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn "\n\n%s\n%s" (Seq.last log.Uri.Segments) (Seq.last log.Uri.Segments |> String.map (fun _ -> '-'))
  logs.GetAppendBlobReference(log.Uri.Segments |> Seq.last).DownloadText() |> printfn "%s"

// Download all files to a local folder with logs
let root = __SOURCE_DIRECTORY__ + "/logs"
if not (IO.Directory.Exists root) then IO.Directory.CreateDirectory root |> ignore
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn " - %s" (Seq.last log.Uri.Segments) 
  let file = Seq.last log.Uri.Segments
  let localFile = root + "/" + file
  if not (IO.File.Exists localFile) then
    printfn "   ...downloading..."
    logs.GetBlobReference(file).DownloadToFile(localFile, IO.FileMode.Create)

// ------------------------------------------------------------------------------------------------
// Logs analaysis 
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts

let [<Literal>] sampleLogs = __SOURCE_DIRECTORY__ + "/logs.json"
type Logs = JsonProvider<sampleLogs>

let json =
  [ for f in IO.Directory.GetFiles(root) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let all = Logs.Parse("[" + json + "]")

all
|> Seq.filter (fun a -> a.Event.IsNone)
|> Seq.iter (fun e -> printfn "%O" e.Time.Date)

all
|> Seq.countBy (fun a -> defaultArg a.Event "?")
|> Chart.Column

all
|> Seq.countBy (fun d -> d.Time.Day)
|> Chart.Column

all
|> Seq.countBy (fun d -> d.Category + ", " + (defaultArg d.Event ""))
|> Seq.filter (fun (e, c) -> c > 200)
|> Chart.Column

let topUsers =
  all
  |> Seq.countBy (fun d -> d.User.ToString())
  |> Seq.filter (fun (u, c) -> c > 50)
  |> Seq.map fst |> set

all 
|> Seq.filter (fun e -> e.Event = Some "loaded" && topUsers.Contains (e.User.ToString()))
|> Seq.map (fun e -> e.Data.String, e.User)
|> Seq.distinct
|> Seq.iter (printfn "%O")
