#ifndef __handle_stream_h_
#define __handle_stream_h_

#include "stream.h"
#include <windows.h>

/**
 * FileStream
 */
class tHandleStream : public tTJSBinaryStream {

public:
	/**
	 * open Handle (won't dispose)
	 */
	tHandleStream(HANDLE handle);

	/**
	 * open file
	 * @param mode
	 */
	tHandleStream(const char *filename, DWORD mode=GENERIC_READ);

	/**
	* open file
	* @param mode
	*/
	tHandleStream(const wchar_t *filename, DWORD mode = GENERIC_READ);

	~tHandleStream();

	//-- must implement
	virtual tjs_uint64  Seek(tjs_int64 offset, tjs_int whence);
	virtual tjs_uint  Read(void *buffer, tjs_uint read_size);
	virtual tjs_uint  Write(const void *buffer, tjs_uint write_size);
	
private:
	bool release;
	HANDLE handle;
};

#endif
