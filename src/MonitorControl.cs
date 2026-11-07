using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OdysseyRemote
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        struct Rect
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte bVCPCode, ref uint pvct, ref uint pdwCurrentValue, ref uint pdwMaximumValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MONITORINFOEX
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        const uint MONITORINFOF_PRIMARY = 1;
        const byte VCP_BRIGHTNESS = 0x10;
        const byte VCP_INPUT_SOURCE = 0x60;

        class TargetMonitorInfo
        {
            public IntPtr hPhysical;
            public PHYSICAL_MONITOR[] allPhys;
            public uint physCount;
            public int width;
            public int height;
            public bool isPrimary;
            public string deviceName;
        }

        static TargetMonitorInfo FindPrimaryMonitor()
        {
            TargetMonitorInfo result = null;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdcMon, ref Rect rc, IntPtr data) =>
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                GetMonitorInfo(hMon, ref mi);

                bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                int w = rc.right - rc.left;
                int h = rc.bottom - rc.top;

                if (isPrimary || result == null)
                {
                    uint count = 0;
                    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) && count > 0)
                    {
                        PHYSICAL_MONITOR[] phys = new PHYSICAL_MONITOR[count];
                        if (GetPhysicalMonitorsFromHMONITOR(hMon, count, phys))
                        {
                            result = new TargetMonitorInfo();
                            result.hPhysical = phys[0].hPhysicalMonitor;
                            result.allPhys = phys;
                            result.physCount = count;
                            result.width = w;
                            result.height = h;
                            result.isPrimary = isPrimary;
                            result.deviceName = mi.szDevice;

                            if (isPrimary)
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            TargetMonitorInfo target = FindPrimaryMonitor();
            if (target == null)
            {
                Console.WriteLine("{\"error\": \"Primary monitor not found or DDC/CI unsupported\"}");
                return 1;
            }

            try
            {
                IntPtr hPhys = target.hPhysical;

                switch (command)
                {
                    case "status":
                    {
                        uint pvct = 0, curB = 0, maxB = 0;
                        bool bSuccess = GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_BRIGHTNESS, ref pvct, ref curB, ref maxB);
                        uint curIn = 0, maxIn = 0;
                        bool inSuccess = GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_INPUT_SOURCE, ref pvct, ref curIn, ref maxIn);

                        Console.WriteLine(string.Format(
                            "{{\"success\":true,\"device\":\"{0}\",\"resolution\":\"{1}x{2}\",\"brightness\":{{\"current\":{3},\"max\":{4},\"supported\":{5}}},\"input\":{{\"current\":{6},\"currentHex\":\"0x{6:X2}\",\"supported\":{7}}}}}",
                            target.deviceName, target.width, target.height,
                            curB, maxB, bSuccess.ToString().ToLower(),
                            curIn, inSuccess.ToString().ToLower()
                        ));
                        return 0;
                    }

                    case "get-brightness":
                    {
                        uint pvct = 0, curB = 0, maxB = 0;
                        if (GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_BRIGHTNESS, ref pvct, ref curB, ref maxB))
                        {
                            Console.WriteLine(curB);
                            return 0;
                        }
                        Console.WriteLine("{\"error\": \"Failed to read brightness\"}");
                        return 2;
                    }

                    case "set-brightness":
                    {
                        uint val = 0;
                        if (args.Length < 2 || !uint.TryParse(args[1], out val))
                        {
                            Console.WriteLine("{\"error\": \"Invalid brightness value\"}");
                            return 3;
                        }
                        uint pvct = 0, curB = 0, maxB = 0;
                        GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_BRIGHTNESS, ref pvct, ref curB, ref maxB);
                        if (maxB > 0 && val > maxB) val = maxB;

                        if (SetVCPFeature(hPhys, VCP_BRIGHTNESS, val))
                        {
                            Console.WriteLine(string.Format("{{\"success\":true,\"brightness\":{0}}}", val));
                            return 0;
                        }
                        Console.WriteLine("{\"error\": \"Failed to set brightness\"}");
                        return 4;
                    }

                    case "brightness-up":
                    case "brightness-down":
                    {
                        uint step = 5;
                        uint s = 0;
                        if (args.Length >= 2 && uint.TryParse(args[1], out s)) step = s;

                        uint pvct = 0, curB = 0, maxB = 0;
                        if (!GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_BRIGHTNESS, ref pvct, ref curB, ref maxB))
                        {
                            Console.WriteLine("{\"error\": \"Failed to read brightness\"}");
                            return 5;
                        }

                        int nextVal = (int)curB + (command == "brightness-up" ? (int)step : -(int)step);
                        if (nextVal < 0) nextVal = 0;
                        if (maxB > 0 && nextVal > (int)maxB) nextVal = (int)maxB;

                        if (SetVCPFeature(hPhys, VCP_BRIGHTNESS, (uint)nextVal))
                        {
                            Console.WriteLine(string.Format("{{\"success\":true,\"brightness\":{0},\"max\":{1}}}", nextVal, maxB));
                            return 0;
                        }
                        Console.WriteLine("{\"error\": \"Failed to update brightness\"}");
                        return 6;
                    }

                    case "get-input":
                    {
                        uint pvct = 0, curIn = 0, maxIn = 0;
                        if (GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_INPUT_SOURCE, ref pvct, ref curIn, ref maxIn))
                        {
                            Console.WriteLine(string.Format("{{\"current\":{0},\"hex\":\"0x{0:X2}\"}}", curIn));
                            return 0;
                        }
                        Console.WriteLine("{\"error\": \"Failed to read input source\"}");
                        return 7;
                    }

                    case "set-input":
                    {
                        if (args.Length < 2)
                        {
                            Console.WriteLine("{\"error\": \"Target input value required (e.g. 15 for DP, 17 for HDMI)\"}");
                            return 8;
                        }

                        uint targetIn = 0;
                        string raw = args[1];
                        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            targetIn = Convert.ToUInt32(raw.Substring(2), 16);
                        }
                        else if (!uint.TryParse(raw, out targetIn))
                        {
                            Console.WriteLine("{\"error\": \"Invalid input value format\"}");
                            return 9;
                        }

                        if (SetVCPFeature(hPhys, VCP_INPUT_SOURCE, targetIn))
                        {
                            Console.WriteLine(string.Format("{{\"success\":true,\"targetInput\":{0},\"targetHex\":\"0x{0:X2}\"}}", targetIn));
                            return 0;
                        }
                        Console.WriteLine("{\"error\": \"Failed to switch input source\"}");
                        return 10;
                    }

                    default:
                        PrintHelp();
                        return 0;
                }
            }
            finally
            {
                if (target.allPhys != null && target.physCount > 0)
                {
                    DestroyPhysicalMonitors(target.physCount, target.allPhys);
                }
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("Odyssey G50D Monitor Controller CLI");
            Console.WriteLine("Usage: MonitorControl.exe <command> [args]");
            Console.WriteLine("Commands:");
            Console.WriteLine("  status                 - Get monitor status (brightness, input)");
            Console.WriteLine("  get-brightness         - Get current brightness");
            Console.WriteLine("  set-brightness <val>   - Set brightness (0..50)");
            Console.WriteLine("  brightness-up [step]   - Increase brightness (default step: 5)");
            Console.WriteLine("  brightness-down [step] - Decrease brightness (default step: 5)");
            Console.WriteLine("  get-input              - Get current input source code");
            Console.WriteLine("  set-input <code|hex>   - Switch input (e.g. 15 / 0x0F for DP/PC, 17 / 0x11 for HDMI)");
        }
    }
}
