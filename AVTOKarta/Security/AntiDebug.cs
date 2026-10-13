using System;
using System.Collections.Generic;
#if PROTECTION
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif

namespace AVTOKarta.Security
{
    // Детектирование отладчиков и инструментов трассировки.
    // ВАЖНО: весь код проверок компилируется ТОЛЬКО при символе PROTECTION,
    // который объявлен исключительно в конфигурации Release|x86.
    // В Debug/иных сборках класс пустой и всегда возвращает пустой список:
    // ни P/Invoke, ни чтения PEB в бинарнике нет, поэтому запуск под отладчиком
    // (Visual Studio, dnSpy) никогда не приводит к отказу запуска.
    public static class AntiDebug
    {
#if PROTECTION
        private struct ProcessBasicInformation
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle, int processInformationClass,
            ref ProcessBasicInformation processInformation,
            int processInformationLength, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle, int processInformationClass,
            out IntPtr processInformation,
            int processInformationLength, out int returnLength);

        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll")]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        private const int ProcessBasicInformationClass = 0;
        private const int ProcessDebugPortClass = 7;
#endif

        public static IList<string> Detect()
        {
            var reasons = new List<string>();
#if !PROTECTION
            // Сборка без защиты: проверок отладчика нет by design.
            return reasons;
#else
            try
            {
                if (Debugger.IsAttached)
                    reasons.Add("подключён управляемый отладчик");
            }
            catch
            {
            }

            try
            {
                if (IsDebuggerPresent())
                    reasons.Add("активен отладчик (IsDebuggerPresent)");
            }
            catch
            {
            }

            try
            {
                IntPtr handle = GetCurrentProcess();
                bool remote = false;
                if (CheckRemoteDebuggerPresent(handle, ref remote) && remote)
                    reasons.Add("обнаружен удалённый отладчик");
            }
            catch
            {
            }

            try
            {
                IntPtr handle = GetCurrentProcess();
                IntPtr debugPort;
                int size;
                if (NtQueryInformationProcess(handle, ProcessDebugPortClass, out debugPort, IntPtr.Size, out size) >= 0
                    && debugPort != IntPtr.Zero)
                    reasons.Add("открыт порт отладки процесса");
            }
            catch
            {
            }

            try
            {
                IntPtr handle = GetCurrentProcess();
                var pbi = new ProcessBasicInformation();
                int size;
                if (NtQueryInformationProcess(handle, ProcessBasicInformationClass, ref pbi, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProcessBasicInformation)), out size) >= 0
                    && pbi.PebBaseAddress != IntPtr.Zero)
                {
                    byte beingDebugged = Marshal.ReadByte(pbi.PebBaseAddress, 0x2);
                    if (beingDebugged != 0)
                        reasons.Add("установлен флаг BeingDebugged в PEB");
                }
            }
            catch
            {
            }

            return reasons;
#endif
        }
    }
}
