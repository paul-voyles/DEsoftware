// DeInterface.NET.h

#pragma once

#include "..\DeInterface.Win32\DeInterface.Win32.h"
#include "DeError.h"

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;
using namespace System::Drawing;
/**
<summary>
In Visual Studio, add a reference to DEInterface.NET.  
</summary>
   <example>
	<code language="cpp">
	using namespace DeInterface;
	</code>
	<code language="cs">
	using DeInterface;
	</code>
	</example>
*/
namespace DeInterface {

	/**
	<summary>
	This class is used to connect to a camera, get and set it's properties, and get and set it's name.
	</summary>
	*/
	public ref class DeInterfaceNET
	{
	public:
		DeInterfaceNET();
		!DeInterfaceNET();
		~DeInterfaceNET();

		bool connect(String^ ip, int rPort, int wPort);
		bool close();
		bool isConnected();

		bool SetProperty(String^ prop, String^ val);
		bool SetCameraName(String^ name);		

		bool GetCameraNames(List<String^>^% cameras);
		bool GetPropertyNames(List<String^>^% props);
		bool GetProperty(String^ prop, String^% value);
		bool GetImage([Out]array<UInt16, 1> ^%image);
		bool IsInLiveMode();
		bool EnableLiveMode();
		bool DisableLiveMode();
		DeError^ GetLastError();
	private:
		DeInterfaceWin32 *unmanaged;
		bool connected;
	};
}
