using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;

namespace Dust765LobbyServer
{
	class AppMain
	{
		private ArrayList	m_aryClients = new ArrayList();
		public static IPAddress IP = null;
		public static int nPortListen = 2596;
		public static int BufferSize = 1024;

		static void Main(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				try
				{
					if ((args[i][0] == '-') || (args[i][0] == '/'))
					{
						switch (Char.ToLower(args[i][1]))
						{
							case 'l':
								IP = IPAddress.Parse(args[++i]);
								break;
							case 'p':
								nPortListen = System.Convert.ToInt32(args[++i]);
								break;
						case 'x':
							BufferSize = System.Convert.ToInt32(args[++i]);
							break;
							
							default:
								usage();
								break;
						}
					}
				}
				catch
				{
					usage();
				}
			}
			usage();


			AppMain app = new AppMain();
			Console.WriteLine( "*** Dust765 Lobby Server Started {0} *** ", DateTime.Now.ToString( "G" ) );
			IPAddress[] aryLocalAddr = null;
			String strHostName = "";

			if (IP == null)
            {
				try
				{
					strHostName = Dns.GetHostName();
					IPHostEntry ipEntry = Dns.GetHostByName(strHostName);
					aryLocalAddr = ipEntry.AddressList;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error trying to get local address {0} ", ex.Message);
				}
				if (aryLocalAddr == null || aryLocalAddr.Length < 1)
				{
					Console.WriteLine("Unable to get local address");
					return;
				}
            }
			else
			{
				aryLocalAddr[0] = IP;
			}

			Console.WriteLine( "Listening on : [{0}] {1}:{2}", strHostName, aryLocalAddr[0], nPortListen );

			Socket listener = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
			listener.Bind( new IPEndPoint( aryLocalAddr[0], nPortListen) );
			listener.Listen( 100 );
			listener.BeginAccept( new AsyncCallback( app.OnConnectRequest ), listener );

			Console.WriteLine ("Press Enter to exit" );
			Console.ReadLine();
			Console.WriteLine ("Closing down Dust765 Lobby Server" );

			listener.Close();
			GC.Collect();
			GC.WaitForPendingFinalizers();		
		}
		public void OnConnectRequest( IAsyncResult ar )
		{
			Socket listener = (Socket)ar.AsyncState;
			NewConnection( listener.EndAccept( ar ) );
			listener.BeginAccept( new AsyncCallback( OnConnectRequest ), listener );
		}
		public void NewConnection( Socket sockClient )
		{
			SocketLobbyClient client = new SocketLobbyClient( sockClient );
			m_aryClients.Add( client );
			Console.WriteLine( "Client {0}, joined {1}", client.Sock.RemoteEndPoint, DateTime.Now.ToString("G"));
			Console.WriteLine( "Total clients connected {0}", m_aryClients.Count);
			client.SetupRecieveCallback( this );
		}
		public void OnRecievedData( IAsyncResult ar )
		{
			SocketLobbyClient client = (SocketLobbyClient)ar.AsyncState;
			byte [] aryRet = client.GetRecievedData( ar );

			if( aryRet.Length < 1 )
			{
				Console.WriteLine( "Client {0}, disconnected {1}", client.Sock.RemoteEndPoint, DateTime.Now.ToString("G"));
				client.Sock.Close();
				m_aryClients.Remove( client );
				Console.WriteLine( "Total clients connected {0}", m_aryClients.Count);
				return;
			}

            int packetNR = aryRet.Length >= 1 ? aryRet[0] : -1;
            string packetIdentifiedString = IdentifyPacket(packetNR);
            
            Console.WriteLine( "Read {0} bytes from client {1} with PacketNR {2} ({3})",
							aryRet.Length, client.Sock.RemoteEndPoint, packetNR, packetIdentifiedString);

            //BROADCAST
            foreach ( SocketLobbyClient clientSend in m_aryClients )
			{
				try
				{
					if (packetNR == 6 && client.Sock == clientSend.Sock)
                    {
                        //DONT SEND TO SENDER ON PACKET 6
                    }else
					{
						Console.WriteLine("Sending {0} bytes to client {1}.", aryRet.Length, clientSend.Sock.RemoteEndPoint);
						clientSend.Sock.Send(aryRet);
					}
				}
				catch
				{
					Console.WriteLine( "Send to client {0} failed {1}", client.Sock.RemoteEndPoint, DateTime.Now.ToString("G"));
					Console.WriteLine( "Client {0}, disconnected {1}", client.Sock.RemoteEndPoint, DateTime.Now.ToString("G"));
					clientSend.Sock.Close();
					m_aryClients.Remove( client );
					Console.WriteLine( "Total clients connected {0}", m_aryClients.Count);
					return;
				}
			}
			client.SetupRecieveCallback( this );
		}

		static void usage()
		{
			Console.WriteLine("Executable_file_name [-l bind-address] [-p port] [-x size]");
			Console.WriteLine("  -l bind-address        Local address to bind to");
			Console.WriteLine("  -p port                Local port to bind to");
			Console.WriteLine("  -x size                Size  of send and receive buffer");
			Console.WriteLine(" Else, default values will be used...");
			Console.WriteLine(" ----------------------------------- ");
		}

        static string IdentifyPacket (int PacketNR)
        {
            switch (PacketNR)
            {
                case 1:
                    return "Connect";

                    break;

                case 2:
                    return "Disconnect";

                    break;

                case 3:
                    return "Spellcast";

                    break;

                case 4:
                    return "SetTarget";

                    break;

                case 5:
                    return "DropTarget";

                    break;

                case 6:
                    return "HiddenPosition";

                    break;

				case 7:
					return "Attack";

					break;

				default:
                    return PacketNR.ToString();

            }
        }
	}
	internal class SocketLobbyClient
	{
		private Socket m_sock;
		private byte[] m_byBuff = new byte[AppMain.BufferSize];
		
		public SocketLobbyClient( Socket sock )
		{
			m_sock = sock;
		}

		public Socket Sock
		{
			get{ return m_sock; }
		}

		public void SetupRecieveCallback( AppMain app )
		{
			try
			{
				AsyncCallback recieveData = new AsyncCallback(app.OnRecievedData);
				m_sock.BeginReceive( m_byBuff, 0, m_byBuff.Length, SocketFlags.None, recieveData, this );
			}
			catch( Exception ex )
			{
				Console.WriteLine( "Recieve callback setup failed! {0} {1}", ex.Message, DateTime.Now.ToString("G"));
			}
		}

		public byte [] GetRecievedData( IAsyncResult ar )
		{
            int nBytesRec = 0;
			try
			{
				nBytesRec = m_sock.EndReceive( ar );
			}
			catch{}
			byte [] byReturn = new byte[nBytesRec];
			Array.Copy( m_byBuff, byReturn, nBytesRec );

			return byReturn;
		}
	}
}