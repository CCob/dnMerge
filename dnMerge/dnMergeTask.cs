﻿using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace dnMerge
{
    public class dnMergeTask : Task {

        public string AssemblyFile { get; set; }
        public string ProjectDirectory { get; set; }  
        public string OutputPath { get; set; }

        public string[] MergeClassNamespace = {
            "SevenZip"
        };

        public override bool Execute() {
            return ExecuteTask();
        }

        private void MergeClasses(ModuleDefMD fromModule, ModuleDefMD toModule) {
            
            foreach(var ns in MergeClassNamespace) {
                var mergeTypes = fromModule.GetTypes().Where(t => t.Namespace.StartsWith(ns) ||  t.Namespace.StartsWith($"{ns}.")).ToList();
                foreach (var mergeType in mergeTypes) {
                    fromModule.Types.Remove(mergeType);
                    toModule.Types.Add(mergeType);
                }
            }

            //Bit of an ugly hack to pull in static initializers
            var privateInit = fromModule.GetTypes().Where(t => t.Name == "<PrivateImplementationDetails>").FirstOrDefault();
            fromModule.Types.Remove(privateInit);
            toModule.Types.Add(privateInit);
        }

        private void InjectDependencyClasses(ModuleDefMD fromModule, ModuleDefMD toModule) {

            //First attach our ModuleLoader class
            var modLoaderType = fromModule.GetTypes().Where(t => t.FullName == "ModuleLoader").FirstOrDefault();
            fromModule.Types.Remove(modLoaderType);
            toModule.Types.Add(modLoaderType);

            //Now create a static module constructor that will be responsible
            //for registering AssemblyResolve event and loading our bundled assembiles
            var ctor = toModule.GlobalType.FindOrCreateStaticConstructor();
            var attachDef = modLoaderType.Methods.Where(m => m.Name == "Attach").FirstOrDefault();
            ctor.Body = new CilBody();
            ctor.Body.Instructions.Add(OpCodes.Call.ToInstruction(attachDef));
            ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());

            //Finally merge all other generic class dependencies (mainly SevenZip stuff)
            MergeClasses(fromModule, toModule);
        }

        private void BuildAssemblyList(HashSet<string> assemblies, string assemblyPath) {

            if (!File.Exists(assemblyPath))
                return;

            ModuleDefMD module = ModuleDefMD.Load(File.ReadAllBytes(assemblyPath));
            var assemblyRefs = module.GetAssemblyRefs();

            foreach (var assemblyRef in assemblyRefs) {
                if (!assemblies.Contains(assemblyRef.Name.ToLower())) {
                    assemblies.Add(assemblyRef.Name.ToLower());
                    var referenceAssemblyPath = Path.Combine(Path.GetDirectoryName(assemblyPath), assemblyRef.Name + ".dll");
                    BuildAssemblyList(assemblies, referenceAssemblyPath);
                }
            }
        }

        private void ProcessAssembly(string assemblyPath, ModuleDefMD module) {
            
            var assemblyReferences = new HashSet<string>();
            BuildAssemblyList(assemblyReferences, assemblyPath);

            foreach (var referenceAssembly in assemblyReferences) {
                var referenceAssemblyPath = Path.Combine(Path.GetDirectoryName(assemblyPath), referenceAssembly + ".dll");
                Log.LogMessage($"Attempting to merge assembly {assemblyPath}");

                if (File.Exists(referenceAssemblyPath)) {
                    module.Resources.Add(new EmbeddedResource(referenceAssembly, SevenZipHelper.Compress(File.ReadAllBytes(referenceAssemblyPath))));
                    Log.LogMessage($"Merged assembly {referenceAssembly}");

                } else {
                    Log.LogMessage($"Could not find assembly {referenceAssembly}, skipping.");
                }
            }                                                     
        }

        public bool ExecuteTask() {

            //Calculate where our assembly exists
            var assemblyFileName = Path.GetFileName(AssemblyFile);
            var fullAssemblyPath = Path.Combine(ProjectDirectory, OutputPath, assemblyFileName);

            //Load modules needed for merging
            ModuleDefMD module = ModuleDefMD.Load(File.ReadAllBytes(fullAssemblyPath));
            ModuleDefMD thisMod = ModuleDefMD.Load(typeof(dnMergeTask).Module);

            //Inject dependant classes
            InjectDependencyClasses(thisMod, module);

            //Recursively merge all assembly references
            ProcessAssembly(fullAssemblyPath, module);

            //Save our updated module
            var moduleOptions = new ModuleWriterOptions(module);
            module.Write(fullAssemblyPath, moduleOptions);

            return true;
        }
    }
}