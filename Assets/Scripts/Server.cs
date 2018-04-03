using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections.Generic;
using System;
using Network.Utility;

namespace NSServer
{
	public class Server : MonoBehaviour
	{
		#region Initialize Server Fields

		private const int MAX_CONNECTION = 4;
		//TODO: Generate error message when player querys a server at capacity

		private int port = 5701;

		private int hostId;
		private int webHostId;
		private int connectionId;

		private int reliableChannel;
		private int unreliableChannel;

		private bool isStarted = false;
		private byte error;

		#endregion

		#region Other Fields

		private List<ServerClient> ClientRegistry;
		private float lastPositionUpdate;
		private float positionUpdateFrequency = 0.1f;
		//Option 1: Every positionUpdateFrequency, send ping to all clients->client returns position->server distributes positions of all clients
		//Option 2: Clients send position to server every positionUpdateFrequency, upon receiving all(or each) update, server distributes info to all clients
		//Option 3: When clients moves more than a certain amount, sends position info to server, server distributes to clients(include direction info)

		#endregion

		private void Start()
		{
			NetworkTransport.Init(); //Initializes the Network Transport Layer
			ConnectionConfig cc = new ConnectionConfig(); //Creates Config for server

			reliableChannel = cc.AddChannel(QosType.Reliable); //Create new channel with reliable connection
			unreliableChannel = cc.AddChannel(QosType.Unreliable);

			HostTopology topo = new HostTopology(cc, MAX_CONNECTION); //Creates a hostInfo object, takes config, and max_connected

			hostId = NetworkTransport.AddHost(topo, port, null); //Initialize host that will be transferring info
			webHostId = NetworkTransport.AddWebsocketHost(topo, port, null); //Initialize host to transfer over browser

			isStarted = true; //Mark startUp as compete with isStarted
			ClientRegistry = new List<ServerClient>();
		}

		private void OnApplicationQuit()
		{
			NetworkTransport.Shutdown();
		}

		private void Update()
		{
			if (!isStarted)
				return;
			int recHostId;
			int channelId;
			byte[] recBuffer = new byte[1024];
			int bufferSize = 1024;
			int dataSize;
			byte error;
			NetworkEventType recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, 
			                                                    recBuffer, bufferSize, out dataSize, out error);

			switch (recData)
			{
				case NetworkEventType.ConnectEvent:
			//A player has connected
					Debug.Log("Player: " + connectionId.ToString() + " connection accepted... Querying");
					OnConnection();
					break;
				case NetworkEventType.DataEvent:
			//Information Received
					string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
					Debug.Log("Player: " + connectionId.ToString() + " has sent: " + msg);
					string[] commands = msg.Split('$');

					OnGetMessage(commands);
					break;
				case NetworkEventType.DisconnectEvent:
			//Player Disconnected
					Debug.Log("Player: " + connectionId.ToString() + " connection ended");
					OnDisconnect();
					break;
				case NetworkEventType.Nothing:
					break;
				default:
					Debug.Log("Possible error for message recipient: Server");
					break;
			}

			if (Time.time - lastPositionUpdate >= positionUpdateFrequency && ClientRegistry.Count > 1)
			{
				Send(GenerateQueryPosition(), unreliableChannel, ClientRegistry.FindAll(x => x.PlayerName != "TEMP")); //Query all clients for position
			}
		}

		private void OnConnection()
		{
			string playerName = "TEMP";
			ClientRegistry.Add(new ServerClient(connectionId, playerName));

			//Register new player on server
			//Give players their ID
			//Request name new player's name

			//Messages will start and end with $
			//Distinct messages will be seperated by |
			//Individual parts of distinct messages will be separated by %

			string msg = "$ASKNAME|" + connectionId;
			Send(msg, reliableChannel, connectionId);
		}

		private void OnDisconnect()
		{
			//Remove that player from registry, send GETPLAYERS command to all
			var client = ClientRegistry.Find(x => x.ConnectionId == connectionId);
			Debug.Log(String.Format("Player Disconnected-> ID: {0} || Name: {1}", connectionId, client.PlayerName));
			ClientRegistry.Remove(client);
			if (ClientRegistry.Count > 0)
				Send(GeneratePlayerLeft(connectionId, client.PlayerName), reliableChannel, ClientRegistry);
		}

		private void OnGetMessage(string[] _message)
		{
			foreach (var command in _message)
			{
				string[] parts = command.Split('|'); //Split each command by parts

				switch (parts[0])
				{
					case "NAMEIS":
						OnNameIs(parts);
						break;
					case "REPLYPOS":
						OnReplyPos(parts);
						break;
					default:
						Debug.LogError("Server command for client " + connectionId.ToString() + " not recognized: " + command);
						break;
				}
			}
		}

		private void OnNameIs(string[] data)
		{
			string temp = data[1];
			//Should only be two elements in array (NAMEIS|playerName)
			//Select ServerClient from list where connectionId == connectionId, update player name
			ClientRegistry.Find(x => x.ConnectionId == connectionId).PlayerName = temp;
			Debug.Log(String.Format("Player Info Set-> ConnectionId: {0} Name: {1}", connectionId.ToString(), temp));
			//Once players answers name request, package up List of all players, send to all players
			//to keep accurate player registry
			//Send to all
			//Send this message on playerJoin
			Send(GeneratePlayerJoin(connectionId, temp), reliableChannel, ClientRegistry);
		}
		private void OnReplyPos(string[] data)
		{
			//When client responds with new position
			// $REPLYPOS  1  (1.2, 0.0, 2.3)  (0.0, 0.0, 0.0, 1.0);
			//Either reply immediately, or wait for all responses, then reply
			string msg = GenerateUpdatePosition();
			msg += data[1] + "|" + data[2] + "|" + data[3];
			Send(msg, unreliableChannel, ClientRegistry);
		}

		private string GenerateUpdatePosition()
		{
			string msg = "$UPDATEPOS|";
			//Append all position info
			return msg;
		}

		private string GenerateQueryPosition()
		{
			string msg = "$QUERYPOS";
			return msg;
		}

		private string GeneratePlayerJoin(int _id, string _name) //Pases info on player who joined
		{
			string msg = "$PLAYERJOIN|";
			msg += (_id.ToString() + "%" + _name);
			msg += GenerateGetPlayers();
			return msg;
		}

		private string GenerateGetPlayers() //Updates Player List
		{
			string msg = "$GETPLAYERS|";
			foreach (var client in ClientRegistry)
			{
				msg += (client.ConnectionId.ToString() + '%' + client.PlayerName + '|');
			}
			msg = msg.Trim('|'); //Remove the last deliminator
			return msg;
		}

		private string GeneratePlayerLeft(int _id, string _name) //Passes info on player who left
		{
			string msg = "$PLAYERLEFT|";
			msg += (_id.ToString() + "%" + _name);
			msg += GenerateGetPlayers();
			return msg;
		}

		private void Send(string _message, int _channel, int _targetId)
		{
			List<ServerClient> c = new List<ServerClient>();
			//c.Add (ClientRegistry.Find ((x) => {
			//	x.ConnectionId == _targetId;
			//}));
			c.Add(ClientRegistry.Find(x => x.ConnectionId == _targetId));
			Send(_message, _channel, c);
		}

		private delegate string ListTargets(List<ServerClient> t);

		///<summary>
		/// Send _message to _targets on given _channel
		/// </summary>
		/// <param name="Send"></param>
		private void Send(string _message, int _channel, List<ServerClient> _targets)
		{
			//Anonymous function simply extracts the playerName from ServerClient for logging purposes
			ListTargets listTargets = (x) => {
				string info = "";
				foreach (var obj in x)
				{
					info += obj.PlayerName + "/";
				}
				return info;
			};

			Debug.Log(String.Format("Sending Data-> From: {0} To: {1} Data: {2}", this.name, listTargets(_targets), _message));
			byte[] msg = Encoding.Unicode.GetBytes(_message);
			foreach (var target in _targets)
			{
				NetworkTransport.Send(hostId, target.ConnectionId, _channel, msg, (sizeof(char) * _message.Length), out error);
			}
		}

	}
}
namespace Network.Utility
{
	public class ServerClient
	{
		//TODO: Create private fields, public properties
		public readonly int ConnectionId;
		public string PlayerName;

		public ServerClient(int connectionId, string playerName)
		{
			this.ConnectionId = connectionId;
			this.PlayerName = playerName;
		}
	}

	public class Player : ServerClient
	{
		public GameObject Avatar;

		public Player(int id, string name, GameObject avatar) : base(id, name)
		{
			this.Avatar = avatar;
		}
	}
}