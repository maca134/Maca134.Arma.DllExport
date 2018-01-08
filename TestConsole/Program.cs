using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maca134.Arma.DllExport.MsBuild;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            DllExporter.IlasmPath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319";
            DllExporter.IldasmPath = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools";
            var file = @"U:\Projects\a2-beahext\a2-beahext\bin\Release\a2-beahext.dll";
            var ex = new DllExporter(file);
            ex.Export();

        }
    }
}
