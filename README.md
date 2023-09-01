# Nfw.Linux.FrameBuffer

An easy to use library for interacting with the Linux framebuffer interface (/dev/fbX).

## NuGet

```dotnet add package Nfw.Linux.FrameBuffer```

## Samples: Get Info From Display

This small snippet will open the provided device and use ioctls to discover its identifier and display parameters:

```csharp
using(RawFrameBuffer fb = new RawFrameBuffer("/dev/fb0")) {
    Console.WriteLine($"Display Device => {fb.Device} Name => {fb.Id} Width => {fb.PixelWidth} Height => {fb.PixelHeight} Bpp => {fb.PixelDepth}");
}
```

## Samples: Write Pixels

This snippet shows how you can write raw pixel data (on a 16bpp screen this shows as Black and Red hatch):

```csharp
using(RawFrameBuffer fb = new RawFrameBuffer("/dev/fb0")) {                
    // For each pixel, flop between 0x00 and 0x80
    byte[] data = new byte[fb.PixelWidth * fb.PixelHeight * (fb.PixelDepth / 8)];
    byte flopMe = 0x00;
    for (int x = 0; x < data.Length; x += (fb.PixelDepth / 8)) {
        for (int pixelByte = 0; pixelByte < (fb.PixelDepth / 8); pixelByte++) {
            data[x + pixelByte] = flopMe;                    
        }
        flopMe = (flopMe == 0x00) ? (byte) 0x80 : (byte) 0x00;
    }
    fb.WriteRaw(data);    
}
```

## Samples: Displaying an Image

This snippet uses the fantastic [ImageSharp](https://docs.sixlabors.com/articles/imagesharp/index.html?tabs=tabid-1) library to load an image, which must be in the same bitdepth as the framebuffer, then write it to the framebuffer:

```csharp
using(RawFrameBuffer fb = new RawFrameBuffer("/dev/fb0")) {                
    byte[] pixelBytes = new byte[fb.PixelWidth * fb.PixelHeight * (fb.PixelDepth / 8)];

    // Load from source
    using(Image loadedImage = Image.Load(filename)) {
        // Resize to fit screen
        loadedImage.Mutate(x => {            
            x.Resize(fb.PixelWidth, fb.PixelHeight);            
        });

        // Write the raw pixel data into a buffer
        loadedImage.CopyPixelDataTo(pixelBytes);
        fb.WriteRaw(pixelBytes);
    }
}
```

More useful is converting from whatever the image format is on disk, to the framebuffer's format, see the [DisplayImage](https://github.com/nate-yocom/Nfw.Linux.FrameBuffer/tree/main/samples/DisplayImage) Sample - which uses [ImageSharp](https://docs.sixlabors.com/articles/imagesharp/index.html?tabs=tabid-1) AND convert the image to the same pixel format as the display before writing the pixel data itself to the framebuffer. 

## Notes

- The RawFrameBuffer constructor can optionally be told NOT to probe via ioctl on construction, instead post-construction you must call ```RefreshDeviceInfo()``` before you can render anything.
- The RawFrameBuffer constructor can also be optionally provided an ```ILogger```, perhaps via IoC, for logging/diagnostics.
- In the case where your FB device can be mirrored, I suggest writing to the fastest (i.e. /dev/fb0 is often HW accelerated) then mirroring via something like the [raspi2fb](https://github.com/AndrewFromMelbourne/raspi2fb) tool to effectively 'double buffer'.


## References
- https://en.wikipedia.org/wiki/Linux_framebuffer
- https://www.kernel.org/doc/Documentation/fb/api.txt

## Attribution
- https://github.com/nickdu088/FrameBuffer

## IMAGE LICENSE

The [Sample Image](https://github.com/nate-yocom/Nfw.Linux.FrameBuffer/blob/main/samples/DisplayImage/images/fall-leaf-keyocom.jpg) used is an original watercolor by the author's wife: Katie Yocom.  License for this file is [CC BY-NC-ND 4.0](https://creativecommons.org/licenses/by-nc-nd/4.0/).

## Changelog

- 1.0.3:
  - Blank() now defaults to FB_BLANK_NORMAL
  - PowerDown() was added for previous behavior
  - Also exposes a Blank(byte) which takes arbitrary ioctl value.