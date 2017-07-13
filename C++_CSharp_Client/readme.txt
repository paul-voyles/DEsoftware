Direct Electron C++ & C# Examples
Tested in Visual Studio 2008 and 2010

Three examples are provided: 
1. DeExampleWin32: C++ program to connect to DirectElectronAPI server, query camera properties and get image; 

2. DeExampleCSharp: C# program to connect to DirectElectronAPI server, query camera properties and get image; 

3. DeExampleCSharpWPF: C# program with a live image view; 


Notes for custom development and deployment: 

For C++ programs:
Build with the static DeInterface.Win32.lib, or copy DeInterface.Win32.dll to the executable folder in order to run with dynamic link. For best performance on x64 machines, please use 64bit lib/dll version whenever possible. 

For C# programs:
Copy relevant DeInterface.NET.dll and DeInterface.Win32.dll to the executable folder in order to run. 
For best performance on x64 machines, please use 64bit lib/dll version whenever possible. 
Add a reference in the project to DEInterface.NET (path = \lib\Win32\Release or \lib\x64\Release)

For .NET 4.5, use DeInterface.NET45.dll instead. 
