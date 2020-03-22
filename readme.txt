Direct Electron image acquisition software for 4D STEM

Developed in Visual Studio 2015

Three folders under C++_CSharp_Client:

1. examples

	1.1. DeExampleWin32: C++ program to connect to DirectElectronAPI server, query camera properties and get image, original release from DE; 

	1.2. DeExampleCSharp: C# program to connect to DirectElectronAPI server, query camera properties and get image, original release from DE; 

	1.3. DeExampleCSharpWPF: C# program with a live image view, extended by Chenyu Zhang to work for 4D STEM, details can be found in  DeExampleCSharpWPF/Documents; 

2. include: header files that is required to connect to DE server

3. lib: libraries to support image and parameter transport from DE server



Notes for custom development and deployment: 

For C++ programs:
Build with the static DeInterface.Win32.lib, or copy DeInterface.Win32.dll to the executable folder in order to run with dynamic link. For best performance on x64 machines, please use 64bit lib/dll version whenever possible. 

For C# programs:
Copy relevant DeInterface.NET.dll and DeInterface.Win32.dll to the executable folder in order to run. 
For best performance on x64 machines, please use 64bit lib/dll version whenever possible. 
Add a reference in the project to DEInterface.NET (path = \lib\Win32\Release or \lib\x64\Release)

For .NET 4.5, use DeInterface.NET45.dll instead. 
