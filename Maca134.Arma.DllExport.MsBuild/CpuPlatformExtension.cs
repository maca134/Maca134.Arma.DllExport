using System;

namespace Maca134.Arma.DllExport.MsBuild
{
    internal static class CpuPlatformExtension
    {
        internal static int GetCorFlags(this CpuPlatform cpu)
        {
            switch (cpu)
            {
                case CpuPlatform.X86:
                    return 2;
                case CpuPlatform.X64:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cpu), cpu, null);
            }
        }
    }
}