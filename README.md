# dnMerge

## Intro

dnMerge is an MSBuild plugin that will merge multiple .NET reference assemblies into a single .NET executable or DLL.  dnMerge can be included within your .NET project using the [NuGet package](https://www.nuget.org/packages/dnMerge/) available from the central repo.

Merged assembiles are compressed with 7-Zip's [LZMA SDK](https://www.7-zip.org/sdk.html) which has the added benefit of smaller executables in comparison with other .NET assembly mergers.  No additional .NET references are including during merging, making dnMerge suitable for cross-compiling on Linux without pulling in .NET Core assembly references into the final merged assembly.

## Usage

Simply add the dnMerge NuGet dependency into your .NET project and compile a release build.  Currently only release builds are merged since debug symbols are lost during merging.

## Credits

* dnMerge uses the brilliant [dnLib](https://github.com/0xd4d/dnlib) library for .NET assembly modifications.  Without this library dnMerge would not be possible.
* 7-Zip LZMA SDK.
