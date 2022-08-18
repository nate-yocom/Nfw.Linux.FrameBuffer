using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Nfw.Linux.FrameBuffer;

string fbDevice = "/dev/fb0";
string? imageFile = null;

if (args.Count() == 0) {
    Console.WriteLine($"Usage: DisplayImage <path to image> [path to framebuffer device]");
    return;
}

imageFile = args[0];

if (args.Count() > 1) {
    fbDevice = args[1];
}

try {
    using(RawFrameBuffer fb = new RawFrameBuffer(fbDevice)) {        
        Console.WriteLine($"Display Device => {fb.Device} Name => {fb.Id} Width => {fb.PixelWidth} Height => {fb.PixelHeight} Bpp => {fb.PixelDepth}");
        Console.WriteLine("Clearing display...");
        fb.Clear();

        Console.WriteLine($"Loading image from {imageFile}...");        
        byte[] pixelBytes;
        switch(fb.PixelDepth) {
            case 8:
                pixelBytes = LoadImageBytes<L8>(imageFile, fb);
                break;
            case 15:
            case 16:
                pixelBytes = LoadImageBytes<Bgr565>(imageFile, fb);
                break;
            case 32:
                pixelBytes = LoadImageBytes<Rgba32>(imageFile, fb);
                break;
            default:
                Console.WriteLine($"Unsure which ImageSharp pixel format to use for {fb.PixelDepth}bpp");
                return;                
        }
        
        Console.WriteLine("Writing image to framebuffer...");
        fb.WriteRaw(pixelBytes);
    }
} catch(Exception ex) {
    Console.WriteLine($"Error during use of fb => {fbDevice}: {ex}");
}

static byte[] LoadImageBytes<T>(string filename, RawFrameBuffer fb) where T : unmanaged, IPixel<T> {
    byte[] pixelBytes = new byte[fb.PixelWidth * fb.PixelHeight * (fb.PixelDepth / 8)];

    // Load from source
    using(Image loadedImage = Image.Load(filename)) {
        // Resize to fit screen
        loadedImage.Mutate(x => {            
            x.Resize(fb.PixelWidth, fb.PixelHeight);            
        });

        // Convert to screen's bit depth
        using(Image<T> converted = loadedImage.CloneAs<T>()) {
            // And get the raw pixel buffer
            converted.CopyPixelDataTo(pixelBytes);
        }
    }

    return pixelBytes;
}
