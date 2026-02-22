OutwardDynasty - Build Setup

This project references Outward + BepInEx assemblies from YOUR machine.

1) Edit: OutwardDynasty/Directory.Build.props
   Set these three paths:

   OutwardManagedDir  -> ...\Outward Definitive Edition_Data\Managed
   BepInExCoreDir     -> ...\BepInEx\core
   BepInExPluginsDir  -> ...\BepInEx\plugins  (optional; only needed if you want optional compat refs)

2) Build in Visual Studio (TargetFrameworkVersion is set to v4.7.2).

Notes
- The Libs/ folder contains the mod-compat DLLs you provided, referenced locally.
- Some optional plugin references (ConfigurationManager, SideLoader, PvP, etc.) are referenced from BepInExPluginsDir ONLY if the DLL exists.
