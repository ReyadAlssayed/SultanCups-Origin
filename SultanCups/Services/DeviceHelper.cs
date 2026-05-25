using Microsoft.Win32;

namespace SultanCups.Services
{
    public static class DeviceHelper
    {
        public static string GetMachineGuid()
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                "MachineGuid",
                ""
            )?.ToString() ?? "";
        }
    }
}