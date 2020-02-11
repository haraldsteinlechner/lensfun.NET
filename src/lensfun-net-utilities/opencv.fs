namespace lensfunNet

open System
open lensfunNet
open OpenCvSharp

module OpenCV = 


    let remap (remap : float32[])  (img : Mat)  = 
        let size = img.Size()
        let remapMat = new Mat(size.Height, size.Width, MatType.CV_32FC2, remap)

        let outimg = new Mat(size, img.Type())
        let map = InputArray.Create(remapMat)
        Cv2.Remap(InputArray.Create img, OutputArray.Create outimg,map,InputArray.Create(new Mat()), InterpolationFlags.Lanczos4)
        outimg

