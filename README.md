# dnMerge

## Intro

dnMerge is an MSBuild plugin that will merge multiple .NET reference assemblies into a single .NET executable or DLL.  dnMerge can be included within your .NET project using the [NuGet package](https://www.nuget.org/packages/dnMerge/) available from the central repo.

Merged assembiles are compressed with 7-Zip's [LZMA SDK](https://www.7-zip.org/sdk.html) which has the added benefit of smaller executables in comparison with other .NET assembly mergers.  No additional .NET references are including during merging, making dnMerge suitable for cross-compiling on Linux without pulling in .NET Core assembly references into the final merged assembly.

## Usage

Simply add the dnMerge NuGet dependency into your .NET project and compile a release build.  Currently only release builds are merged since debug symbols are lost during merging.

## Customisation

dnMerge supports customising how the merged assembly merged.  Currently you can exclude assemblies from being merged, turn PDB generation on and off and control if the the merged assemlby is overwritten or if it's saved to a new file.  Just create a file called `dnMerge.config` inside the project folder that is using dnMerge.  Example configuration below.

```xml
<dnMergeConfig>
    <GeneratePDB>false</GeneratePDB>
    <OverwriteAssembly>true</OverwriteAssembly>
    <ExcludeReferences>
        <ReferenceName>bofnet.dll</ReferenceName>
    </ExcludeReferences>
</dnMergeConfig>
```

## Credits

* dnMerge uses the brilliant [dnLib](https://github.com/0xd4d/dnlib) library for .NET assembly modifications.  Without this library dnMerge would not be possible.
* 7-Zip LZMA SDK.
