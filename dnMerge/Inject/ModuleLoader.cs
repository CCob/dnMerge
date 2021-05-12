using System;
using System.Reflection;
using System.IO;
using SevenZip.Compression.LZMA;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;

internal class ModuleLoader {

    private static int isAttached;

    private static object nullCacheLock = new object();

    private static Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    public static void Attach() {
        
        if (Interlocked.Exchange(ref ModuleLoader.isAttached, 1) == 1) {
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    private static string CultureToString(CultureInfo culture) {
        if (culture == null) {
            return "";
        }
        return culture.Name;
    }

    private static Assembly ReadExistingAssembly(AssemblyName name) {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            AssemblyName name2 = assembly.GetName();
            if (string.Equals(name2.Name, name.Name, StringComparison.InvariantCultureIgnoreCase) && string.Equals(ModuleLoader.CultureToString(name2.CultureInfo), ModuleLoader.CultureToString(name.CultureInfo), StringComparison.InvariantCultureIgnoreCase)) {
                return assembly;
            }
        }
        return null;
    }

    static Assembly ReadAssemblyFromResource(AssemblyName assemblyName) {

        var resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assemblyName.Name.ToLower());
        if (resStream == null) {
            return null;
        }

        var assemblyStream = new BinaryReader(resStream);        
        byte[] data = SevenZipHelper.Decompress(assemblyStream.ReadBytes((int)assemblyStream.BaseStream.Length));
        return Assembly.Load(data);        
    }

    static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {

        object obj = ModuleLoader.nullCacheLock;
        lock (obj) {
            if (ModuleLoader.nullCache.ContainsKey(args.Name)) {
                return null;
            }
        }
        AssemblyName assemblyName = new AssemblyName(args.Name);
        Assembly assembly = ModuleLoader.ReadExistingAssembly(assemblyName);
        if (assembly != null) {
            return assembly;
        }

        assembly = ReadAssemblyFromResource(assemblyName);

        if (assembly == null) {
            obj = ModuleLoader.nullCacheLock;
            lock (obj) {
                ModuleLoader.nullCache[args.Name] = true;
            }
            if ((assemblyName.Flags & AssemblyNameFlags.Retargetable) != AssemblyNameFlags.None) {
                assembly = Assembly.Load(assemblyName);
            }
        }
        return assembly;
    }
}