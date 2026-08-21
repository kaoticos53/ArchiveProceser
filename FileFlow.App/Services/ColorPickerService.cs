using System.Runtime.InteropServices;

namespace FileFlow.App.Services;

/// <summary>
/// Implementación de selección de color nativo de Windows (Win32 Common Dialog).
/// </summary>
public class ColorPickerService : IColorPickerService
{
    public static ColorPickerService Instance { get; } = new();

    private static readonly int[] CustomColors = new int[16];

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ChooseColor(ref CHOOSECOLOR cc);

    public string? PickColorHex()
    {
        var gch = GCHandle.Alloc(CustomColors, GCHandleType.Pinned);
        try
        {
            var cc = new CHOOSECOLOR();
            cc.lStructSize = Marshal.SizeOf(cc);
            cc.lpCustColors = gch.AddrOfPinnedObject();
            cc.Flags = 0x00000002 | 0x00000001; // CC_FULLOPEN | CC_RGBINIT

            if (ChooseColor(ref cc))
            {
                int rgb = cc.rgbResult;
                byte r = (byte)(rgb & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)((rgb >> 16) & 0xFF);
                return $"#{r:X2}{g:X2}{b:X2}";
            }

            return null;
        }
        finally
        {
            gch.Free();
        }
    }
}
