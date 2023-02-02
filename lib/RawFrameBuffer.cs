using Microsoft.Extensions.Logging;

using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Nfw.Linux.FrameBuffer {
    
    public class RawFrameBuffer : IDisposable {
        private ILogger? _logger;
        private MemoryMappedFile? _fbMmFile = null;
        private MemoryMappedViewStream? _fbStream = null;
        private object _mutex = new object();
        private bool _disposedValue = false;
        
        private const string DEFAULT_DISPLAY_DEVICE = "/dev/fb0";        
        
        public string Device { get; private set; }          // Device file/path
        public string? Id { get; private set; }             // Device name
        public int PixelWidth { get; private set; } = 0;    // Number of pixels wide
        public int PixelHeight { get; private set; } = 0;   // Number of pixels high
        public int PixelDepth { get; private set; } = 0;    // Bits per-pixel

        public RawFrameBuffer(string device, ILogger? logger, bool autoprobe) {
            Device = device;
            _logger = logger;

            if (autoprobe) {
                RefreshDeviceInfo();
            }
        }

        public RawFrameBuffer(string device, bool autoprobe) : this(device, null, autoprobe) {
        }

        public RawFrameBuffer(string device) : this(device, null, true) {
        }

        public RawFrameBuffer() : this(DEFAULT_DISPLAY_DEVICE, null, true) {
        }

        public void RefreshDeviceInfo() {
            using(var fileHandle = File.OpenHandle(Device, FileMode.Open, FileAccess.ReadWrite, FileShare.None, FileOptions.None, 0)) {
                fb_fix_screeninfo fixed_info = new fb_fix_screeninfo();
                fb_var_screeninfo variable_info = new fb_var_screeninfo();

                if(ioctl(fileHandle.DangerousGetHandle().ToInt32(), FBIOGET_FSCREENINFO, ref fixed_info) < 0) {
                    _logger?.LogError($"ProbeFrameBuffer ioctl({FBIOGET_FSCREENINFO}) error: {System.Runtime.InteropServices.Marshal.ReadInt32(__errno_location())}");
                } else {
                    Id = System.Text.ASCIIEncoding.ASCII.GetString(fixed_info.id).TrimEnd(new char[] { '\r', '\n', ' ', '\0' });
                    _logger?.LogDebug($"Display memory for {Id} starts at: {fixed_info.smem_start} length: {fixed_info.smem_len}");
                }

                if(ioctl(fileHandle.DangerousGetHandle().ToInt32(), FBIOGET_VSCREENINFO, ref variable_info) < 0) {
                    _logger?.LogError($"ProbeFrameBuffer ioctl({FBIOGET_VSCREENINFO}) error: {System.Runtime.InteropServices.Marshal.ReadInt32(__errno_location())}");
                } else {
                    _logger?.LogDebug($"Actual width => {variable_info.xres} height => {variable_info.yres} bpp => {variable_info.bits_per_pixel}");
                    PixelDepth = variable_info.bits_per_pixel;
                    PixelWidth = variable_info.xres;
                    PixelHeight = variable_info.yres;
                }
            }
        }

        public void Blank()
        {
            IoctlBlanking(FB_BLANK_POWERDOWN);
        }

        public void UnBlank()
        {
            IoctlBlanking(FB_BLANK_UNBLANK);            
        }

        private void IoctlBlanking(int arg)
        {
            EnsureOpenStream();
            if(ioctl(_fbMmFile!.SafeMemoryMappedFileHandle.DangerousGetHandle().ToInt32(), FBIOBLANK, arg) < 0) {
                _logger?.LogError($"Blanking ioctl({FBIOBLANK}) arg({arg}) error: {System.Runtime.InteropServices.Marshal.ReadInt32(__errno_location())}");
            }
        }

        public void Clear() {
            byte[] emptyData = new byte[PixelWidth * PixelHeight * (PixelDepth / 8)];
            WriteRaw(emptyData);
        }
        
        public void WriteRaw(byte[] item) {
            EnsureOpenStream();
            int len = (int) Math.Min(item.Length, _fbStream!.Length);
            _fbStream?.Seek(0, SeekOrigin.Begin);
            _logger?.LogTrace($"Writing to FB - Stream pos: {_fbStream?.Position} Length: {_fbStream?.Length} Buffer Size: {item.Length} Writing len: {len}");
            _fbStream?.Write(item, 0, len);
            _fbStream?.Flush();            
        }       
                        
        private void EnsureOpenStream() {
            try {
                lock (_mutex) {
                    if (_fbMmFile == null) {
                        _fbMmFile = MemoryMappedFile.CreateFromFile(Device, FileMode.Open, null, (PixelWidth * PixelHeight * (PixelDepth / 8)));
                        _fbStream = _fbMmFile.CreateViewStream();
                    }
                }  
            } catch(Exception ex) {
                _logger?.LogError($"Unable to ensure framebuffer stream: ${ex}");
                throw;
            }
        }
        
        // FB api from https://www.kernel.org/doc/Documentation/fb/api.txt
        //  mapped for the bits we need, ignoring trailing parts (with byte[] only) for now

        [StructLayout(LayoutKind.Sequential)]
        private struct fb_fix_screeninfo {
            // char[] 
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] 
            public byte[] id;

            // unsigned long 
            [MarshalAs(UnmanagedType.U4)] 
            public uint smem_start;

            // __u32
            [MarshalAs(UnmanagedType.U4)] 
            public uint smem_len;

            // __u32
            [MarshalAs(UnmanagedType.U4)] 
            public uint type;

            // Remaing bits we dont care about
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)] 
            public byte[] __remaining_bits;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct fb_var_screeninfo {
            public int xres;
            public int yres;
            public int xres_virtual;
            public int yres_virtual;
            public int xoffset;
            public int yoffset;
            public int bits_per_pixel;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 132)] 
            public byte[] __remaining_bits;
        };


        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int ioctl(int handle, uint request, ref fb_var_screeninfo capability);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int ioctl(int handle, uint request, ref fb_fix_screeninfo capability);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int ioctl(int handle, uint request, int arg);
        
        
        [DllImport("libc", EntryPoint = "__errno_location")]
        private static extern System.IntPtr __errno_location();

        private const int FBIOGET_FSCREENINFO = 0x4602;
        private const int FBIOGET_VSCREENINFO = 0x4600;
        private const int FBIOBLANK = 0x4611;
        private const int FB_BLANK_UNBLANK= 0x0;
        private const int FB_BLANK_POWERDOWN = 0x4;

                        
        ~RawFrameBuffer() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing) {            
            if (!_disposedValue) {
                if (disposing) {                    
                    if (_fbStream != null) {
                        _fbStream.Dispose();
                        _fbStream = null;
                    }

                    if (_fbMmFile != null) {
                        _fbMmFile.Dispose();
                        _fbMmFile = null;
                    }
                }
                _disposedValue = true;
            }            
        }
    }

}