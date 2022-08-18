using Nfw.Linux.FrameBuffer;

string fbDevice = "/dev/fb0";

if (args.Count() > 0) {
    fbDevice = args[0];
}

try {
    using(RawFrameBuffer fb = new RawFrameBuffer(fbDevice)) {        
        Console.WriteLine($"Display Device => {fb.Device} Name => {fb.Id} Width => {fb.PixelWidth} Height => {fb.PixelHeight} Bpp => {fb.PixelDepth}");
        Console.WriteLine("Clearing display...");
        fb.Clear();
        Console.WriteLine("Writing test pattern...");
        
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
} catch(Exception ex) {
    Console.WriteLine($"Error during use of fb => {fbDevice}: {ex}");
}
