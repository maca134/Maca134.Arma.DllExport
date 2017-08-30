using System.Security.Permissions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Maca134.Arma.DllExport.MsBuild
{
    [LoadInSeparateAppDomain]
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class ArmaDllExportTask : AppDomainIsolatedTask
    {
        [Required]
        public string FileName { get; set; }

        [Required]
        public string FrameworkPath { get; set; }

        [Required]
        public string SdkPath { get; set; }

        public string WrapperNamespace { get; set; } = "Maca134.Arma.DllExport";

        public string WrapperTypeName { get; set; } = "DllExportWrapper";

        public string WrapperMethodName { get; set; } = "RVExtension";

        public bool KeepIl { get; set; }

        public override bool Execute()
        {
            DllExporter.IldasmPath = SdkPath;
            DllExporter.IlasmPath = FrameworkPath;
            DllExporter dll;
            try
            {
                dll = new DllExporter(FileName);
            }
            catch (DllExporterException ex)
            {
                Log.LogError("There was a problem initialising the exporter:");
                Log.LogErrorFromException(ex);
                return false;
            }
            if (!dll.FoundMethod)
            {
                Log.LogMessage("No export method was found - did you forget the ArmaDllExport attribute?");
                return true;
            }
            dll.KeepIl = KeepIl;
            dll.WrapperNamespace = WrapperNamespace;
            dll.WrapperTypeName = WrapperTypeName;
            dll.WrapperMethodName = WrapperMethodName;
            dll.Log = s => Log.LogMessage(s);
            try
            {
                dll.Export();
            }
            catch (DllExporterException ex)
            {
                Log.LogError("There was a problem exporting:");
                Log.LogErrorFromException(ex);
                return false;
            }
            return true;
        }
    }
}
