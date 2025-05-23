using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Buffers.Binary;
using Windows.Storage.Streams;
using System.Collections.ObjectModel;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

using GlobalStructures;
using static GlobalStructures.GlobalTools;
using Direct2D;
using WIC;
using static WIC.WICTools;
using DXGI;
using static DXGI.DXGITools;
using MMIO;
using static MMIO.MMIOTools;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

// https://developers.google.com/speed/WebP/docs/riff_container

namespace WinUI3_WIC_WebP
{
    public sealed class WebPControl : SwapChainPanel, IDisposable    
    {
        [ComImport, Guid("63aad0b8-7c24-40ff-85a8-640d944cc325"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISwapChainPanelNative
        {
            [PreserveSig]
            HRESULT SetSwapChain(IDXGISwapChain swapChain);
        }

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        private bool disposedValue;

        private bool m_bAnimation = false;
        private uint m_nCanvasWidth = 0;
        private uint m_nCanvasHeight = 0;
        private uint m_nFrameWidth = 0;
        private uint m_nFrameHeight = 0;
        private System.Drawing.Color m_BackgroundColor = System.Drawing.Color.Black;
        private uint m_nLoopCount = 0;
        private uint m_nCurrentLoop = 0;
        private double m_nFrameDuration = 90;
        private double m_nTotalDuration = 0;
        private uint m_nCurrentFrame = 0;
        private uint m_nNbFrames = 0;

        private ObservableCollection<Frame> listFrames = new ObservableCollection<Frame>();

        IntPtr m_hWndMain;

        ID2D1Factory1 m_pD2DFactory1 = null;
        IWICImagingFactory m_pWICImagingFactory = null;

        IntPtr m_pD3D11DevicePtr = IntPtr.Zero; // Released in CreateDeviceContext : not used
        ID3D11DeviceContext m_pD3D11DeviceContext = null; // Released in Clean : not used
        IDXGIDevice1 m_pDXGIDevice = null; // Released in Clean

        public ID2D1DeviceContext m_pD2DDeviceContext = null; // Released in Clean

        public IDXGISwapChain1 m_pDXGISwapChain1 = null;
        ID2D1Bitmap1 m_pD2DTargetBitmap = null;

        IWICBitmapDecoder m_pWICBitmapDecoder = null;
        ID2D1Bitmap m_pD2DBitmapFrame = null;       

        public WebPControl()
        {
            //this.SizeChanged += WebPControl_SizeChanged;
        }

        public void Init(IntPtr hWndMain, ID2D1Factory1 pD2DFactory1, IWICImagingFactory pWICImagingFactory)
        {
            m_hWndMain = hWndMain;
            m_pD2DFactory1 = pD2DFactory1;
            m_pWICImagingFactory = pWICImagingFactory;
            HRESULT hr = CreateDeviceContext();
            //hr = CreateDeviceResources();
            hr = CreateSwapChain(IntPtr.Zero);
            if (hr == HRESULT.S_OK)
            {
                hr = ConfigureSwapChain(m_hWndMain);
                ISwapChainPanelNative panelNative = WinRT.CastExtensions.As<ISwapChainPanelNative>(this);
                hr = panelNative.SetSwapChain(m_pDXGISwapChain1);
            }
            this.SizeChanged += WebPControl_SizeChanged;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object sender, object e)
        {
            if (stopWatch != null)
            {
                if (listFrames.Count >= 1)
                {
                    GetBitmapFromFrame(m_nCurrentFrame);
                    Render(m_nCurrentFrame);
                    var nElapsedTime = stopWatch.ElapsedMilliseconds;
                    double nDuration = listFrames.ElementAt((int)m_nCurrentFrame).Duration;
                    if (nElapsedTime >= nDuration)
                    {
                        m_nCurrentFrame++;
                        if (m_nCurrentFrame >= m_nNbFrames)
                        {
                            m_nCurrentFrame = 0;
                            m_nCurrentLoop += 1;
                        }
                        if (m_nLoopCount > 0 && m_nCurrentLoop >= m_nLoopCount)
                        {
                            stopWatch.Reset();
                            stopWatch.Stop();
                        }
                        else
                            stopWatch.Restart();
                    }
                }
            }
        }

        public bool LoadFile(string sFile, TextBlock tbWidth = null, TextBlock tbHeight = null, TextBlock tbAnimation = null)
        {  
            bool bFileOK = false;
            IntPtr hmmio;
            uint mmr;
            hmmio = mmioOpen(sFile, IntPtr.Zero, MMIO_READ);
            if (hmmio != IntPtr.Zero)
            {
                MMCKINFO mmckRiff = new MMCKINFO();
                mmckRiff.fccType = (uint)FOURCC_WebP;
                mmr = mmioDescend(hmmio, ref mmckRiff, IntPtr.Zero, MMIO_FINDRIFF);
                if (mmr == MMSYSERR_NOERROR)
                {
                    bFileOK = true;
                    m_bAnimation = false;
                    m_nCanvasWidth = 0;
                    m_nCanvasHeight = 0;
                    m_nFrameWidth = 0;
                    m_nFrameHeight = 0;
                    m_BackgroundColor = System.Drawing.Color.Black;
                    m_nLoopCount = 0;
                    m_nCurrentLoop = 0;
                    m_nFrameDuration = 90;
                    m_nTotalDuration = 0;
                    m_nCurrentFrame = 0;
                    m_nNbFrames = 0;
                    listFrames.Clear();

                    MMCKINFO mmck = new MMCKINFO();
                    while (mmioDescend(hmmio, ref mmck, ref mmckRiff, 0) == MMSYSERR_NOERROR)
                    {
                        if (mmck.ckid == FOURCC_VP8) // Lossy
                        {
                            VP8 vp8 = new VP8();
                            var pVP8 = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VP8)));
                            mmr = mmioRead(hmmio, pVP8, (int)Marshal.SizeOf(typeof(VP8)));
                            vp8 = (VP8)Marshal.PtrToStructure(pVP8, typeof(VP8));
                            Marshal.FreeHGlobal(pVP8);

                            uint rawData = (uint)(vp8.byteFrameTag[0] | (vp8.byteFrameTag[1] << 8) | (vp8.byteFrameTag[2] << 16));
                            var bKeyframe = BITS_GET(rawData, 0, 1) == 0 ? 1 : 0;
                            var nVersion = BITS_GET(rawData, 1, 2);
                            var bExperimental = BITS_GET(rawData, 3, 1);
                            var bShown = BITS_GET(rawData, 4, 1);
                            var nPart0Size = BITS_GET(rawData, 5, 19);

                            //rawData = (uint)(vp8.byteHorizontalScaleWidth[0] | (vp8.byteHorizontalScaleWidth[1] << 8)
                            //    | (vp8.byteVerticalScaleHeight[0] << 16) | (vp8.byteVerticalScaleHeight[1] << 24));
                            //var width = BITS_GET(rawData, 0, 14);
                            //var scale_w = BITS_GET(rawData, 14, 2);
                            //var height = BITS_GET(rawData, 16, 14);
                            //var scale_h = BITS_GET(rawData, 30, 2);

                            m_nFrameWidth = (uint)vp8.HorizontalScaleWidth & 0x3fff;
                            sbyte xScale = (sbyte)(vp8.HorizontalScaleWidth >> 14);

                            m_nFrameHeight = (uint)vp8.VerticalScaleHeight & 0x3fff;
                            sbyte yScale = (sbyte)(vp8.VerticalScaleHeight >> 14);
                            m_nCanvasWidth = m_nFrameWidth;
                            m_nCanvasHeight = m_nFrameHeight;
                            
                            if (listFrames.Count == 0)
                            {
                                bool bAlpha = true;
                                listFrames.Add(new Frame(0, 0, m_nCanvasWidth, m_nCanvasHeight, 90, (bAlpha ? 0 : 1), 1));
                            }
                            m_nTotalDuration += m_nFrameDuration;
                        }

                        if (mmck.ckid == FOURCC_VP8L) // Lossless
                        {
                            VP8L vp8l = new VP8L();
                            var pVP8L = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VP8L)));
                            mmr = mmioRead(hmmio, pVP8L, (int)Marshal.SizeOf(typeof(VP8L)));
                            vp8l = (VP8L)Marshal.PtrToStructure(pVP8L, typeof(VP8L));
                            Marshal.FreeHGlobal(pVP8L);
                            
                            m_nFrameWidth = (uint)((vp8l.byteData[0] | (vp8l.byteData[1] & 0x3F) << 8) + 1);
                            m_nFrameHeight = (uint)((vp8l.byteData[1] >> 6 | vp8l.byteData[2] << 2 | (vp8l.byteData[3] & 0x0F) << 10) + 1);
                            m_nCanvasWidth = m_nFrameWidth;
                            m_nCanvasHeight = m_nFrameHeight;

                            bool bAlpha = true;
                            listFrames.Add(new Frame(0, 0, m_nCanvasWidth, m_nCanvasHeight, 90, (bAlpha ? 0 : 1), 1));
                            m_nTotalDuration += m_nFrameDuration;
                        }

                        if (mmck.ckid == FOURCC_VP8X)
                        {
                            VP8X vp8x = new VP8X();
                            var pVP8X = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VP8X)));
                            mmr = mmioRead(hmmio, pVP8X, (int)Marshal.SizeOf(typeof(VP8X)));
                            vp8x = (VP8X)Marshal.PtrToStructure(pVP8X, typeof(VP8X));
                            Marshal.FreeHGlobal(pVP8X);

                            var bICC = (vp8x.byteFlags & (byte)VP8XFlags.ICCP_FLAG) == (byte)VP8XFlags.ICCP_FLAG;
                            m_bAnimation = (vp8x.byteFlags & (byte)VP8XFlags.ANIMATION_FLAG) == (byte)VP8XFlags.ANIMATION_FLAG;
                            var bAlpha = (vp8x.byteFlags & (byte)VP8XFlags.ALPHA_FLAG) == (byte)VP8XFlags.ALPHA_FLAG;
                            var bExif = (vp8x.byteFlags & (byte)VP8XFlags.EXIF_FLAG) == (byte)VP8XFlags.EXIF_FLAG;
                            var bXMP = (vp8x.byteFlags & (byte)VP8XFlags.XMP_FLAG) == (byte)VP8XFlags.XMP_FLAG;

                            m_nCanvasWidth = (uint)((vp8x.byteCanvasWidthMinusOne[0] | vp8x.byteCanvasWidthMinusOne[1] << 8 | vp8x.byteCanvasWidthMinusOne[2] << 16) + 1);
                            m_nCanvasHeight = (uint)((vp8x.byteCanvasHeightMinusOne[0] | vp8x.byteCanvasHeightMinusOne[1] << 8 | vp8x.byteCanvasHeightMinusOne[2] << 16) + 1);

                            if (!m_bAnimation)
                            {
                                if (listFrames.Count == 0)
                                {
                                    listFrames.Add(new Frame(0, 0, m_nCanvasWidth, m_nCanvasHeight, 90, (bAlpha ? 0 : 1), 1));
                                }                                
                            }
                        }

                        if (mmck.ckid == FOURCC_ANIM)
                        {
                            if (m_bAnimation)
                            {                               
                                ANIM dataAnim = new ANIM();
                                var pAnim = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ANIM)));
                                mmr = mmioRead(hmmio, pAnim, (int)Marshal.SizeOf(typeof(ANIM)));
                                dataAnim = (ANIM)Marshal.PtrToStructure(pAnim, typeof(ANIM));
                                Marshal.FreeHGlobal(pAnim);

                                m_nLoopCount = (uint)dataAnim.LoopCount;

                                m_BackgroundColor = System.Drawing.Color.FromArgb((int)dataAnim.BackgroundColor);
                                var nBlue = m_BackgroundColor.B;
                                var nGreen = m_BackgroundColor.G;
                                var nRed = m_BackgroundColor.R;
                                var nAlpha = m_BackgroundColor.A;
                            }
                        }

                        if (mmck.ckid == FOURCC_ANMF)
                        {
                            if (m_bAnimation)
                            {                                
                                ANMF dataAnmf = new ANMF();
                                var pAnmf = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ANMF)));
                                mmr = mmioRead(hmmio, pAnmf, (int)Marshal.SizeOf(typeof(ANMF)));
                                dataAnmf = (ANMF)Marshal.PtrToStructure(pAnmf, typeof(ANMF));
                                Marshal.FreeHGlobal(pAnmf);

                                var blendingMethod = ((dataAnmf.bd >> 1) & 1);
                                var disposalMethod = (dataAnmf.bd & 1);

                                uint nFrameX = (uint)((dataAnmf.byteFrameX[0] | (dataAnmf.byteFrameX[1] << 8) | (dataAnmf.byteFrameX[2] << 16)) * 2);
                                uint nFrameY = (uint)((dataAnmf.byteFrameY[0] | (dataAnmf.byteFrameY[1] << 8) | (dataAnmf.byteFrameY[2] << 16)) * 2);
                                uint nFrameWidth = (uint)((dataAnmf.byteFrameWidthMinusOne[0] | (dataAnmf.byteFrameWidthMinusOne[1] << 8) | (dataAnmf.byteFrameWidthMinusOne[2] << 16)) + 1);
                                uint nFrameHeight = (uint)((dataAnmf.byteFrameHeightMinusOne[0] | (dataAnmf.byteFrameHeightMinusOne[1] << 8) | (dataAnmf.byteFrameHeightMinusOne[2] << 16)) + 1);
                                uint nFrameDuration = (uint)(dataAnmf.byteFrameDuration[0] | (dataAnmf.byteFrameDuration[1] << 8) | (dataAnmf.byteFrameDuration[2] << 16));
                                if (listFrames.Count == 0)
                                    m_nFrameDuration = nFrameDuration;
                                m_nTotalDuration += nFrameDuration;
                                listFrames.Add(new Frame(nFrameX, nFrameY, nFrameWidth, nFrameHeight, nFrameDuration, blendingMethod, disposalMethod));
                            }
                        }

                        if (mmck.ckid == FOURCC_EXIF)
                        {
                        }

                        if (mmck.ckid == FOURCC_ICCP)
                        {
                        }

                        if (mmck.ckid == FOURCC_XMP)
                        {
                        }

                        if (mmioAscend(hmmio, ref mmck, 0) != MMSYSERR_NOERROR)
                            break;
                    }           
                }
                mmioClose(hmmio, 0);
            }

            if (bFileOK)
            {
                SafeRelease(ref m_pWICBitmapDecoder);
                LoadBitmapDecoder(sFile);

                this.Width = m_nCanvasWidth;
                this.Height = m_nCanvasHeight;

                if (tbWidth != null)
                    tbWidth.Text = m_nCanvasWidth.ToString();
                if (tbHeight != null)
                    tbHeight.Text = m_nCanvasHeight.ToString();
                if (tbAnimation != null)
                {
                    tbAnimation.Text = m_bAnimation ? "Yes" : "No";
                    if (m_bAnimation)
                    {
                        tbAnimation.Text += (" (" + m_nNbFrames.ToString() + " frames)");
                        if (m_nLoopCount > 0)
                            tbAnimation.Text += (" (" + m_nLoopCount.ToString() + " loops)");
                    }
                }

                if (stopWatch == null)
                {
                    stopWatch = new Stopwatch();
                    stopWatch.Start();
                }
                else
                    stopWatch.Restart();
            }
            return bFileOK;
        }

        Stopwatch stopWatch = null; 

        public HRESULT LoadBitmapDecoder(string sFile)
        {
            HRESULT hr = HRESULT.S_OK;
            hr = m_pWICImagingFactory.CreateDecoderFromFilename(sFile, Guid.Empty, unchecked((int)GENERIC_READ), WICDecodeOptions.WICDecodeMetadataCacheOnLoad, out m_pWICBitmapDecoder);
            if (hr == HRESULT.S_OK)
            {               
                hr = m_pWICBitmapDecoder.GetFrameCount(out m_nNbFrames);         
            }
            return (hr);
        }

        HRESULT GetBitmapFromFrame(uint nFrame)
        {
            HRESULT hr = HRESULT.S_OK;

            IWICBitmapFrameDecode pWICBitmapFrameDecode = null;
            if (m_pWICBitmapDecoder != null)
            {
                hr = m_pWICBitmapDecoder.GetFrame(nFrame, out pWICBitmapFrameDecode);
                if (hr == HRESULT.S_OK)
                {
                    //uint originalWidth, originalHeight;
                    //hr = pWICBitmapFrameDecode.GetSize(out originalWidth, out originalHeight);

                    IWICFormatConverter pConverter = null;
                    hr = m_pWICImagingFactory.CreateFormatConverter(out pConverter);
                    if (hr == HRESULT.S_OK)
                    {
                        hr = pConverter.Initialize(pWICBitmapFrameDecode, GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0f, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
                        if (hr == HRESULT.S_OK)
                        {
                            SafeRelease(ref m_pD2DBitmapFrame);
                            D2D1_BITMAP_PROPERTIES bitmapProperties = new D2D1_BITMAP_PROPERTIES();
                            //bitmapProperties.pixelFormat = D2DTools.PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED);
                            bitmapProperties.pixelFormat = D2DTools.PixelFormat();
                            int nAlpha = listFrames.ElementAt((int)nFrame).Blending;
                            //if (nAlpha == 0)
                            //    bitmapProperties.pixelFormat = D2DTools.PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED);
                            //else
                            //    bitmapProperties.pixelFormat = D2DTools.PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_IGNORE);
                            bitmapProperties.dpiX = 96;
                            bitmapProperties.dpiY = 96;
                            hr = m_pD2DDeviceContext.CreateBitmapFromWicBitmap(pConverter, bitmapProperties, out m_pD2DBitmapFrame);
                        }
                        SafeRelease(ref pConverter);
                    }
                    SafeRelease(ref pWICBitmapFrameDecode);
                }
            }

            return (hr);
        }

        //public HRESULT Dispose(uint nFrame)
        //{
        //    HRESULT hr = HRESULT.S_OK;
        //    if (m_pD2DDeviceContext != null)
        //    {
        //        m_pD2DDeviceContext.BeginDraw();
        //        int nDisposal = listFrames.ElementAt((int)nFrame).Disposal;               
        //        if (nDisposal == 1)
        //            m_pD2DDeviceContext.Clear(new ColorF(m_BackgroundColor.R, m_BackgroundColor.G, m_BackgroundColor.B, m_BackgroundColor.A));
        //        hr = m_pD2DDeviceContext.EndDraw(out ulong tag11, out ulong tag12);
        //        if ((uint)hr == D2DTools.D2DERR_RECREATE_TARGET)
        //        {
        //            m_pD2DDeviceContext.SetTarget(null);
        //            SafeRelease(ref m_pD2DDeviceContext);
        //            hr = CreateDeviceContext();
        //            hr = CreateSwapChain(IntPtr.Zero);
        //            hr = ConfigureSwapChain(m_hWndMain);
        //        }
        //        hr = m_pDXGISwapChain1.Present(1, 0);
        //    }
        //    return (hr);
        //}

        public HRESULT Render(uint nFrame)
        {
            HRESULT hr = HRESULT.S_OK;
            if (m_pD2DDeviceContext != null)
            {
                m_pD2DDeviceContext.BeginDraw();
                m_pD2DDeviceContext.GetSize(out D2D1_SIZE_F size);  

                int nDisposal = 0;
                if (nFrame > 0)
                    nDisposal = listFrames.ElementAt((int)nFrame - 1).Disposal;
                else
                    nDisposal = listFrames.ElementAt((int)m_nNbFrames - 1).Disposal;
                if (nDisposal == 1)
                    m_pD2DDeviceContext.Clear(new ColorF(m_BackgroundColor.R, m_BackgroundColor.G, m_BackgroundColor.B, m_BackgroundColor.A));
                
                //if (nFrame == 0)
                m_pD2DDeviceContext.Clear(new ColorF(ColorF.Enum.White, 0.0f));

                //float nWidth = this.ActualWidth;
                //float nHeight = this.ActualHeight;
                float nWidth = size.width;
                float nHeight = size.height;
                D2D1_RECT_F destRect = new D2D1_RECT_F(0.0f, 0.0f, nWidth, nHeight);

                //uint nFrameX = listFrames.ElementAt((int)nFrame).X;
                //uint nFrameY = listFrames.ElementAt((int)nFrame).Y;

                //uint nFrameWidth = listFrames.ElementAt((int)nFrame).Width;
                //uint nFrameHeight = listFrames.ElementAt((int)nFrame).Height;

                //destRect.left = (size.width - (float)(nFrameX + nFrameWidth)) / 2.0f;
                //destRect.top = (size.height - (float)(nFrameY + nFrameHeight)) / 2.0f;
                //destRect.right = destRect.left + (float)nFrameWidth;
                //destRect.bottom = destRect.top + (float)nFrameHeight;

                uint nFrameWidth = m_nCanvasWidth;
                uint nFrameHeight = m_nCanvasHeight;

                destRect.left = (nWidth - nFrameWidth) / 2.0f;
                destRect.top = (nHeight - nFrameHeight) / 2.0f;
                destRect.right = destRect.left + (float)nFrameWidth;
                destRect.bottom = destRect.top + (float)nFrameHeight;

                //float fAspectRatio = (float)nFrameWidth / (float)nFrameHeight;
                //if (destRect.left < 0)
                //{
                //    float fNewWidth = nWidth;
                //    float fNewHeight = fNewWidth / fAspectRatio;
                //    destRect.left = 0;
                //    destRect.top = (nHeight - fNewHeight) / 2.0f;
                //    destRect.right = fNewWidth;
                //    destRect.bottom = destRect.top + fNewHeight;
                //}
                //if (destRect.top < 0)
                //{
                //    float fNewHeight = nHeight;
                //    float fNewWidth = fNewHeight * fAspectRatio;
                //    destRect.left = (nWidth - fNewWidth) / 2.0f;
                //    destRect.top = 0;
                //    destRect.right = destRect.left + fNewWidth;
                //    destRect.bottom = fNewHeight;
                //}

                D2D1_RECT_F sourceRect = new D2D1_RECT_F(0.0f, 0.0f, nFrameWidth, nFrameHeight);
                m_pD2DDeviceContext.DrawBitmap(m_pD2DBitmapFrame, ref destRect, 1.0f, D2D1_BITMAP_INTERPOLATION_MODE.D2D1_BITMAP_INTERPOLATION_MODE_LINEAR, ref sourceRect);

                //ID2D1SolidColorBrush m_pMainBrush = null;
                //hr = m_pD2DDeviceContext.CreateSolidColorBrush(new ColorF(ColorF.Enum.Blue), null, out m_pMainBrush);
                //m_pD2DDeviceContext.DrawRectangle(ref destRect, m_pMainBrush);
                //SafeRelease(ref m_pMainBrush);

                hr = m_pD2DDeviceContext.EndDraw(out ulong tag11, out ulong tag12);

                if ((uint)hr == D2DTools.D2DERR_RECREATE_TARGET)
                {
                    m_pD2DDeviceContext.SetTarget(null);
                    SafeRelease(ref m_pD2DDeviceContext);
                    hr = CreateDeviceContext();
                    //CleanDeviceResources();
                    //hr = CreateDeviceResources();
                    hr = CreateSwapChain(IntPtr.Zero);
                    hr = ConfigureSwapChain(m_hWndMain);
                }
                hr = m_pDXGISwapChain1.Present(1, 0);
            }
            return (hr);
        }

        private void WebPControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //float nWidthParent = (float)((FrameworkElement)this.Parent).ActualWidth;
            //float nHeightParent = (float)((FrameworkElement)this.Parent).ActualHeight;
            //this.Width = nWidthParent;
            //this.Height = nHeightParent;

            Resize(e.NewSize);
        }

        HRESULT Resize(Windows.Foundation.Size sz)
        {
            HRESULT hr = HRESULT.S_OK;

            if (m_pDXGISwapChain1 != null)
            {
                if (m_pD2DDeviceContext != null)
                    m_pD2DDeviceContext.SetTarget(null);

                if (m_pD2DTargetBitmap != null)
                    SafeRelease(ref m_pD2DTargetBitmap);

                // 0, 0 => HRESULT: 0x80070057 (E_INVALIDARG) if not CreateSwapChainForHwnd
                //hr = m_pDXGISwapChain1.ResizeBuffers(
                // 2,
                // 0,
                // 0,
                // DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                // 0
                // );
                if (sz.Width != 0 && sz.Height != 0)
                {
                    hr = m_pDXGISwapChain1.ResizeBuffers(
                      2,
                      (uint)sz.Width,
                      (uint)sz.Height,
                      DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                      0
                      );
                }
                ConfigureSwapChain(m_hWndMain);
            }
            return (hr);
        }

        public HRESULT CreateDeviceContext()
        {
            HRESULT hr = HRESULT.S_OK;
            uint creationFlags = (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT;

            // Needs "Enable native code Debugging"
            creationFlags |= (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG;

            int[] aD3D_FEATURE_LEVEL = new int[] { (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
                (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_3,
                (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_2, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1};

            D3D_FEATURE_LEVEL featureLevel;
            hr = D2DTools.D3D11CreateDevice(null,    // specify null to use the default adapter
                D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                creationFlags,              // optionally set debug and Direct2D compatibility flags
                                            //pD3D_FEATURE_LEVEL,              // list of feature levels this app can support
                aD3D_FEATURE_LEVEL,
                //(uint)Marshal.SizeOf(aD3D_FEATURE_LEVEL),   // number of possible feature levels
                (uint)aD3D_FEATURE_LEVEL.Length,
                D2DTools.D3D11_SDK_VERSION,
                out m_pD3D11DevicePtr,                    // returns the Direct3D device created
                out featureLevel,            // returns feature level of device created
                                             //out pD3D11DeviceContextPtr                    // returns the device immediate context
                out m_pD3D11DeviceContext
            );
            if (hr == HRESULT.S_OK)
            {
                //m_pD3D11DeviceContext = Marshal.GetObjectForIUnknown(pD3D11DeviceContextPtr) as ID3D11DeviceContext;             

                //ID2D1Multithread m_D2DMultithread;
                //m_D2DMultithread = (ID2D1Multithread)m_pD2DFactory1;

                //m_pD2DFactory1.GetDesktopDpi(out float x, out float y);

                m_pDXGIDevice = Marshal.GetObjectForIUnknown(m_pD3D11DevicePtr) as IDXGIDevice1;
                if (m_pD2DFactory1 != null)
                {
                    ID2D1Device pD2DDevice = null; // Released in CreateDeviceContext
                    hr = m_pD2DFactory1.CreateDevice(m_pDXGIDevice, out pD2DDevice);
                    if (hr == HRESULT.S_OK)
                    {
                        hr = pD2DDevice.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE, out m_pD2DDeviceContext);
                        SafeRelease(ref pD2DDevice);
                    }
                }
                //Marshal.Release(m_pD3D11DevicePtr);
            }
            return hr;
        }

        HRESULT CreateSwapChain(IntPtr hWnd)
        {
            HRESULT hr = HRESULT.S_OK;
            DXGI_SWAP_CHAIN_DESC1 swapChainDesc = new DXGI_SWAP_CHAIN_DESC1();
            swapChainDesc.Width = 1;
            swapChainDesc.Height = 1;
            swapChainDesc.Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM; // this is the most common swapchain format
            swapChainDesc.Stereo = false;
            swapChainDesc.SampleDesc.Count = 1;                // don't use multi-sampling
            swapChainDesc.SampleDesc.Quality = 0;
            swapChainDesc.BufferUsage = D2DTools.DXGI_USAGE_RENDER_TARGET_OUTPUT;
            swapChainDesc.BufferCount = 2;                     // use double buffering to enable flip
            swapChainDesc.Scaling = (hWnd != IntPtr.Zero) ? DXGI_SCALING.DXGI_SCALING_NONE : DXGI_SCALING.DXGI_SCALING_STRETCH;
            swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL; // all apps must use this SwapEffect       
            swapChainDesc.Flags = 0;

            IDXGIAdapter pDXGIAdapter;
            hr = m_pDXGIDevice.GetAdapter(out pDXGIAdapter);
            if (hr == HRESULT.S_OK)
            {
                IntPtr pDXGIFactory2Ptr;
                hr = pDXGIAdapter.GetParent(typeof(IDXGIFactory2).GUID, out pDXGIFactory2Ptr);
                if (hr == HRESULT.S_OK)
                {
                    IDXGIFactory2 pDXGIFactory2 = Marshal.GetObjectForIUnknown(pDXGIFactory2Ptr) as IDXGIFactory2;
                    if (hWnd != IntPtr.Zero)
                        hr = pDXGIFactory2.CreateSwapChainForHwnd(m_pD3D11DevicePtr, hWnd, ref swapChainDesc, IntPtr.Zero, null, out m_pDXGISwapChain1);
                    else
                        hr = pDXGIFactory2.CreateSwapChainForComposition(m_pD3D11DevicePtr, ref swapChainDesc, null, out m_pDXGISwapChain1);

                    hr = m_pDXGIDevice.SetMaximumFrameLatency(1);
                    SafeRelease(ref pDXGIFactory2);
                    Marshal.Release(pDXGIFactory2Ptr);
                }
                SafeRelease(ref pDXGIAdapter);
            }
            return hr;
        }

        HRESULT ConfigureSwapChain(IntPtr hWnd)
        {
            HRESULT hr = HRESULT.S_OK;

            //IntPtr pD3D11Texture2DPtr = IntPtr.Zero;
            //hr = m_pDXGISwapChain1.GetBuffer(0, typeof(ID3D11Texture2D).GUID, ref pD3D11Texture2DPtr);
            //m_pD3D11Texture2D = Marshal.GetObjectForIUnknown(pD3D11Texture2DPtr) as ID3D11Texture2D;

            D2D1_BITMAP_PROPERTIES1 bitmapProperties = new D2D1_BITMAP_PROPERTIES1();
            bitmapProperties.bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CANNOT_DRAW;
            bitmapProperties.pixelFormat = D2DTools.PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_IGNORE);
            //float nDpiX, nDpiY = 0.0f;
            //m_pD2DContext.GetDpi(out nDpiX, out nDpiY);
            uint nDPI = GetDpiForWindow(hWnd);
            bitmapProperties.dpiX = nDPI;
            bitmapProperties.dpiY = nDPI;

            IntPtr pDXGISurfacePtr = IntPtr.Zero;
            hr = m_pDXGISwapChain1.GetBuffer(0, typeof(IDXGISurface).GUID, out pDXGISurfacePtr);
            if (hr == HRESULT.S_OK)
            {
                IDXGISurface pDXGISurface = Marshal.GetObjectForIUnknown(pDXGISurfacePtr) as IDXGISurface;
                hr = m_pD2DDeviceContext.CreateBitmapFromDxgiSurface(pDXGISurface, ref bitmapProperties, out m_pD2DTargetBitmap);
                if (hr == HRESULT.S_OK)
                {
                    m_pD2DDeviceContext.SetTarget(m_pD2DTargetBitmap);
                }
                SafeRelease(ref pDXGISurface);
                Marshal.Release(pDXGISurfacePtr);
            }
            return hr;
        }

        void Clean()
        {
            SafeRelease(ref m_pD2DTargetBitmap);
            SafeRelease(ref m_pDXGISwapChain1);

            SafeRelease(ref m_pDXGIDevice);
            SafeRelease(ref m_pD3D11DeviceContext);
            SafeRelease(ref m_pD2DDeviceContext);
            Marshal.Release(m_pD3D11DevicePtr);

            SafeRelease(ref m_pWICBitmapDecoder);
            SafeRelease(ref m_pD2DBitmapFrame);           
        }

        public void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)

                    stopWatch.Stop();                    
                    stopWatch = null; 
                }
                Clean();

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~WebPControl()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class Frame
    {
        #region Properties
        public uint X { get; set; }
        public uint Y { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint Duration { get; set; }
        public int Blending { get; set; }
        public int Disposal { get; set; }
        #endregion

        public Frame(uint nX, uint nY, uint nWidth, uint nHeight, uint nDuration, int nBlending, int nDisposal)
        {
            X = nX;
            Y = nY;
            Width = nWidth;
            Height = nHeight;
            Duration = nDuration;
            Blending = nBlending;
            Disposal = nDisposal;
        }
    }
}
