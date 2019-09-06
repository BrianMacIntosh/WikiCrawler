using System;
using System.Runtime.InteropServices;

public static class WindowsUtility
{
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

	//Flash both the window caption and taskbar button.
	//This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags. 
	public const UInt32 FLASHW_ALL = 3;

	// Flash continuously until the window comes to the foreground. 
	public const UInt32 FLASHW_TIMERNOFG = 12;

	[StructLayout(LayoutKind.Sequential)]
	public struct FLASHWINFO
	{
		public UInt32 cbSize;
		public IntPtr hwnd;
		public UInt32 dwFlags;
		public UInt32 uCount;
		public UInt32 dwTimeout;
	}
	
	public static bool FlashWindowEx(IntPtr hWnd)
	{
		FLASHWINFO fInfo = new FLASHWINFO();

		fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
		fInfo.hwnd = hWnd;
		fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
		fInfo.uCount = UInt32.MaxValue;
		fInfo.dwTimeout = 0;

		return FlashWindowEx(ref fInfo);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct TokPriv1Luid
	{
		public int Count;
		public long Luid;
		public int Attr;
	}

	[DllImport("kernel32.dll", ExactSpelling = true)]
	internal static extern IntPtr GetCurrentProcess();

	[DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
	internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

	[DllImport("advapi32.dll", SetLastError = true)]
	internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

	[DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
	internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

	[DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
	internal static extern bool ExitWindowsEx(int flg, int rea);

	public enum EWX
	{
		EWX_LOGOFF = 0x00000000,
		EWX_SHUTDOWN = 0x00000001,
		EWX_REBOOT = 0x00000002,
		EWX_FORCE = 0x00000004,
		EWX_POWEROFF = 0x00000008,
		EWX_FORCEIFHUNG = 0x00000010,
	}

	internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
	internal const int TOKEN_QUERY = 0x00000008;
	internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
	internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

	public static void DoExitWin(EWX flg)
	{
		bool ok;
		TokPriv1Luid tp;
		IntPtr hproc = GetCurrentProcess();
		IntPtr htok = IntPtr.Zero;
		ok = OpenProcessToken(hproc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok);
		tp.Count = 1;
		tp.Luid = 0;
		tp.Attr = SE_PRIVILEGE_ENABLED;
		ok = LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref tp.Luid);
		ok = AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
		ok = ExitWindowsEx((int)flg, 0);
	}
}
