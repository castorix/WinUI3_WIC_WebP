using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using GlobalStructures;
using static GlobalStructures.GlobalTools;
using System.Text;
using Direct2D;
using DXGI;
using static DXGI.DXGITools;
using WIC;
using static WIC.WICTools;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUI3_WIC_WebP
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private IntPtr hWndMain = IntPtr.Zero;
        private Microsoft.UI.Windowing.AppWindow _apw;

        ID2D1Factory m_pD2DFactory = null;
        ID2D1Factory1 m_pD2DFactory1 = null;
        IWICImagingFactory m_pWICImagingFactory = null;

        public MainWindow()
        {
            this.InitializeComponent();
            hWndMain = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWndMain);
            _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);
            _apw.Resize(new Windows.Graphics.SizeInt32(1500, 800));
            this.Title = "WinUI 3 - WebP control";

            m_pWICImagingFactory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(WICTools.CLSID_WICImagingFactory));
            CheckDecoder();

            HRESULT hr = CreateD2D1Factory();

            WC1.Init(hWndMain, m_pD2DFactory1, m_pWICImagingFactory);
            string sExePath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string sFilePath = sExePath + @"/Assets/Smiley_Nerd.WebP";
            WC1.LoadFile(sFilePath, tbWidth, tbHeight, tbAnimation);

            // Test 2 controls
            //WC2.Init(hWndMain, m_pD2DFactory1, m_pWICImagingFactory);
            //WC2.LoadFile(sExePath + @"/Assets/Spider.WebP");

            this.Closed += MainWindow_Closed;           
        }  

        private async void CheckDecoder()
        {
            bool bWepbDecoder = FindWICWebPDecoder();
            if (!bWepbDecoder)
            {
                Windows.UI.Popups.MessageDialog md = new Windows.UI.Popups.MessageDialog("No WebP decoder found !", "Information");
                WinRT.Interop.InitializeWithWindow.Initialize(md, hWndMain);
                _ = await md.ShowAsync();
            }
        }

        private bool FindWICWebPDecoder()
        {
            bool bFoundWebPDecoder = false;
            IEnumUnknown pEnumUnknown = null;
            HRESULT hr = m_pWICImagingFactory.CreateComponentEnumerator(WICComponentType.WICDecoder, WICComponentEnumerateOptions.WICComponentEnumerateDefault, out pEnumUnknown);
            if (hr == HRESULT.S_OK)
            {
                object[] pUnknown = new object[1];
                uint uceltFetched;
                while (HRESULT.S_OK == pEnumUnknown.Next(1, pUnknown, out uceltFetched) && (uceltFetched == 1))
                {
                    StringBuilder sbBuffer = new StringBuilder(260);
                    IWICBitmapCodecInfo pWICBitmapCodecInfo = (IWICBitmapCodecInfo)pUnknown[0];
                    Guid clsid = Guid.Empty;
                    hr = pWICBitmapCodecInfo.GetCLSID(out clsid);
                    uint nLength = 0;
                    string sExtensions = string.Empty;
                    StringBuilder sbExtensions = new StringBuilder(260);
                    hr = pWICBitmapCodecInfo.GetFileExtensions((uint)sbExtensions.Capacity, sbExtensions, out nLength);
                    sExtensions = sbExtensions.ToString();
                    if (sExtensions.ToUpper().Contains(".WEBP"))
                    {
                        bFoundWebPDecoder = true;
                        break;
                    }
                    string sFriendlyName = string.Empty;
                    StringBuilder sbFriendlyName = new StringBuilder(260);
                    nLength = 0;
                    hr = pWICBitmapCodecInfo.GetFriendlyName((uint)sbFriendlyName.Capacity, sbFriendlyName, out nLength);
                    sFriendlyName = sbFriendlyName.ToString();
                }
                SafeRelease(ref pEnumUnknown);
            }
            return bFoundWebPDecoder;
        }

        //private void myButton_Click(object sender, RoutedEventArgs e)
        //{
        //    myButton.Content = "Clicked";
        //}

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            string sFilePath = await OpenFileDialog();
            if (sFilePath != string.Empty)
            {
                if (! WC1.LoadFile(sFilePath, tbWidth, tbHeight, tbAnimation))
                {
                    Windows.UI.Popups.MessageDialog md = new Windows.UI.Popups.MessageDialog(sFilePath + " does not seem to be a WebP file !", "Information");
                    WinRT.Interop.InitializeWithWindow.Initialize(md, hWndMain);
                    _ = await md.ShowAsync();
                }
            }
        }

        private async Task<string> OpenFileDialog()
        {
            var fop = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(fop, hWndMain);
            fop.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;

            fop.FileTypeFilter.Add(".webp");   

            var file = await fop.PickSingleFileAsync();
            return (file != null ? file.Path : string.Empty);
        }

        public HRESULT CreateD2D1Factory()
        {
            HRESULT hr = HRESULT.S_OK;
            D2D1_FACTORY_OPTIONS options = new D2D1_FACTORY_OPTIONS();

            // Needs "Enable native code Debugging"
            options.debugLevel = D2D1_DEBUG_LEVEL.D2D1_DEBUG_LEVEL_INFORMATION;

            hr = D2DTools.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, ref D2DTools.CLSID_D2D1Factory, ref options, out m_pD2DFactory);
            //hr = D2DTools.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_MULTI_THREADED, ref D2DTools.CLSID_D2D1Factory, ref options, out m_pD2DFactory);
            m_pD2DFactory1 = (ID2D1Factory1)m_pD2DFactory;
            return hr;
        }

        void Clean()
        {
            SafeRelease(ref m_pWICImagingFactory);
            SafeRelease(ref m_pD2DFactory1);
            SafeRelease(ref m_pD2DFactory);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            WC1.Dispose(true);
            //WC2.Dispose(true);
            Clean();
        }
    }
}
