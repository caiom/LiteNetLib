using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LiteNetLib
{
    public class NetSocket
    {
        private const int BufferSize = 131071;
        private readonly byte[] _receiveBuffer = new byte[NetConstants.MaxPacketSize];
        private readonly Socket _udpSocket;               //Udp socket

#if NETFX_CORE
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly SocketAsyncEventArgs _sendSocketArgs = new SocketAsyncEventArgs();
        private readonly SocketAsyncEventArgs _receiveSocketArgs = new SocketAsyncEventArgs();

        private void EventComplete(object o, SocketAsyncEventArgs args)
        {
            _autoResetEvent.Set();
        }
#endif

        //Socket constructor
        public NetSocket()
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
#if NETFX_CORE
            _udpSocket.Ttl = 255;
#else
            _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);
            _udpSocket.Blocking = false;
#endif
            _udpSocket.ReceiveBufferSize = BufferSize;
            _udpSocket.SendBufferSize = BufferSize;

            //_udpSocket.DontFragment = true;
#if NETFX_CORE
            _sendSocketArgs.Completed += EventComplete;
            _receiveSocketArgs.Completed += EventComplete;
#endif
        }

        //Bind socket to port
        public bool Bind(NetEndPoint ep)
        {            
            try
            {
                _udpSocket.Bind(ep.EndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ep.EndPoint.Port);
                return true;
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Red, "[B]Bind exception: {0}", ex.ToString());
                return false;
            }
        }

        //Send to
        public int SendTo(byte[] data, NetEndPoint remoteEndPoint)
        {
            try
            {
#if NETFX_CORE
                _sendSocketArgs.SetBuffer(data, 0, data.Length);
                _sendSocketArgs.RemoteEndPoint = remoteEndPoint.EndPoint;
                _udpSocket.SendToAsync(_sendSocketArgs);
                _autoResetEvent.WaitOne();
                int result = _sendSocketArgs.BytesTransferred;
#else
                int result = _udpSocket.SendTo(data, remoteEndPoint.EndPoint);
#endif

                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint, result);
                return result;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]" + ex);
                return -1;
            }
        }

        //Receive from
        public int ReceiveFrom(ref byte[] data, ref NetEndPoint remoteEndPoint, ref int errorCode)
        {
#if !NETFX_CORE
            //wait for data
            if (!_udpSocket.Poll(1000, SelectMode.SelectRead))
            {
                return 0;
            }
#endif

            int result;

            //Reading data
            try
            {        
#if NETFX_CORE
                _receiveSocketArgs.SetBuffer(_receiveBuffer, 0, _receiveBuffer.Length);
                _udpSocket.ReceiveFromAsync(_receiveSocketArgs);
                _autoResetEvent.WaitOne(1);
                result = _receiveSocketArgs.BytesTransferred;
                remoteEndPoint.EndPoint = (IPEndPoint)_receiveSocketArgs.RemoteEndPoint;
#else
                EndPoint p = remoteEndPoint.EndPoint;
                result = _udpSocket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref p);
                remoteEndPoint.EndPoint = (IPEndPoint)p;
#endif
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    return 0;
                }
                else
                {
                    NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Error code: {0} - {1}", ex.SocketErrorCode, ex.ToString());
                    errorCode = (int)ex.SocketErrorCode;
                    return -1;
                }
            }

            //All ok!
            NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Recieved data from {0}, result: {1}", remoteEndPoint.ToString(), result);

            //Detecting bad data
            if (result == 0)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (0)");
                return 0;
            }

            if (result < NetConstants.HeaderSize)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (D<HS)");
                return 0;
            }
            data = _receiveBuffer;

            //Creating packet from data
            return result;
        }

        //Close socket
        public void Close()
        {
#if NETFX_CORE
            _udpSocket.Shutdown(SocketShutdown.Both);
#else
            _udpSocket.Close();
#endif
        }
    }
}
