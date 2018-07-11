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
			static Bitmap^ ToBitmap(array<Byte>^ tlgBytes, [OutAttribute]Int32% version, [Optional]bool useUnmanagedScan0);

			static bool CheckTlg(array<Byte>^ tlgBytes);
			static bool GetInfoTlg(array<Byte>^ tlgBytes, [OutAttribute]Int32% width, [OutAttribute]Int32% height, [OutAttribute]Int32% version);
		};

		/// <summary>
		/// E-mote
		/// </summary>
		public ref class TlgLoader : IDisposable
		{
		public:
			TlgLoader(array<Byte>^ tlgBytes);
			/// <summary>
			/// Get bitmap
			/// </summary>
			property Bitmap^ Bitmap
			{
				System::Drawing::Bitmap^ get()
				{
					if (_bmp != nullptr)
					{
						return _bmp;
					}
				}
			}
			/// <summary>
			/// TLG Version
			/// </summary>
			property int Version
			{
				int get()
				{
					return _version;
				}
			}
			/// <summary>
			/// Height
			/// </summary>
			property int Height
			{
				int get()
				{
					return _height;
				}
			}
			/// <summary>
			/// Width
			/// </summary>
			property int Width
			{
				int get()
				{
					return _width;
				}
			}
			/// <summary>
			/// Bytes Per Pixel
			/// </summary>
			property int Colors
			{
				int get()
				{
					return _colors;
				}
			}
			~TlgLoader()
			{
				if (_data != nullptr)
				{
					delete _data;
				}
			}
		private:
			System::Drawing::Bitmap^ GetBitmap();
			System::Drawing::Bitmap^ _bmp;
			unsigned char* _data;
			int _version;
			int _width;
			int _height;
			int _colors = 4;
		};
	}

}
