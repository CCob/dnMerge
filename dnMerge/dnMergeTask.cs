using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace dnMerge
{
    public class dnMergeTask : Task {

        [Required]
        public string Configuration { get; set; }
        [Required]
        public string AssemblyFile { get; set; }
        [Required]
        public string ProjectDirectory { get; set; }
        [Required]
        public string OutputPath { get; set; }
        [Required]
        public string DependenciesAssembly { get; set; }
        [Required]
        public ITaskItem[] ReferenceCopyLocalPaths { get; set; }
        public TaskLoggingHelper Logger { get; set; }
        public bool OverwriteAssembly { get; set; } = true;

        public dnMergeConfig MergeConfig { get; private set; } = new dnMergeConfig();        

        public string[] MergeClassNamespace = {
            "SevenZip"
        };

        public override bool Execute() {
            Logger = Log;
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

            //Pull in static initializers that SevenZip relies on
            var privateInit = fromModule.GetTypes().Where(t => t.Name == "<PrivateImplementationDetails>").FirstOrDefault();
            fromModule.Types.Remove(privateInit);

            var targetPrivateInit = toModule.GetTypes().Where(t => t.Name == "<PrivateImplementationDetails>").FirstOrDefault();            
            if (targetPrivateInit == null) {
                //If our target module doesn't have the <PriviateImplementationDetails> then
                //simply copy the entire type accross
                toModule.Types.Add(privateInit);
            } else {

                //<PrivateImplementationDetails> already exists so just copy the fields
                //across into the existing type instead.
                var fieldsToCopy = new List<FieldDef>();
                foreach(var field in privateInit.Fields) {
                    fieldsToCopy.Add(field);
                }
                fieldsToCopy.ForEach(field => {
                    field.DeclaringType = null;
                    targetPrivateInit.Fields.Add(field);
                });

                //Also copy nested types across
                var typesToCopy = new List<TypeDef>();
                foreach (var type in privateInit.NestedTypes) {
                    typesToCopy.Add(type);
                }
                typesToCopy.ForEach(type => {
                    type.DeclaringType = null;
                    targetPrivateInit.NestedTypes.Add(type);
                });
            }
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


        private void ProcessAssembly(ModuleDefMD module) {

            ReferenceCopyLocalPaths
               .Select(x => x.ItemSpec)
               .Where(referenceCopyLocalFile => !MergeConfig.ExcludeReferences.Select(v => v.ToLower()).Any(excludedRef => referenceCopyLocalFile.ToLower().EndsWith(excludedRef))).ToList()
               .ForEach(referenceCopyLocalFile => {
                   if (referenceCopyLocalFile.ToLower().EndsWith(".dll")) {
                       try {
                           var referenceAssemblyData = File.ReadAllBytes(referenceCopyLocalFile);
                           var refModule = ModuleDefMD.Load(referenceAssemblyData);
                           module.Resources.Add(new EmbeddedResource(refModule.Assembly.Name.ToLower(), SevenZipHelper.Compress(referenceAssemblyData)));
                           Logger.LogMessage($"Merged assembly {referenceCopyLocalFile}");
                       } catch (Exception e) {
                           Log.LogMessage(MessageImportance.High, $"Failed to merge assembly {referenceCopyLocalFile} with error {e.Message}");
                       }
                   }
               });
        }

        public bool ExecuteTask() {

            if(Configuration == "Debug") {
                Logger.LogMessage($"Skipping merging for debug build");
                return true;
            }

            //Calculate where our assembly exists
            var assemblyFileName = Path.GetFileName(AssemblyFile);
            var fullAssemblyPath = Path.Combine(ProjectDirectory, OutputPath, assemblyFileName);
            var configFile = Path.Combine(ProjectDirectory, "dnMerge.config");

            if (!File.Exists(fullAssemblyPath)) {
                Logger.LogError($"Cannot find assembly at expected location {fullAssemblyPath}");
                return false;
            }

            if (File.Exists(configFile)) {
                XmlSerializer serializer = new XmlSerializer(typeof(dnMergeConfig));
                MergeConfig = (dnMergeConfig)serializer.Deserialize(new FileStream(configFile, FileMode.Open, FileAccess.Read));
            }            

            //Load modules needed for merging
            ModuleDefMD module = ModuleDefMD.Load(File.ReadAllBytes(fullAssemblyPath));
            ModuleDefMD thisMod = ModuleDefMD.Load(File.ReadAllBytes(DependenciesAssembly));

            //Inject dependant classes
            InjectDependencyClasses(thisMod, module);
            Logger.LogMessage($"Injected dependant classes into {fullAssemblyPath}");

            //Merge copy local assemblies
            ProcessAssembly(module);

            //Save our updated module
            var moduleOptions = new ModuleWriterOptions(module);
            moduleOptions.WritePdb = MergeConfig.GeneratePDB;

            if(MergeConfig.OverwriteAssembly)
                module.Write(fullAssemblyPath, moduleOptions);
            else
                module.Write(fullAssemblyPath + ".merged", moduleOptions);

            return true;
        }
    }
}
