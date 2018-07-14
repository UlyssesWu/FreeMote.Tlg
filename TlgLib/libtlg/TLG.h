//---------------------------------------------------------------------------
/*
	TVP2 ( T Visual Presenter 2 )  A script authoring tool
	Copyright (C) 2000 W.Dee <dee@kikyou.info> and contributors

	See details of license at "license.txt"
*/
//---------------------------------------------------------------------------
// TLG5/6 decoder/encoder
//---------------------------------------------------------------------------

#ifndef __TLG_H__
#define __TLG_H__

#include "tjs.h"
#include "stream.h"
#include <string>
#include <map>

//---------------------------------------------------------------------------
// Graphic Loading Handler Type
//---------------------------------------------------------------------------

/*
	callback type to inform the image's size.
	call this once before TVPGraphicScanLineCallback.
	return false can stop processing
	color: 1=8bits, 3=24bits, 4=32bits
*/
typedef bool(*tTVPGraphicSizeCallback)(void *callbackdata, tjs_uint width, tjs_uint height, tjs_uint color);

/*
	callback type to ask the scanline buffer for the decoded image, per a line.
	returning null can stop the processing.

	passing of y=-1 notifies the scan line image had been written to the buffer that
	was given by previous calling of TVPGraphicScanLineCallback. in this time,
	this callback function must return NULL.
*/
typedef void * (*tTVPGraphicScanLineCallback)(void *callbackdata, tjs_int y);

//---------------------------------------------------------------------------
// return code
//---------------------------------------------------------------------------

#define TLG_SUCCESS (0)
#define TLG_ABORT   (1)
#define TLG_ERROR  (-1)


//---------------------------------------------------------------------------
// functions
//---------------------------------------------------------------------------

/**
 * src MemoryStream with TLG loaded
 * Check if it's a valid TLG
 */
bool
TVPCheckTLG(tTJSBinaryStream* src);

/**
 * Get TLG image info
 * @param src input stream
 * @param width
 * @parma height
 * @parma version (Added by Ulysses) TLG Version: 0=unknown, 5=v5, 6=v6
 */
extern bool
TVPGetInfoTLG(tTJSBinaryStream* src, int* width, int* height, int* version);

/**
 * Decode TLG image
 * @param src input stream
 * @param callbackdata pass data
 * @param sizecallback get size info
 * @param scanlinecallback output bitmap line data
 * @param tags Tag dictionary
 * @param tlgVersion (Added by Ulysses) TLG Version: 0=unknown, 5=v5, 6=v6
 * @return 0:success 1:break -1:error
 */
extern int
TVPLoadTLG(void *callbackdata,
	tTVPGraphicSizeCallback sizecallback,
	tTVPGraphicScanLineCallback scanlinecallback,
	std::map<std::string, std::string> *tags,
	tTJSBinaryStream *src, int* tlgVersion = 0);

/**
 * Encode TLG image
 * @param dest output stream
 * @param type Type 0:TLG5 1:TLG6
 * @parma width
 * @param height
 * @param colors 1:8bit Gray 3:RGB 4:RGBA
 * @param callbackdata pass data
 * @param scanlinecallback pass bitmap line data
 * @param tags Tag dictionary
 * @return 0:success 1:break -1:error
 */
extern int
TVPSaveTLG(tTJSBinaryStream *dest,
	int type,
	int width, int height, int colors,
	void *callbackdata,
	tTVPGraphicScanLineCallback scanlinecallback,
	const std::map<std::string, std::string> *tags);

#endif
