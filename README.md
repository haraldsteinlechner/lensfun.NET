# lensfun.NET

lightweigt wrapper for the lensfun library: https://lensfun.github.io/

```
let lf = LensFun("./db_files")
let ps = Exif.fileToParams @"path to image"
let modifier = lf.CreateModifier ps
let remapArray = modifier.ComputeMap(width,height)
// use opencv or whatever to remap the images....
```