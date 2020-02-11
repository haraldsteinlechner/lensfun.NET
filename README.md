all on nuget: https://www.nuget.org/packages?q=lensfunnet

# lensfun.NET

(incomplete/wip) lightweigt wrapper for the lensfun library: https://lensfun.github.io/

```
let lf = LensFun("./db_files")
let ps = Exif.fileToParams @"path to image"
let modifier = lf.CreateModifier ps
let remapArray = modifier.ComputeMap(width,height)
// use opencv or whatever to remap the images....
```

full undist example can be found here: https://github.com/haraldsteinlechner/lensfun.NET/blob/master/src/Example/Program.fs


# the tool

recursively converts images in a directory using lensfun lib (which is downloaded) and opencv.

```
>dotnet tool install --global lensfunNet-undist 
>lensfunnet-undist "C:\Users\hs\Pictures" "IMG_%d.JPG" "IMG_%d_undist.JPG"
C:\Users\hs\Pictures\IMG_6975.JPG -> C:\Users\hs\Pictures\IMG_6975_undist.JPG
```
