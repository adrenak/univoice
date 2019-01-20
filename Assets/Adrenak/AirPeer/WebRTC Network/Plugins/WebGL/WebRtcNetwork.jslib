/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
 
var UnityWebRtcNetwork =
{
	UnityWebRtcNetworkIsAvailable:function()
    {
		if(typeof CAPIWebRtcNetworkIsAvailable === 'function')
		{
			return CAPIWebRtcNetworkIsAvailable();
		}
		return false;
    },
	UnityWebRtcNetworkCreate:function(lConfiguration)
	{
		return CAPIWebRtcNetworkCreate(Pointer_stringify(lConfiguration));
	},
	UnityWebRtcNetworkRelease:function(lIndex)
	{
		CAPIWebRtcNetworkRelease(lIndex);
	},
	UnityWebRtcNetworkConnect:function(lIndex, lRoom)
	{
		return CAPIWebRtcNetworkConnect(lIndex, Pointer_stringify(lRoom));
	},
	UnityWebRtcNetworkStartServer:function(lIndex, lRoom)
	{
		CAPIWebRtcNetworkStartServer(lIndex, Pointer_stringify(lRoom));
	},
	UnityWebRtcNetworkStopServer:function(lIndex)
	{
		CAPIWebRtcNetworkStopServer(lIndex);
	},
	UnityWebRtcNetworkDisconnect:function(lIndex, lConnectionId)
	{
		CAPIWebRtcNetworkDisconnect(lIndex, lConnectionId);
	},
	UnityWebRtcNetworkShutdown:function(lIndex)
	{
		CAPIWebRtcNetworkShutdown(lIndex);
	},
	UnityWebRtcNetworkUpdate:function(lIndex)
	{
		CAPIWebRtcNetworkUpdate(lIndex);
	},
	UnityWebRtcNetworkFlush:function(lIndex)
	{
		CAPIWebRtcNetworkFlush(lIndex);
	},
	UnityWebRtcNetworkSendData:function(lIndex, lConnectionId, lUint8ArrayDataPtr, lUint8ArrayDataOffset, lUint8ArrayDataLength, lReliable)
	{
		var sndReliable = true;
		if(lReliable == false || lReliable == 0 || lReliable == "false" || lReliable == "False")
			sndReliable = false;
		CAPIWebRtcNetworkSendDataEm(lIndex, lConnectionId, HEAPU8, lUint8ArrayDataPtr + lUint8ArrayDataOffset, lUint8ArrayDataLength, sndReliable);
	},
	UnityWebRtcNetworkPeekEventDataLength:function(lIndex)
	{
		return CAPIWebRtcNetworkPeekEventDataLength(lIndex);
	},
	UnityWebRtcNetworkDequeue:function(lIndex, lTypeIntArrayPtr, lConidIntArrayPtr, lUint8ArrayDataPtr, lUint8ArrayDataOffset, lUint8ArrayDataLength, lDataLenIntArrayPtr )
	{
		var val = CAPIWebRtcNetworkDequeueEm(lIndex, HEAP32, lTypeIntArrayPtr >> 2, HEAP32, lConidIntArrayPtr >> 2, HEAPU8, lUint8ArrayDataPtr + lUint8ArrayDataOffset, lUint8ArrayDataLength, HEAP32, lDataLenIntArrayPtr >> 2);
		return val;
	},
	UnityWebRtcNetworkPeek:function(lIndex, lTypeIntArrayPtr, lConidIntArrayPtr, lUint8ArrayDataPtr, lUint8ArrayDataOffset, lUint8ArrayDataLength, lDataLenIntArrayPtr )
	{
		var val = CAPIWebRtcNetworkPeekEm(lIndex, HEAP32, lTypeIntArrayPtr >> 2, HEAP32, lConidIntArrayPtr >> 2, HEAPU8, lUint8ArrayDataPtr + lUint8ArrayDataOffset, lUint8ArrayDataLength, HEAP32, lDataLenIntArrayPtr >> 2);
		return val;
	}
};

mergeInto(LibraryManager.library, UnityWebRtcNetwork);