namespace lensfunNet

open ExifLib
open lensfunNet

module Exif =
    let fileToParams (filename : string) = 
        use reader = new ExifLib.ExifReader(filename)
        let ok,cam_maker = reader.GetTagValue<string>(ExifTags.Make)
        if not ok then failwith "could not get make"
        let ok,cam_model = reader.GetTagValue<string>(ExifTags.Model)
        let cam_model = if ok then cam_model else printfn "could not get lens model. leaving empty"; ""
        if not ok then failwith "could not get model"
        let ok,focal_len = reader.GetTagValue<float>(ExifTags.FocalLength)
        if not ok then failwith "could not get focal length"
        let ok,aperture = reader.GetTagValue<float>(ExifTags.ApertureValue)
        if not ok then failwith "could not get aperture"
        let ok,lens_maker = reader.GetTagValue<string>(ExifTags.LensMake)
        let lens_maker = if ok then lens_maker else cam_maker
        let ok,lens_model = reader.GetTagValue<string>(ExifTags.LensModel)
        let lens_model = if ok then lens_model else printfn "could not get lens model. leaving empty"; ""
        if not ok then printfn "could not get lens model for: %A" filename
        {
            cam_maker  = cam_maker
            cam_model  = cam_model
            focal_len  = focal_len
            aperture   = aperture
            lens_maker = lens_maker
            lens_model = lens_model
            distance   = 10.0
        }