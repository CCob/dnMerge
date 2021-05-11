using System;
using System.Reflection;
using System.IO;
using SevenZip.Compression.LZMA;

internal class ModuleLoader {

    public static void Attach() {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {

        var assemblyName = new AssemblyName(args.Name);
        var resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assemblyName.Name);
        if (resStream == null) {
            return null;
        }

        var assemblyStream = new BinaryReader(resStream);

        if (assemblyStream.BaseStream != null) {
            byte[] data = SevenZipHelper.Decompress(assemblyStream.ReadBytes((int)assemblyStream.BaseStream.Length));
            return Assembly.Load(data);
        } else {
            return null;
        }
    }
}