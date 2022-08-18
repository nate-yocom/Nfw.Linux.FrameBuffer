using Nfw.Linux.FrameBuffer;

string fbDevice = "/dev/fb0";

if (args.Count() > 0) {
    fbDevice = args[0];
}

try {
    using(RawFrameBuffer fb = new RawFrameBuffer(fbDevice)) {        
        Console.WriteLine($"Display Device => {fb.Device} Name => {fb.Id} Width => {fb.PixelWidth} Height => {fb.PixelHeight} Bpp => {fb.PixelDepth}");
    }
} catch(Exception ex) {
    Console.WriteLine($"Unable to read framebuffer info for {fbDevice}: {ex}");
}
