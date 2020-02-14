open System
open System.IO
open lensfunNet
open OpenCvSharp
open System.Text.RegularExpressions
open System.Collections.Concurrent
open System.Threading


module MetaData =
    open LensFun
    open Chiron

    type Ext = Ext

    module Ext =
        let inline fromJsonDefaults (a: ^a, _: ^b) =
            ((^a or ^b or ^e) : (static member FromJson1: ^e * ^a ->    ^a Json)(Unchecked.defaultof<_>,a))
    
        let inline fromJson x =
            fst (fromJsonDefaults (Unchecked.defaultof<'a>, FromJsonDefaults) x)
    
        let inline toJsonDefaults (a: ^a, _: ^b) =
            ((^a or ^b or ^e) : (static member ToJson1: ^e * ^a -> unit Json)(Unchecked.defaultof<_>,a))
    
        let inline toJson (x: 'a) =
            snd (toJsonDefaults (x, ToJsonDefaults) (Object (Map.empty)))
        
    type Ext with
        static member ToJson1 (e : Ext, x : Params) = 
            json {
                do! Json.write "Make" x.cam_maker
                do! Json.write "Model" x.cam_model
                do! Json.write "FocalLength" x.focal_len
                do! Json.write "ApertureValue" x.aperture
                do! Json.write "LensMake" x.lens_maker
                do! Json.write "LensModel" x.lens_model
                do! Json.write "Distance" x.distance
            }
        static member FromJson1(e : Ext, _ : Params) =
            json {
                let! make = Json.read "Make"
                let! model = Json.read "Model"
                let! focal = Json.read "FocalLength"
                let! aperture = Json.read "ApertureValue"
                let! lensMake = Json.read "LensMake"
                let! lensModel = Json.read "LensModel"
                let! distance = Json.read "Distance"
                return 
                    {
                        cam_maker = make
                        cam_model = model
                        focal_len = focal
                        aperture = aperture
                        lens_maker = lensMake
                        lens_model = lensModel
                        distance = distance
                    }
            }
        static member ToJson1 (e : Ext, x : LensFun.LensInfo) = 
            json {
                do! Json.write "Maker" x.Maker
                do! Json.write "Model" x.Model
                do! Json.write "Type" x.LensType
                do! Json.write "MinFocal" x.MinFocal
                do! Json.write "MaxFocal" x.MaxFocal
                do! Json.write "MinAperture" x.MinAperture
                do! Json.write "MaxAperture" x.MaxAperture
                do! Json.write "CropFactor" x.CropFactor
                do! Json.write "CenterX" x.CenterX
                do! Json.write "CenterY" x.CenterY
                do! Json.write "Score" x.Score
            }
        static member ToJson1 (e : Ext, x : LensFun.CameraInfo) = 
            json {
                do! Json.write "Maker" x.Maker
                do! Json.write "Model" x.Model
                do! Json.write "Mount" x.Mount
            }
        static member FromJson1(e : Ext, x : LensFun.CameraInfo) = 
            json {
                let! make = Json.read "Maker"
                let! model = Json.read "Model"
                let! mount = Json.read "Mount"
                return  {
                    Maker = make
                    Model = model
                    Variant = ""
                    Mount = mount
                }
            }

        static member FromJson1 (e : Ext, _ : LensFun.LensInfo) = 
            json {
                let! maker = Json.read "Maker" 
                let! model = Json.read "Model"
                let! t = Json.read "Type" 
                let! minFocal = Json.read "MinFocal" 
                let! maxFocal = Json.read "MaxFocal"
                let! minAperture =  Json.read "MinAperture" 
                let! maxAperture = Json.read "MaxAperture" 
                let! cropFactor =  Json.read "CropFactor"
                let! centerX = Json.read "CenterX" 
                let! centerY = Json.read "CenterY" 
                let! score = Json.read "Score"
                return {
                    Maker      = maker
                    Model      = model
                    LensType  = t
                    MinFocal = minFocal
                    MaxFocal = maxFocal
                    MinAperture = minAperture
                    MaxAperture = maxAperture
                    CropFactor  = cropFactor
                    CenterX = centerX
                    CenterY = centerY
                    Score = score
                }
            }

        static member ToJson1 (e : Ext, x : LensFun.ImageInfo) = 
            json {
                do! Json.writeWith Ext.toJson<Params,Ext> "ExifData" x.imageParams
                do! Json.write "width" x.width
                do! Json.write "height" x.height
                do! Json.writeWith Ext.toJson<CameraInfo,Ext> "CameraInfo" x.cameraInfo
                do! Json.writeWith Ext.toJson<LensFun.LensInfo,Ext> "LensInfo" x.lensInfo
                //do! Json.write "remap" x.remap
            }
        static member FromJson1 (e : Ext, _ : LensFun.ImageInfo) = 
            json {
                let! exif = Json.readWith Ext.fromJson<Params,Ext> "ExifData" 
                let! width = Json.read "width"
                let! height = Json.read "height"
                let! cameraInfo = Json.readWith Ext.fromJson<LensFun.CameraInfo,Ext> "CameraInfo"
                let! lensLens = Json.readWith Ext.fromJson<LensFun.LensInfo,Ext> "LensInfo"
                //let! remap = Json.tryRead "remap"
                return {
                    imageParams = exif
                    width = width
                    height = height
                    lensInfo = lensLens
                    remap = None
                    cameraInfo = cameraInfo
                }
            }

    type MetaData = 
        {
            imageInfo : ImageInfo
        } with
            static member ToJson(x : MetaData) = 
                json {
                    do! Json.writeWith Ext.toJson<ImageInfo,Ext> "ImageInfo" x.imageInfo
                }
            static member FromJson(_ : MetaData) =
                json {
                    let! imageInfo = Json.readWith Ext.fromJson<ImageInfo,Ext> "ImageInfo"
                    return { imageInfo = imageInfo}
                }

    let toJson (i : MetaData) = 
        i |> Json.serialize |> Json.formatWith JsonFormattingOptions.Pretty

    let fromJson (s : string) : Choice<MetaData,string> = 
        match s |> Json.tryParse with 
        | Choice1Of2 ok -> ok |> Json.deserialize |> Choice1Of2
        | Choice2Of2 err -> Choice2Of2 err

module Tool = 

    let undistort (remap : float32[])  (img : Mat)  = 
        let size = img.Size()
        let remapMat = new Mat(size.Height, size.Width, MatType.CV_32FC2, remap)

        let outimg = new Mat(size, img.Type())
        let map = InputArray.Create(remapMat)
        Cv2.Remap(InputArray.Create img, OutputArray.Create outimg,map,InputArray.Create(new Mat()), InterpolationFlags.Lanczos4)
        outimg

    let undistortJpg (db : IntPtr) (filename : string) (undistored : string) =
        let cameraParameters = Exif.fileToParams filename
        let img = Cv2.ImRead filename
        let width,height = img.Size(1), img.Size(0)
        let lensInfo = LensFun.createModifier db cameraParameters width height 
        let outimg = undistort lensInfo.remap.Value img

        Cv2.ImWrite(undistored, outimg) |> printfn "ok: %A"


    let run (argv : array<string>) =
        let app = 
            Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)
        let dbD = Path.Combine(app,"./db_files")
        LensFun.downloadLensFunFromWheels app dbD
        let db = LensFun.initLf dbD

        let mappers = ConcurrentDictionary<_,LensFun.ImageInfo>()

        let q = new BlockingCollection<_>(20)
        let w = new BlockingCollection<_>(20)
        let distorter () = 
            for (sourceFile, img, info : LensFun.ImageInfo, targetFile) in q.GetConsumingEnumerable() do 
                try 
                    let undistorted = undistort info.remap.Value img
                    w.Add((sourceFile, info, undistorted, targetFile))
                with e -> 
                    printfn "failed to convert: %s" sourceFile
            

        let writer () =
            for (sourceFile, info, img, targetFile) in w.GetConsumingEnumerable() do 
                try
                    if Cv2.ImWrite(targetFile,img) then
                        let json = MetaData.toJson { imageInfo = info }
                        let info = Path.ChangeExtension(targetFile,"json")
                        File.WriteAllText(info,json)
                        let a = MetaData.fromJson json
                        printfn "%s -> %s" sourceFile targetFile
                    else printfn "failed to ImWrite: %s" sourceFile
                with e -> 
                     printfn "failed to write: %s" sourceFile

        let reader (files : seq<string*string>) =
            for (s,d) in files do   
                try
                    let img = Cv2.ImRead s
                    let p = Exif.fileToParams s
                    let key = (img.Size(),p)
                    let mapper  = 
                        mappers.GetOrAdd(key, fun v -> 
                            let width,height = img.Size(1), img.Size(0)
                            let lensInfo = LensFun.createModifier db p width height 
                            lensInfo
                        )
                    q.Add((s, img, mapper, d))
                with e -> 
                    printfn "%s failed with: %A" s e
            q.CompleteAdding()
                           

                
        match argv with
            | [|dir;pat;tPat|] -> 
                let start f =
                    async {
                        do! Async.SwitchToNewThread()
                        return f()
                    } 


                let writer = Async.StartAsTask <| start writer
                let runners = 
                    async {
                        let! dists = Async.Parallel [for i in 0 .. 8 do yield distorter |> start] 
                        w.CompleteAdding()
                    } |> Async.StartAsTask

                let r = 
                    if pat.Contains("%d") then
                        pat.Replace("%d","(?<id>[0-9]*)") + "$"
                    else pat
                let regex = Regex r
                let files = 
                    seq { 
                        for f in Directory.EnumerateFiles(dir,"*", SearchOption.AllDirectories) do
                            let file = Path.GetFileName f
                            let m = regex.Match(file)
                            if m.Success then
                                let n = 
                                    if m.Groups.ContainsKey "id" && tPat.Contains("%d") then 
                                        tPat.Replace("%d",m.Groups.["id"].Value)
                                    else tPat
                                let dir = Path.GetDirectoryName f
                                yield f, Path.Combine(dir,n)
                    } |> Seq.toList

                reader files 
                runners.Wait()
                writer.Wait()
                0
            | _ -> 
                printfn "usage: tool path searchPattern\n e.g. tool . \"IMG_%%d.JPG\" \"undist_%%d.jpg0\""
                1


 

[<EntryPoint>]
let main argv =
    Tool.run argv

