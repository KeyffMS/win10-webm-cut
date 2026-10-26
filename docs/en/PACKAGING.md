# Local publishing and packaging

[Documentation](README.md) · [Polski](../PACKAGING.md)

These instructions are intended for maintainers creating their own distributions. The repository includes a packaging script and allowlist, **not** a hosted installer builder.

## Variants

**Framework-dependent:** the destination computer needs .NET 10 Desktop Runtime and external FFmpeg/FFprobe.

**Self-contained win-x64:** the published files include the .NET runtime, but ffmpeg.exe and ffprobe.exe must still be installed separately or intentionally placed in the tools directory next to the EXE. Single-file publishing does not embed separate documentation and legal notices in the same EXE.

## Publish

~~~powershell
dotnet publish .\src\Win10WebmCut\Win10WebmCut.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\win-x64
~~~

Documentation files are copied beside the executable using ExcludeFromSingleFile=true. The authoritative package allowlist is [package-manifest.json](../package-manifest.json), including root README, both language documentation sets, LICENSE, PRIVACY, THIRD-PARTY-NOTICES and package policy.

## Required licensing/provenance

The packaging script expects a local JSON record of the real runtime **version, origin, variant** and paths to four matching legal files:

- DOTNET-LICENSE.txt
- DOTNET-THIRD-PARTY-NOTICES.txt
- WINDOWSDESKTOP-LICENSE.txt
- WINDOWSDESKTOP-THIRD-PARTY-NOTICES.txt

When bundling FFmpeg tools, supply a separate JSON with actual **version, origin, variant, configuration** and matching **LICENSE.txt, NOTICES.txt, SOURCE-COMPLIANCE.txt**. These must describe the actual third-party distribution. The script can check required fields and files, **not** establish legal compliance.

## Assemble a new package

After publishing and preparing runtime-provenance.local.json with actual information:

~~~powershell
.\scripts\Prepare-Package.ps1 -PublishDirectory .\artifacts\win-x64 -Destination .\artifacts\package-win-x64 -RuntimeProvenance .\runtime-provenance.local.json -SourceRevision '<actual-40-character-source-SHA>'
~~~

To bundle ffmpeg.exe and ffprobe.exe, add **-ToolsDirectory** and **-FfmpegProvenance**. The destination must not already exist or be located inside the publish directory.

The script uses an allowlist, rejects unexpected files and reparse points, copies matching components, computes SHA256 and produces PACKAGE-CONTENTS.json. It does not download, build or execute binaries, and does not overwrite a previous package. On failure, it only cleans up its own newly created staging directory.

Do not include .git, logs, dumps, source media, private keys, PDB files or unrelated user directories. Review real binary versions and license obligations before sharing. Test startup and an appropriate export on a target machine: file hashes alone are not functional tests or legal certification. Additional information: [Third-party notices](../../THIRD-PARTY-NOTICES.md#english).
