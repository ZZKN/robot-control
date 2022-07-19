using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class EchoClient
{
	public static void Main(String[] args)
	{
		IPAddress ip_address = IPAddress.Parse("127.0.0.1"); //default
		int port = 8080;
		try
		{
			if (args.Length >= 1)
			{
				ip_address = IPAddress.Parse(args[0]);
			}
		}
		catch (FormatException)
		{
			Console.WriteLine("Invalid IP address entered. Using default IP of: "
												+ ip_address.ToString());
		}
		try
		{
			TcpClient client = new TcpClient();
			Console.WriteLine("Connecting.....");

			client.Connect(ip_address.ToString(), port); 
			Console.WriteLine("Connected");
			NetworkStream nwStream = client.GetStream();
			

			byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes("Oompa Loompa\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);

			byte[] bytesToRead = new byte[client.ReceiveBufferSize];
			int bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
			Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));

			

			bytesToSend = ASCIIEncoding.ASCII.GetBytes("0\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);

			bytesToRead = new byte[client.ReceiveBufferSize];
			bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
			Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));


			bytesToSend = ASCIIEncoding.ASCII.GetBytes("RECHARGING\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);

			Thread.Sleep(1000);

			bytesToSend = ASCIIEncoding.ASCII.GetBytes("12FULL POWER\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);


			bytesToSend = ASCIIEncoding.ASCII.GetBytes("8389\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);

			bytesToRead = new byte[client.ReceiveBufferSize];
			bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
			Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));

			bytesToRead = new byte[client.ReceiveBufferSize];
			bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
			Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));

			bytesToSend = ASCIIEncoding.ASCII.GetBytes("OK -5 -10\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);

			bytesToRead = new byte[client.ReceiveBufferSize];
			bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
			Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));

			bytesToSend = ASCIIEncoding.ASCII.GetBytes("OK -4 -10\a\b");
			nwStream.Write(bytesToSend, 0, bytesToSend.Length);
			while (true)
            {
				//---read back the text---
				bytesToRead = new byte[client.ReceiveBufferSize];
				bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
				Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));
				//Console.ReadLine();

				Console.Write("Enter the string to be transmitted : ");
				
					
				String str = Console.ReadLine();
				Console.WriteLine("Sending : " + str);
				bytesToSend = ASCIIEncoding.ASCII.GetBytes(str+'\a'+'\b');
				nwStream.Write(bytesToSend, 0, bytesToSend.Length);

				
			}
			
			
			client.Close();

		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	} // end Main()
} // end class definition