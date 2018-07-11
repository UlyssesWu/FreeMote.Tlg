#pragma once

using namespace System;
using namespace System::Drawing;
using namespace System::Runtime::CompilerServices;

namespace FreeMote
{
	namespace Tlg
	{
		[ExtensionAttribute]
		static public ref class TlgNative
		{
		public:
			[ExtensionAttribute]
			static array<Byte>^ ToTlg6(Bitmap^ bmp);
			[ExtensionAttribute]
			static array<Byte>^ ToTlg5(Bitmap^ bmp);
		};
	}

}
