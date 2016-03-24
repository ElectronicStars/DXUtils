using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Capture.Interface
{
    class KeyboardHook
    {


    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;

    private static LowLevelKeyboardProc _proc = HookCallback;

    private static IntPtr _hookID = IntPtr.Zero;


    //public static void Main()

    //{

    //    //_hookID = SetHook(_proc);

    //    //Application.Run();

    //    //UnhookWindowsHookEx(_hookID);

    //}
        

    public static void  SetHook()
    {
        _hookID = SetHook(_proc);
        _interface.Message(MessageType.Information, _hookID.ToString());

    }
    private  static IntPtr SetHook(LowLevelKeyboardProc proc)

    {

        using (Process curProcess = Process.GetCurrentProcess())

        using (ProcessModule curModule = curProcess.MainModule)

        {
            _interface.Message(MessageType.Information, curProcess.MainModule.FileName);

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,

                GetModuleHandle(curModule.ModuleName), 0);

        }

    }


    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);


    struct keydata
    {
        int code;
        DateTime time;

        public keydata(int _code, DateTime _time)
        {
            code = _code;
            time = _time;
        }

        public override string ToString()
        {
            return time.ToString() + ":" + code.ToString();
        }
    }
    const int BUFFERSIZE = 20;
    static keydata[] buffer = new keydata[BUFFERSIZE];
    static keydata[] buffer2 = new keydata[BUFFERSIZE];
    static int ptr = 0 ;

    static bool buffer1full = false;
    static bool buffer2full = false;
    static bool swap = false;
    static keydata[] GetBuffer()
    {
        if (swap)
        {
            return buffer2;
        }
        else
        {
            return buffer;
        }

        
    }
    static Queue<keydata> keyqueue;
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        _interface.Message(MessageType.Information, "pressed");
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)

        {
            _interface.Message(MessageType.Information,"pressed");

            int vkCode = Marshal.ReadInt32(lParam);
            //Console.WriteLine((System.Windows.Forms.Keys)vkCode);
            _interface.Message(MessageType.Information, ((System.Windows.Forms.Keys)vkCode).ToString());

            buffer[ptr] = new keydata(vkCode, DateTime.Now);
            ptr++;
            if (ptr == BUFFERSIZE)
            {
                ptr = ptr % BUFFERSIZE;
                swap = !swap;
                _wait.Set();
            }

        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);

    }

    static ManualResetEvent _wait = new ManualResetEvent(false);
    static List<keydata> keydatalist = new List<keydata>();

    public static CaptureInterface _interface = null;
    public static void Consumer(Object obj1)
    {
        _wait.Reset();
        while (true)
        {
            _wait.WaitOne();

            //on signal copy
            //TODO we might need to lock swap and buffer
            keydata[] currentbuffer  = null;
            if (swap)
            {
                 currentbuffer =buffer;
            }
            else
            {
                currentbuffer = buffer2;
            }

             keydatalist.AddRange(currentbuffer);
             if (_interface != null)
             {
                 _interface.Message(MessageType.Information, currentbuffer.ToString());

             }
            _wait.Reset();

        }
    }


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]

    private static extern IntPtr SetWindowsHookEx(int idHook,

        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]

    [return: MarshalAs(UnmanagedType.Bool)]

    private static extern bool UnhookWindowsHookEx(IntPtr hhk);


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]

    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,

        IntPtr wParam, IntPtr lParam);


    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]

    private static extern IntPtr GetModuleHandle(string lpModuleName);

}

}
