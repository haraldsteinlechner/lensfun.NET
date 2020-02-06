open System
open lensfunNet
open OpenCvSharp
open ExifLib

module Exif =
    let fileToParams (filename : string) = 
        use reader = new ExifLib.ExifReader(filename)
        let ok,cam_maker = reader.GetTagValue<string>(ExifTags.Make)
        if not ok then failwith "could not get make"
        let ok,cam_model = reader.GetTagValue<string>(ExifTags.Model)
        if not ok then failwith "could not get model"
        let ok,focal_len = reader.GetTagValue<float>(ExifTags.FocalLength)
        if not ok then failwith "could not get focal length"
        let ok,aperture = reader.GetTagValue<float>(ExifTags.ApertureValue)
        if not ok then failwith "could not get aperture"
        let ok,lens_maker = reader.GetTagValue<string>(ExifTags.LensMake)
        let lens_maker = if ok then lens_maker else cam_maker
        let ok,lens_model = reader.GetTagValue<string>(ExifTags.LensModel)
        let distance = 10.0
        printfn "Focus distance not computed. using: %A" distance
        if not ok then failwith "could not get lens model"
        {
            cam_maker  = cam_maker
            cam_model  = cam_model
            focal_len  = focal_len
            aperture   = aperture
            lens_maker = lens_maker
            lens_model = lens_model
            distance   = 10.0
        }

let undistort (db : IntPtr) (remap : float32[]) (camera : Params) (img : Mat)  = 
    let size = img.Size()
    let remapMat = new Mat(size.Height, size.Width, MatType.CV_32FC2, remap)

    let outimg = new Mat(size, img.Type())
    let map = InputArray.Create(remapMat)
    Cv2.Remap(InputArray.Create img, OutputArray.Create outimg,map,InputArray.Create(new Mat()), InterpolationFlags.Lanczos4)
    outimg

let undistortJpg (filename : string) (undistored : string) =
    let db = LensFun.initLf "./db_files"
    let cameraParameters = Exif.fileToParams filename
    let img = Cv2.ImRead filename
    let width,height = img.Size(1), img.Size(0)
    let remap,_ = LensFun.createModifier db width height cameraParameters
    let outimg = undistort db remap cameraParameters img

    Cv2.ImWrite(undistored, outimg) |> printfn "ok: %A"


[<EntryPoint;STAThread>]
let main argv =
    
    0
 