using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Maca134.Arma.DllExport.MsBuild
{
    public class DllExporter
    {
        public static string IlasmPath { get; set; }
        public static string IldasmPath { get; set; }

        private bool _injected;

        public bool FoundMethod => ExportMethod != null;
        public string Target { get; }
        public ModuleDefinition Module { get; }
        public MethodDefinition ExportMethod { get; }

        public string WrapperNamespace { get; set; } = "Maca134.Arma.DllExport";
        public string WrapperTypeName { get; set; } = "DllExportWrapper";
        public string WrapperMethodName { get; set; } = "RVExtension";
        public bool KeepIl { get; set; } = true;
        public Action<string> Log = s => Console.WriteLine(s);

        public bool Debug => Module.HasDebugHeader;
        public CpuPlatform Cpu
        {
            get
            {
                if (Module.Architecture == TargetArchitecture.AMD64)
                    return CpuPlatform.X64;
                if (Module.Architecture == TargetArchitecture.I386 && (Module.Attributes & ModuleAttributes.Required32Bit) != 0)
                    return CpuPlatform.X86;
                return CpuPlatform.AnyCpu;
            }
        }

        public DllExporter(string target)
        {
            Target = target;
            Module = ModuleDefinition.ReadModule(Target);
            if (Module.Kind != ModuleKind.Dll)
                throw new DllExporterException("only supports dlls");
            if (Cpu == CpuPlatform.AnyCpu)
                throw new DllExporterException("AnyCpu dlls not supported");
            foreach (var typeDefinition in Module.Types)
            {
                var methods = typeDefinition.Methods.Where(m => m.HasCustomAttributes && m.CustomAttributes.Any(c => c.AttributeType.Name == "ArmaDllExportAttribute"));
                var methodDefinitions = methods as MethodDefinition[] ?? methods.ToArray();
                if (!methodDefinitions.Any())
                    continue;
                if (methodDefinitions.Length > 1)
                    throw new DllExporterException("you can only have 1 method with the attribute ArmaDllExportAttribute");
                ExportMethod = methodDefinitions[0];
                break;
            }
            if (ExportMethod == null)
                return;
			
            if (ExportMethod.MethodReturnType.ReturnType.FullName != Module.Import(typeof(string)).FullName)
                throw new DllExporterException("The method must return a string");

            if (!ExportMethod.Parameters.Select(p => p.ParameterType.FullName).ToArray().SequenceEqual(new[] { Module.Import(typeof(string)).FullName, Module.Import(typeof(int)).FullName }))
                throw new DllExporterException("The method must have the signature: Method(string, int)");

            if (!ExportMethod.DeclaringType.IsPublic)
                throw new DllExporterException("The class were the export method resides must be public");

            if (!ExportMethod.IsStatic)
                throw new DllExporterException("The export method must be static");

            if (!ExportMethod.IsPublic)
                throw new DllExporterException("The export method must be public");
        }

        public void Export()
        {
            if (_injected)
                throw new DllExporterException("you can only inject into a dll once");
            Log("Injecting wrapper method into dll");
            InjectWrapper();
            Log("Removing ArmaDllExport attribute and reference");
            RemoveArmaExportRefs();

            Log("Writing injected dll");
            Module.Write(Target);
            var ilPath = $"{Target}.il";

            Log("Disassembling dll");
            IlDasm(Target, ilPath);

            Log("Adding export");
            IlParser(ilPath);

            Log("Assembling dll");
            IlAsm(ilPath);

            _injected = true;
            if (KeepIl)
                return;
            Log("Cleaning up");
            try
            {
                File.Delete(ilPath);
                File.Delete(Path.Combine(Path.GetDirectoryName(ilPath), $"{Path.GetFileNameWithoutExtension(ilPath)}.res"));
            }
            catch
            {
                // ignored - failed to delete files...
            }
        }

        private void InjectWrapper()
        {
            if (ExportMethod == null)
                throw new DllExporterException("ExportMethod is null");

            var type = new TypeDefinition(WrapperNamespace, WrapperTypeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed)
            {
                Methods =
                {
                    new MethodDefinition(
                        WrapperMethodName,
                        MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.Public,
                        Module.Import(typeof(void))
                    )
                    {
                        Parameters =
                        {
                            new ParameterDefinition(Module.Import(typeof(StringBuilder)))
                            {
                                Name = "output"
                            },
                            new ParameterDefinition(Module.Import(typeof(int)))
                            {
                                Name = "outputSize"
                            },
                            new ParameterDefinition(Module.Import(typeof(string)))
                            {
                                Name = "input",
                                MarshalInfo = new MarshalInfo(NativeType.LPStr)
                            }
                        },
                        ReturnType = new OptionalModifierType(Module.Import(typeof(System.Runtime.CompilerServices.CallConvStdcall)), Module.Import(typeof(void))),
                    }
                }
            };
            var il = type.Methods[0].Body.GetILProcessor();
            il.Append(Instruction.Create(OpCodes.Nop));
            il.Append(Instruction.Create(OpCodes.Ldarg_0));
            il.Append(Instruction.Create(OpCodes.Ldarg_2));
            il.Append(Instruction.Create(OpCodes.Ldarg_1));
            il.Append(Instruction.Create(OpCodes.Call, ExportMethod));
            il.Append(Instruction.Create(OpCodes.Callvirt, Module.Import(typeof(StringBuilder).GetMethods().Single(m => m.Name == "Append" && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string) })))));
            il.Append(Instruction.Create(OpCodes.Pop));
            il.Append(Instruction.Create(OpCodes.Ret));
            Module.Types.Add(type);
        }

        private void RemoveArmaExportRefs()
        {
            ExportMethod.CustomAttributes.Remove(ExportMethod.CustomAttributes.SingleOrDefault(c => c.AttributeType.Name == "ArmaDllExportAttribute"));
            Module.AssemblyReferences.Remove(Module.AssemblyReferences.First(a => a.Name == "Maca134.Arma.DllExport"));
        }

        private void IlDasm(string dllPath, string ilPath)
        {
            if (IldasmPath == null)
                throw new DllExporterException("ildasm not found, please set ArmaDllExportFrameworkPath and ArmaDllExportSdkPath build properties");
            var ildasm = Path.Combine(IldasmPath, "ildasm.exe");
            if (!File.Exists(ildasm))
                throw new DllExporterException("ildasm not found, please set ArmaDllExportFrameworkPath and ArmaDllExportSdkPath build properties");
            var arguments = string.Format(
                CultureInfo.InvariantCulture,
                "/quoteallnames /unicode /nobar{0}\"/out:{1}\" \"{2}\"",
                Debug ? " /linenum " : "",
                ilPath,
                dllPath
            );
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = ildasm,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                //RedirectStandardOutput = true,
                //RedirectStandardError = true
            }))
            {
                process?.WaitForExit();
            }

        }

        private void IlAsm(string ilPath)
        {
            if (IlasmPath == null)
                throw new DllExporterException("ilasm path is null, please set ArmaDllExportFrameworkPath and ArmaDllExportSdkPath build properties");
            var ilasm = Path.Combine(IlasmPath, "ilasm.exe");
            if (!File.Exists(ilasm))
                throw new DllExporterException("ilasm not found, please set ArmaDllExportFrameworkPath and ArmaDllExportSdkPath build properties");
            var arguments = string.Format(
                CultureInfo.InvariantCulture,
                "/nologo \"/out:{0}\" {2} {4} {5} \"{1}\" \"{3}\"",
                Target,
                ilPath,
                "/" + Path.GetExtension(Target)?.Trim('.', '"').ToUpperInvariant(),
                // ReSharper disable once AssignNullToNotNullAttribute
                Path.Combine(Path.GetDirectoryName(ilPath), $"{Path.GetFileNameWithoutExtension(ilPath)}.res"),
                Debug ? "/debug" : "/optimize",
                Cpu == CpuPlatform.X86 ? "" : "/X64"
            );
            Console.WriteLine(arguments);
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = ilasm,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                //RedirectStandardOutput = true,
                //RedirectStandardError = true
            }))
            {
                process?.WaitForExit();
            }
        }

        private void IlParser(string ilIn)
        {
            var il = File.ReadAllLines(ilIn).ToList();
            for (var i = 0; i < il.Count; i++)
            {
                if (il[i].StartsWith(".corflags "))
                {
                    il[i] = $".corflags 0x{Cpu.GetCorFlags().ToString("X8", CultureInfo.InvariantCulture)}";
                    continue;
                }
                if (!il[i].Contains($"'{WrapperNamespace}'.'{WrapperTypeName}'")) continue;
                i++; // inside type
                i++; // {
                while (il[i].Trim() != "{")
                {
                    i++;
                }
                i++; // {
                il.InsertRange(i, new[]
                {
                    "    .vtentry 1 : 1",
                    Cpu == CpuPlatform.X64 ? "    .export [1] as RVExtension" : "    .export [1] as _RVExtension@12"
                });
                break;
            }
            File.WriteAllLines(ilIn, il);
        }
    }
}