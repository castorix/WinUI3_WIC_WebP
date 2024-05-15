using Microsoft.UI.Xaml.Data;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;

namespace MMIO
{
    internal class MMIOTools
    {
        public const int MMSYSERR_NOERROR = 0;
        public const int MMIOERR_BASE = 256;
        public const int MMIOERR_FILENOTFOUND = (MMIOERR_BASE + 1);  /* file not found */
        public const int MMIOERR_OUTOFMEMORY = (MMIOERR_BASE + 2);  /* out of memory */
        public const int MMIOERR_CANNOTOPEN = (MMIOERR_BASE + 3);  /* cannot open */
        public const int MMIOERR_CANNOTCLOSE = (MMIOERR_BASE + 4);  /* cannot close */
        public const int MMIOERR_CANNOTREAD = (MMIOERR_BASE + 5);  /* cannot read */
        public const int MMIOERR_CANNOTWRITE = (MMIOERR_BASE + 6);  /* cannot write */
        public const int MMIOERR_CANNOTSEEK = (MMIOERR_BASE + 7);  /* cannot seek */
        public const int MMIOERR_CANNOTEXPAND = (MMIOERR_BASE + 8);  /* cannot expand file */
        public const int MMIOERR_CHUNKNOTFOUND = (MMIOERR_BASE + 9);  /* chunk not found */
        public const int MMIOERR_UNBUFFERED = (MMIOERR_BASE + 10); /*  */
        public const int MMIOERR_PATHNOTFOUND = (MMIOERR_BASE + 11); /* path incorrect */
        public const int MMIOERR_ACCESSDENIED = (MMIOERR_BASE + 12); /* file was protected */
        public const int MMIOERR_SHARINGVIOLATION = (MMIOERR_BASE + 13); /* file in use */
        public const int MMIOERR_NETWORKERROR = (MMIOERR_BASE + 14); /* network not responding */
        public const int MMIOERR_TOOMANYOPENFILES = (MMIOERR_BASE + 15); /* no more file handles  */
        public const int MMIOERR_INVALIDFILE = (MMIOERR_BASE + 16); /* default error file error */

        /* bit field masks */
        public const int MMIO_RWMODE = 0x00000003;      /* open file for reading/writing/both */
        public const int MMIO_SHAREMODE = 0x00000070;      /* file sharing mode number */

        /* constants for dwFlags field of MMIOINFO */
        public const int MMIO_CREATE = 0x00001000;      /* create new file (or truncate file) */
        public const int MMIO_PARSE = 0x00000100;      /* parse new file returning path */
        public const int MMIO_DELETE = 0x00000200;      /* create new file (or truncate file) */
        public const int MMIO_EXIST = 0x00004000;      /* checks for existence of file */
        public const int MMIO_ALLOCBUF = 0x00010000;      /* mmioOpen() should allocate a buffer */
        public const int MMIO_GETTEMP = 0x00020000;      /* mmioOpen() should retrieve temp name */

        public const int MMIO_DIRTY = 0x10000000;      /* I/O buffer is dirty */

        /* read/write mode numbers (bit field MMIO_RWMODE) */
        public const int MMIO_READ = 0x00000000;      /* open file for reading only */
        public const int MMIO_WRITE = 0x00000001;      /* open file for writing only */
        public const int MMIO_READWRITE = 0x00000002;      /* open file for reading and writing */

        /* share mode numbers (bit field MMIO_SHAREMODE) */
        public const int MMIO_COMPAT = 0x00000000;      /* compatibility mode */
        public const int MMIO_EXCLUSIVE = 0x00000010;      /* exclusive-access mode */
        public const int MMIO_DENYWRITE = 0x00000020;      /* deny writing to other processes */
        public const int MMIO_DENYREAD = 0x00000030;      /* deny reading to other processes */
        public const int MMIO_DENYNONE = 0x00000040;      /* deny nothing to other processes */

        /* various MMIO flags */
        public const int MMIO_FHOPEN = 0x0010;  /* mmioClose: keep file handle open */
        public const int MMIO_EMPTYBUF = 0x0010;  /* mmioFlush: empty the I/O buffer */
        public const int MMIO_TOUPPER = 0x0010;  /* mmioStringToFOURCC: to u-case */
        public const int MMIO_INSTALLPROC = 0x00010000;  /* mmioInstallIOProc: install MMIOProc */
        public const int MMIO_GLOBALPROC = 0x10000000;  /* mmioInstallIOProc: install globally */
        public const int MMIO_REMOVEPROC = 0x00020000;  /* mmioInstallIOProc: remove MMIOProc */
        public const int MMIO_UNICODEPROC = 0x01000000;  /* mmioInstallIOProc: Unicode MMIOProc */
        public const int MMIO_FINDPROC = 0x00040000;  /* mmioInstallIOProc: find an MMIOProc */
        public const int MMIO_FINDCHUNK = 0x0010;  /* mmioDescend: find a chunk by ID */
        public const int MMIO_FINDRIFF = 0x0020;  /* mmioDescend: find a LIST chunk */
        public const int MMIO_FINDLIST = 0x0040;  /* mmioDescend: find a RIFF chunk */
        public const int MMIO_CREATERIFF = 0x0020;  /* mmioCreateChunk: make a LIST chunk */
        public const int MMIO_CREATELIST = 0x0040;  /* mmioCreateChunk: make a RIFF chunk */

        /* message numbers for MMIOPROC I/O procedure functions */
        public const int MMIOM_READ = MMIO_READ;       /* read */
        public const int MMIOM_WRITE = MMIO_WRITE;       /* write */
        public const int MMIOM_SEEK = 2;       /* seek to a new position in file */
        public const int MMIOM_OPEN = 3;       /* open file */
        public const int MMIOM_CLOSE = 4;       /* close file */
        public const int MMIOM_WRITEFLUSH = 5;       /* write and flush */
        public const int MMIOM_RENAME = 6;       /* rename specified file */
        public const int MMIOM_USER = 0x8000;       /* beginning of user-defined messages */

        public const int SEEK_SET = 0;      /* seek to an absolute position */
        public const int SEEK_CUR = 1;      /* seek relative to current position */
        public const int SEEK_END = 2;      /* seek relative to end of file */

        /* other constants */
        public const int MMIO_DEFAULTBUFFER = 8192;   /* default buffer size */

        private static int MAKEFOURCC(char ch0, char ch1, char ch2, char ch3)
        {
            return ((int)(byte)(ch0) | ((byte)(ch1) << 8) | ((byte)(ch2) << 16) | ((byte)(ch3) << 24));
        }

        public static readonly int FOURCC_RIFF = MAKEFOURCC('R', 'I', 'F', 'F');
        public static readonly int FOURCC_AVI = MAKEFOURCC('A', 'V', 'I', ' ');
        public static readonly int FOURCC_LIST = MAKEFOURCC('L', 'I', 'S', 'T');
        public static readonly int FOURCC_WebP = MAKEFOURCC('W', 'E', 'B', 'P');
        public static readonly int FOURCC_VP8 = MAKEFOURCC('V', 'P', '8', ' ');
        public static readonly int FOURCC_VP8L = MAKEFOURCC('V', 'P', '8', 'L');
        public static readonly int FOURCC_VP8X = MAKEFOURCC('V', 'P', '8', 'X');
        public static readonly int FOURCC_ICCP = MAKEFOURCC('I', 'C', 'C', 'P');
        public static readonly int FOURCC_ANIM = MAKEFOURCC('A', 'N', 'I', 'M');
        public static readonly int FOURCC_ANMF = MAKEFOURCC('A', 'N', 'M', 'F');
        public static readonly int FOURCC_ALPH = MAKEFOURCC('A', 'L', 'P', 'H');
        public static readonly int FOURCC_XMP = MAKEFOURCC('X', 'M', 'P', ' ');
        public static readonly int FOURCC_EXIF = MAKEFOURCC('E', 'X', 'I', 'F');

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioStringToFOURCC([MarshalAs(UnmanagedType.LPWStr)] string sz, uint uFlags);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr mmioInstallIOProc(uint fccIOProc, ref MMIOPROC pIOProc, uint dwFlags);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr mmioOpen([In][MarshalAs(UnmanagedType.LPWStr)] string pszFileName, ref MMIOINFO pmmioinfo, uint fdwOpen);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr mmioOpen([In][MarshalAs(UnmanagedType.LPWStr)] string pszFileName, IntPtr pmmioinfo, uint fdwOpen);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioRename([In][MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In][MarshalAs(UnmanagedType.LPWStr)] string pszNewFileName, ref MMIOINFO pmmioinfo, uint fdwRename);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioClose(IntPtr hmmio, uint fuClose);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioRead(IntPtr hmmio, IntPtr pch, int cch);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioWrite(IntPtr hmmio, ref int pch, int cch);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioSeek(IntPtr hmmio, int lOffset, int iOrigin);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioGetInfo(IntPtr hmmio, ref MMIOINFO pmmioinfo, uint fuInfo);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioSetInfo(IntPtr hmmio, ref MMIOINFO pmmioinfo, uint fuInfo);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioSetBuffer(IntPtr hmmio, [MarshalAs(UnmanagedType.LPStr)] StringBuilder pchBuffer, int cchBuffer, uint fuBuffer);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioFlush(IntPtr hmmio, uint fuFlush);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioAdvance(IntPtr hmmio, ref MMIOINFO pmmioinfo, uint fuAdvance);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr mmioSendMessage(IntPtr hmmio, uint uMsg, IntPtr lParam1, IntPtr lParam2);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioDescend(IntPtr hmmio, ref MMCKINFO pmmcki, ref MMCKINFO pmmckiParent, uint fuDescend);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioDescend(IntPtr hmmio, ref MMCKINFO pmmcki, IntPtr pmmckiParent, uint fuDescend);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioAscend(IntPtr hmmio, ref MMCKINFO pmmcki, uint fuAscend);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint mmioCreateChunk(IntPtr hmmio, ref MMCKINFO pmmcki, uint fuCreate);

        // From "VP8 Data Format and Decoding Guide"

        /* Evaluates to a mask with n bits set */
        public static byte BITS_MASK(byte n)
        {
           return (byte)((1 << (n)) - 1);
        }

        /* Returns len bits, with the LSB at position bit */
        public static byte BITS_GET(uint val, byte bit, byte len)
        {
           return (byte) (((val) >> (bit)) & BITS_MASK(len));
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate int MMIOPROC([MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder lpmmioinfo, int uMsg, IntPtr lParam1, IntPtr lParam2);

    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MMIOINFO
    {
        public uint dwFlags;        /* general status flags */
        public uint fccIOProc;      /* pointer to I/O procedure */
        public MMIOPROC pIOProc;        /* pointer to I/O procedure */
        public uint wErrorRet;      /* place for error to be returned */
        public IntPtr htask;          /* alternate local task */

        /* fields maintained by MMIO functions during buffered I/O */
        public int cchBuffer;      /* size of I/O buffer (or 0L) */
        public string pchBuffer;      /* start of I/O buffer (or NULL) */
        public string pchNext;        /* pointer to next byte to read/write */
        public string pchEndRead;     /* pointer to last valid byte to read */
        public string pchEndWrite;    /* pointer to last byte to write */
        public int lBufOffset;     /* disk offset of start of buffer */

        /* fields maintained by I/O procedure */
        public int lDiskOffset;    /* disk offset of next read or write */
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U4)]
        public uint[] adwInfo; /* data specific to type of MMIOPROC */

        /* other fields maintained by MMIO */
        public uint dwReserved1;    /* reserved for MMIO use */
        public uint dwReserved2;    /* reserved for MMIO use */
        public IntPtr hmmio;        /* handle to open file */       
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct MMCKINFO
    {
        public uint ckid;           /* chunk ID */
        public uint cksize;         /* chunk size */
        public uint fccType;        /* form type or list type */
        public uint dwDataOffset;   /* offset of data portion of chunk */
        public uint dwFlags;        /* flags used by MMIO functions */
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct VP8X
    {
        public byte byteFlags;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteReserved;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteCanvasWidthMinusOne;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteCanvasHeightMinusOne;
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct VP8
    {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteFrameTag;
        public byte byte0;
        public byte byte1;
        public byte byte2;
        public UInt16 HorizontalScaleWidth;
        public UInt16 VerticalScaleHeight;
        //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I1)]
        //public byte[] byteHorizontalScaleWidth;
        //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I1)]
        //public byte[] byteVerticalScaleHeight;
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct VP8L
    {        
        public byte byteSignature;
        //public int data;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I1)]
        public byte[] byteData;
    }

    public enum VP8XFlags
    {
        ANIMATION_FLAG = 0x00000002,
        XMP_FLAG = 0x00000004,
        EXIF_FLAG = 0x00000008,
        ALPHA_FLAG = 0x00000010,
        ICCP_FLAG = 0x00000020,
        ALL_VALID_FLAGS = 0x0000003E
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct ANIM
    {
        public uint BackgroundColor;
        public Int16 LoopCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 1, CharSet = CharSet.Ansi)]
    public struct ANMF2
    {
        [FieldOffset(0)] public byte reserved1;
        [FieldOffset(0)] public byte reserved2;
        [FieldOffset(0)] public byte reserved3;
        [FieldOffset(0)] public byte reserved4;
        [FieldOffset(0)] public byte reserved5;
        [FieldOffset(0)] public byte reserved6;
        [FieldOffset(0)] public byte Blending;
        [FieldOffset(0)] public byte Disposal;
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct ANMF
    {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteFrameX;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteFrameY;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteFrameWidthMinusOne;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteFrameHeightMinusOne;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] byteFrameDuration;
        // Blending method(B): 1 bit
        //    0: Use alpha-blending
        //    1: Do not blend.
        // Disposal method(D): 1 bit
        //    0: Do not dispose.
        //    1: Dispose to the background color.
        public byte bd;
    }


}
