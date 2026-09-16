using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinTweaker.Services
{
    public class DisplayModeInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }

        public string DisplayName =>
            $"{Width} × {Height} @ {RefreshRate} Гц";
    }

    public class OriginalDisplayMode
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public int BitsPerPixel { get; set; }
    }

    public static class DisplayService
    {
        private const int ENUM_CURRENT_SETTINGS = -1;

        private const uint CDS_UPDATEREGISTRY = 0x00000001;
        private const uint CDS_TEST = 0x00000002;

        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISP_CHANGE_RESTART = 1;

        private const int DM_BITSPERPEL = 0x00040000;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;
        private const int DM_DISPLAYFREQUENCY = 0x00400000;

        private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

        private static OriginalDisplayMode? _originalMode;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(
            string? lpDevice,
            uint iDevNum,
            ref DISPLAY_DEVICE lpDisplayDevice,
            uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(
            string lpszDeviceName,
            int iModeNum,
            ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(
            string? lpszDeviceName,
            ref DEVMODE lpDevMode,
            IntPtr hwnd,
            uint dwflags,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;

            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public short dmLogPixels;

            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;

            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        /// <summary>
        /// Возвращает имя основного монитора Windows.
        /// Обычно это \\.\DISPLAY1.
        /// </summary>
        public static string? GetPrimaryMonitorDeviceName()
        {
            for (uint i = 0; ; i++)
            {
                DISPLAY_DEVICE device = new DISPLAY_DEVICE
                {
                    cb = Marshal.SizeOf<DISPLAY_DEVICE>()
                };

                if (!EnumDisplayDevices(null, i, ref device, 0))
                    break;

                if ((device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                    continue;

                if ((device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0)
                    return device.DeviceName;
            }

            return null;
        }

        /// <summary>
        /// Возвращает текущее разрешение основного монитора.
        /// </summary>
        public static DisplayModeInfo? GetCurrentMode()
        {
            string? deviceName = GetPrimaryMonitorDeviceName();

            if (string.IsNullOrWhiteSpace(deviceName))
                return null;

            DEVMODE mode = CreateDevMode();

            if (!EnumDisplaySettings(
                    deviceName,
                    ENUM_CURRENT_SETTINGS,
                    ref mode))
            {
                return null;
            }

            return new DisplayModeInfo
            {
                Width = mode.dmPelsWidth,
                Height = mode.dmPelsHeight,
                RefreshRate = mode.dmDisplayFrequency
            };
        }

        /// <summary>
        /// Возвращает все доступные режимы основного монитора.
        /// </summary>
        public static List<DisplayModeInfo> GetAvailableModes()
        {
            var result = new List<DisplayModeInfo>();

            string? deviceName = GetPrimaryMonitorDeviceName();

            if (string.IsNullOrWhiteSpace(deviceName))
                return result;

            for (int modeIndex = 0; ; modeIndex++)
            {
                DEVMODE mode = CreateDevMode();

                if (!EnumDisplaySettings(
                        deviceName,
                        modeIndex,
                        ref mode))
                {
                    break;
                }

                if (mode.dmPelsWidth <= 0 ||
                    mode.dmPelsHeight <= 0 ||
                    mode.dmDisplayFrequency <= 0)
                {
                    continue;
                }

                bool exists = result.Exists(x =>
                    x.Width == mode.dmPelsWidth &&
                    x.Height == mode.dmPelsHeight &&
                    x.RefreshRate == mode.dmDisplayFrequency);

                if (!exists)
                {
                    result.Add(new DisplayModeInfo
                    {
                        Width = mode.dmPelsWidth,
                        Height = mode.dmPelsHeight,
                        RefreshRate = mode.dmDisplayFrequency
                    });
                }
            }

            result.Sort((a, b) =>
            {
                int widthCompare = b.Width.CompareTo(a.Width);

                if (widthCompare != 0)
                    return widthCompare;

                int heightCompare = b.Height.CompareTo(a.Height);

                if (heightCompare != 0)
                    return heightCompare;

                return b.RefreshRate.CompareTo(a.RefreshRate);
            });

            return result;
        }

        /// <summary>
        /// Сохраняет исходный режим основного монитора.
        /// </summary>
        public static bool SaveOriginalMode()
        {
            string? deviceName = GetPrimaryMonitorDeviceName();

            if (string.IsNullOrWhiteSpace(deviceName))
                return false;

            DEVMODE mode = CreateDevMode();

            if (!EnumDisplaySettings(
                    deviceName,
                    ENUM_CURRENT_SETTINGS,
                    ref mode))
            {
                return false;
            }

            _originalMode = new OriginalDisplayMode
            {
                Width = mode.dmPelsWidth,
                Height = mode.dmPelsHeight,
                RefreshRate = mode.dmDisplayFrequency,
                BitsPerPixel = mode.dmBitsPerPel
            };

            return true;
        }

        /// <summary>
        /// Меняет разрешение только основного монитора.
        /// </summary>
        public static bool SetResolution(
            int width,
            int height,
            int refreshRate)
        {
            string? deviceName = GetPrimaryMonitorDeviceName();

            if (string.IsNullOrWhiteSpace(deviceName))
                return false;

            if (_originalMode == null)
            {
                if (!SaveOriginalMode())
                    return false;
            }

            DEVMODE mode = CreateDevMode();

            if (!EnumDisplaySettings(
                    deviceName,
                    ENUM_CURRENT_SETTINGS,
                    ref mode))
            {
                return false;
            }

            mode.dmPelsWidth = width;
            mode.dmPelsHeight = height;
            mode.dmDisplayFrequency = refreshRate;

            mode.dmFields =
                DM_PELSWIDTH |
                DM_PELSHEIGHT |
                DM_DISPLAYFREQUENCY |
                DM_BITSPERPEL;

            // Проверяем режим перед применением.
            int testResult = ChangeDisplaySettingsEx(
                deviceName,
                ref mode,
                IntPtr.Zero,
                CDS_TEST,
                IntPtr.Zero);

            if (testResult != DISP_CHANGE_SUCCESSFUL)
                return false;

            // Применяем режим.
            int result = ChangeDisplaySettingsEx(
                deviceName,
                ref mode,
                IntPtr.Zero,
                CDS_UPDATEREGISTRY,
                IntPtr.Zero);

            return result == DISP_CHANGE_SUCCESSFUL ||
                   result == DISP_CHANGE_RESTART;
        }

        /// <summary>
        /// Возвращает сохранённый исходный режим.
        /// </summary>
        public static bool RestoreOriginalMode()
        {
            if (_originalMode == null)
                return false;

            int width = _originalMode.Width;
            int height = _originalMode.Height;
            int refreshRate = _originalMode.RefreshRate;

            string? deviceName = GetPrimaryMonitorDeviceName();

            if (string.IsNullOrWhiteSpace(deviceName))
                return false;

            DEVMODE mode = CreateDevMode();

            if (!EnumDisplaySettings(
                    deviceName,
                    ENUM_CURRENT_SETTINGS,
                    ref mode))
            {
                return false;
            }

            mode.dmPelsWidth = width;
            mode.dmPelsHeight = height;
            mode.dmDisplayFrequency = refreshRate;

            mode.dmFields =
                DM_PELSWIDTH |
                DM_PELSHEIGHT |
                DM_DISPLAYFREQUENCY |
                DM_BITSPERPEL;

            int testResult = ChangeDisplaySettingsEx(
                deviceName,
                ref mode,
                IntPtr.Zero,
                CDS_TEST,
                IntPtr.Zero);

            if (testResult != DISP_CHANGE_SUCCESSFUL)
                return false;

            int result = ChangeDisplaySettingsEx(
                deviceName,
                ref mode,
                IntPtr.Zero,
                CDS_UPDATEREGISTRY,
                IntPtr.Zero);

            if (result == DISP_CHANGE_SUCCESSFUL ||
                result == DISP_CHANGE_RESTART)
            {
                _originalMode = null;
                return true;
            }

            return false;
        }

        public static bool HasSavedOriginalMode()
        {
            return _originalMode != null;
        }

        public static void ClearSavedOriginalMode()
        {
            _originalMode = null;
        }

        private static DEVMODE CreateDevMode()
        {
            return new DEVMODE
            {
                dmDeviceName = string.Empty,
                dmFormName = string.Empty,
                dmSize = (short)Marshal.SizeOf<DEVMODE>()
            };
        }
    }
}