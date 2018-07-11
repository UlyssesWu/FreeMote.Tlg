#pragma once

using namespace System;
using namespace System::Drawing;
using namespace System::Runtime::CompilerServices;
using namespace System::Runtime::InteropServices;

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

			static array<Byte>^ ToBitmapBytes(array<Byte>^ tlgBytes, [OutAttribute]Int32% bitWidth);
			static Bitmap^ ToBitmap(array<Byte>^ tlgBytes);

			static bool CheckTlg(array<Byte>^ tlgBytes);
			static bool GetInfoTlg(array<Byte>^ tlgBytes, [OutAttribute]Int32% width, [OutAttribute]Int32% height);
		};
	}

}
