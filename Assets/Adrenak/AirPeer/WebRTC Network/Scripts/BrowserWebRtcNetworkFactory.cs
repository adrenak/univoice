using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Byn.Net
{
    public class BrowserWebRtcNetworkFactory : IWebRtcNetworkFactory
    {
        private bool disposedValue = false;


        public IBasicNetwork CreateDefault(string websocketUrl, IceServer[] lIceServers)
        {
            
            return new BrowserWebRtcNetwork(websocketUrl, lIceServers);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //free managed
                }
                //free unmanaged

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }

    }
}
