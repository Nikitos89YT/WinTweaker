using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinTweaker.Services;

// Windows counts frames presented by each process on each video present source.
internal sealed class ProcessFrameCounter : IDisposable
{
    private readonly SafeProcessHandle _process;
    private readonly List<Source> _sources = [];
    private long _lastSample;

    private sealed class Source(Adapter adapter, uint id)
    {
        public readonly Adapter Adapter = adapter;
        public readonly uint Id = id;
        public uint? Previous;
    }

    public bool HasFrames { get; private set; }
    public bool IsRunning => !_process.IsInvalid && GetExitCodeProcess(_process, out uint code) && code == 259;

    public ProcessFrameCounter(int pid)
    {
        _process = OpenProcess(0x0400, false, pid); // PROCESS_QUERY_INFORMATION
        if (_process.IsInvalid || RuntimeInformation.ProcessArchitecture != Architecture.X64) return;
        var adapters = new AdapterList { Items = new Adapter[16] };
        if (D3DKMTEnumAdapters(ref adapters) != 0) return;
        foreach (var adapter in adapters.Items.Take((int)adapters.Count))
        {
            try
            {
                for (uint id = 0; id < adapter.SourceCount; id++)
                    _sources.Add(new Source(adapter, id));
            }
            finally
            {
                uint handle = adapter.Handle;
                D3DKMTCloseAdapter(ref handle);
            }
        }
        TakeFps(); // Establish a baseline before measuring the first interval.
    }

    public double? TakeFps()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = Stopwatch.GetElapsedTime(_lastSample, now).TotalSeconds;
        _lastSample = now;
        uint maximum = 0;
        foreach (var source in _sources)
        {
            var query = new Statistics
            {
                Type = 8, // D3DKMT_QUERYSTATISTICS_PROCESS_VIDPNSOURCE
                LuidLow = source.Adapter.LuidLow, LuidHigh = source.Adapter.LuidHigh,
                Process = _process.DangerousGetHandle(), SourceId = source.Id
            };
            if (D3DKMTQueryStatistics(ref query) != 0)
            {
                source.Previous = null;
                continue;
            }
            if (source.Previous is uint previous && query.Frame >= previous)
                maximum = Math.Max(maximum, query.Frame - previous);
            source.Previous = query.Frame;
            HasFrames |= query.Frame != 0;
        }
        // Hybrid adapters can report the same frames on multiple sources.
        return maximum > 0 && elapsed > 0 ? maximum / elapsed : null;
    }

    public void Dispose() => _process.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct Adapter { public uint Handle, LuidLow, LuidHigh, SourceCount; public int PreciseRegions; }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterList
    {
        public uint Count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public Adapter[] Items;
    }

    // x64 layout of D3DKMT_QUERYSTATISTICS from d3dkmthk.h. The result union
    // begins with PROCESS_VIDPNSOURCE_INFORMATION.Frame for this query type.
    [StructLayout(LayoutKind.Explicit, Size = 0x328)]
    private struct Statistics
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint LuidLow;
        [FieldOffset(8)] public uint LuidHigh;
        [FieldOffset(16)] public IntPtr Process;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(0x320)] public uint SourceId;
    }

    [DllImport("gdi32.dll")] private static extern int D3DKMTEnumAdapters(ref AdapterList adapters);
    [DllImport("gdi32.dll")] private static extern int D3DKMTCloseAdapter(ref uint handle);
    [DllImport("gdi32.dll")] private static extern int D3DKMTQueryStatistics(ref Statistics query);
    [DllImport("kernel32.dll")] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);
}
