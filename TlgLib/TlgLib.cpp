#include "stdafx.h"
#include <string>
#include <sstream>
#include "libtlg\TLG.h"
#include "libtlg\stream.h"
#include "TlgLib.h"

using namespace System::Drawing;
using namespace System::Drawing::Imaging;
extern tTJSBinaryStream *GetMemoryStream();

public struct BmpData
{
	int Stride;
	int Width;
	int Height;
	char* Data;
};

static void *scanLineCallback(void *callbackdata, int y)
{
	if (y == -1)
	{
		return NULL;
	}
	BmpData* data = (BmpData*)(callbackdata);
	return data->Data + data->Stride * y;
}

//static bool sizeCallback(void *callbackdata, unsigned int w, unsigned int h) {
//	TLG_FrameDecode *decoder = (TLG_FrameDecode*)callbackdata;
//	return decoder->setSize(w, h);
//}


array<Byte>^ SaveTlg(Bitmap^ bmp, int type = 1)
{
	tTJSBinaryStream* output = GetMemoryStream();
	//1:8bit 3:RGB 4:RGBA
	int pxFormat = 4;
	switch (bmp->PixelFormat)
	{
	case PixelFormat::Format8bppIndexed:
		pxFormat = 1;
		break;
	case PixelFormat::Format24bppRgb:
		pxFormat = 3;
		break;
	default:
		break;
	}

	BitmapData^ bmpData = bmp->LockBits(Rectangle(0, 0, bmp->Width, bmp->Height),
		ImageLockMode::ReadOnly, bmp->PixelFormat);
	int stride = bmpData->Stride;
	IntPtr scan0 = bmpData->Scan0;
	int scanBytes = stride * bmpData->Height;
	BmpData data = BmpData();
	data.Width = bmpData->Width;
	data.Height = bmpData->Height;
	data.Stride = bmpData->Stride;
	data.Data = new char[scanBytes];
	memcpy(data.Data, scan0.ToPointer(), scanBytes);
	bmp->UnlockBits(bmpData);

	std::map<std::string, std::string> tags;
	tags.insert(std::pair<std::string, std::string>("Software", "FreeMote"));

	//Demonstrating Tag building
	//std::stringstream ss;
	//std::map<std::string, std::string>::const_iterator it = tags.begin();
	//while (it != tags.end()) {
	//	ss << it->first.length() << ":" << it->first << "=" << it->second.length() << ":" << it->second << ",";
	//	it++;
	//}
	//std::string s = ss.str();

	int state = TVPSaveTLG(output, type, bmp->Width, bmp->Height, pxFormat, &data, scanLineCallback, &tags);

	if (state == 0)
	{
		int len = output->Seek(0, TJS_BS_SEEK_END);
		output->Seek(0, TJS_BS_SEEK_SET);
		array<Byte>^ buf = gcnew array<Byte>(len);
		pin_ptr<unsigned char> nativeBuf = &buf[0];
		output->ReadBuffer(nativeBuf, len);
		return buf;
	}

	return nullptr;
}

array<Byte>^ FreeMote::Tlg::TlgNative::ToTlg6(Bitmap^ bmp)
{
	return SaveTlg(bmp, 1);
}

array<Byte>^ FreeMote::Tlg::TlgNative::ToTlg5(Bitmap^ bmp)
{
	return SaveTlg(bmp, 0);
}




