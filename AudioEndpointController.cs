using System;
using System.Runtime.InteropServices;

namespace ScreenDimmer;

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr notify);
    int UnregisterControlChangeNotify(IntPtr notify);
    int GetChannelCount(out uint channelCount);
    int SetMasterVolumeLevel(float levelDB, ref Guid eventContext);
    int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    int GetMasterVolumeLevel(out float levelDB);
    int GetMasterVolumeLevelScalar(out float level);
    int SetChannelVolumeLevel(uint channelNumber, float levelDB, ref Guid eventContext);
    int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);
    int GetChannelVolumeLevel(uint channelNumber, out float levelDB);
    int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
    int GetVolumeStepInfo(out uint step, out uint stepCount);
    int VolumeStepUp(ref Guid eventContext);
    int VolumeStepDown(ref Guid eventContext);
    int QueryHardwareSupport(out uint hardwareSupportMask);
    int GetVolumeRange(out float volumeMinDB, out float volumeMaxDB, out float volumeIncrementDB);
}

public static class AudioEndpointController
{
    private static readonly Guid IID_IAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;

    public static bool GetIsMuted()
    {
        try
        {
            var volume = GetAudioEndpointVolume();
            if (volume != null)
            {
                volume.GetMute(out bool isMuted);
                Marshal.ReleaseComObject(volume);
                return isMuted;
            }
        }
        catch { }
        return false;
    }

    public static void SetMute(bool mute)
    {
        try
        {
            var volume = GetAudioEndpointVolume();
            if (volume != null)
            {
                var guid = Guid.Empty;
                volume.SetMute(mute, ref guid);
                Marshal.ReleaseComObject(volume);
            }
        }
        catch { }
    }

    private static IAudioEndpointVolume? GetAudioEndpointVolume()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice dev);
            if (dev != null)
            {
                var iid = IID_IAudioEndpointVolume;
                dev.Activate(ref iid, 1, IntPtr.Zero, out object epvObj);
                Marshal.ReleaseComObject(dev);
                Marshal.ReleaseComObject(enumerator);
                return epvObj as IAudioEndpointVolume;
            }
            Marshal.ReleaseComObject(enumerator);
        }
        catch { }
        return null;
    }
}
