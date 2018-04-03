using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Text;
using System.Collections.Generic;
using Network.Utility;
using System.Linq;
using System.Collections;

public class Client : MonoBehaviour
{
	#region Initialize Client Fields

	private const int MAX_CONNECTION = 100;
	private const float LERP_VAL = 0.75f;

	private int port = 5701;

	private int hostId;
	private int webHostId;
	private int connectionId;
	private int myConnectionId;

	private int reliableChannel;
	private int unreliableChannel;

	private bool isConnected = false;
	private bool isStarted = false;
	private byte error;

	private float connectionTimeElapsed;
	private string connectionTimeReal;

	#endregion

	#region Other Fields

	public GameObject PlayerPrefab;
	public InputField NameInput;
	public GameObject ConnectionUI;

	private string playerName;
	private List<ServerClient> PlayerRegistry;
	private Dictionary<int, Player> PlayerList;

	#endregion

	private void Awake()
	{
		ConnectionUI.SetActive(true);
	}

	public void Connect()
	{
		playerName = NameInput.text;
		if (playerName == null || playerName == "")
		{
			Debug.LogWarning("Must enter name for server connection");
			GameObject.Find("NamePlaceholder").GetComponent <Text>().text = "Enter Name To Proceed.";
			return;
		}

		NetworkTransport.Init(); //Initializes the Network Transport Layer
		ConnectionConfig cc = new ConnectionConfig(); //Creates Config for server

		reliableChannel = cc.AddChannel(QosType.Reliable); //Create new channel with reliable connection
		unreliableChannel = cc.AddChannel(QosType.Unreliable);

		HostTopology topo = new HostTopology(cc, MAX_CONNECTION); //Creates a hostInfo object, takes config, and max_connected

		hostId = NetworkTransport.AddHost(topo, 0); //TODO: May need to change this value if I have issues

		connectionId = NetworkTransport.Connect(hostId, "127.0.0.1", port, 0, out error); //Look into exceptionConnectionId
		//Debug.LogError (error);
		connectionTimeElapsed = Time.time; //For Time-Out functionality
		connectionTimeReal = DateTime.Now.ToString();
		Debug.Log(String.Format("Connected to server({0}) at {1}", connectionId.ToString(), connectionTimeReal));
		isConnected = true; //Mark startUp as compete with isStarted
		PlayerRegistry = new List<ServerClient>();
		PlayerList = new Dictionary<int, Player>();
	}

	private void Update()
	{
		if (!isConnected)
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
			case NetworkEventType.DataEvent:
			//Information Received
				string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
			
				Debug.Log(String.Format("Receiving Data-> To: {0} From: {1} Server Data: {2}",
				                        this.name,
				                        connectionId.ToString(),
				                        msg));

				string[] commands = msg.Split('$'); //Split by command

				OnGetMessage(commands);
				break;
			case NetworkEventType.Nothing:
				break;
			default:
				break;
		}
	}

	private void OnGetMessage(string[] message)
	{
		foreach (var command in message)
		{
			string[] parts = command.Split('|'); //Split each command by parts

			switch (parts[0])
			{
				case "":
				//Debug.Log ("Empty Command Received");
					break;
				case "ASKNAME":
					OnAskName(parts);
					break;
				case "PLAYERJOIN":
					OnPlayerJoin(parts);
					break;
				case "PLAYERLEFT":
					OnPlayerLeft(parts);
					break;
				case "GETPLAYERS":
					OnGetPlayers(parts);
					break;
				case "QUERYPOS":
					if (PlayerList.Count > 0)
						OnQueryPos(parts);
					break;
				case "UPDATEPOS":
					OnUpdatePos(parts);
					break;
				default:
					Debug.LogError("Server command for client " + myConnectionId + " not recognized: " + command);
					break;
			}
		}
	}

	private void OnUpdatePos(string[] data)
	{
		int playerId = int.Parse(data[1]);
		if (playerId == myConnectionId)
			return;
		
		//Turn back into Vector3 and Quaternion
		string pos = data[2].TrimStart('(').TrimEnd(')');
		string rot = data[3].TrimStart('(').TrimEnd(')');
		string[] posParts = pos.Split(',');
		string[] rotParts = rot.Split(',');

		float[] vector = ConvertToFloat(posParts);
		float[] qtn = ConvertToFloat(rotParts);

		Vector3 vecPos = new Vector3(vector[0], vector[1], vector[2]);
		Quaternion qtnRot = new Quaternion(qtn[0], qtn[1], qtn[2], qtn[3]);

		//TODO: Add Lerp function
		//TODO: Include a timestamp when sent for more accurate lerping
		Vector3 lastPos = PlayerList[playerId].Avatar.transform.position;
		Quaternion lastRot = PlayerList[playerId].Avatar.transform.rotation;
		PlayerList[playerId].Avatar.transform.position = Vector3.Lerp(lastPos, vecPos, LERP_VAL);
		PlayerList[playerId].Avatar.transform.rotation = Quaternion.Slerp(lastRot, qtnRot, LERP_VAL);
	}
	private float[] ConvertToFloat(string[] data)
	{
		float[] temp = new float[data.Length];
		for (int i = 0; i < data.Length; i++)
		{
			if (data[i] == " ")
				data[i] = data[i].TrimStart(' ');
			temp[i] = float.Parse(data[i]);
		}
		return temp;
	}

	private void OnQueryPos(string[] data)
	{
		//Return package containing position
		Vector3 myPos = PlayerList[myConnectionId].Avatar.transform.position;
		Quaternion myRot = PlayerList[myConnectionId].Avatar.transform.rotation;
		//Debug.Log("Rotation as String: " + mySRot + " Position as String: " + mySPos); //(0, 0, 0, 0) (0, 0, 0) Split based on parenthesis, then split based on commas, rot[4] pos[3]
		//TODO: Add velocity info by taking inverse of currentPos - previousPos = -direction
		string msg = "$REPLYPOS|";
		msg += myConnectionId + "|" + myPos.ToString() + "|" + myRot.ToString(); // $REPLYPOS|1|(1.2, 0.0, 2.3)|(0.0, 0.0, 0.0, 1.0);
		Send(msg, unreliableChannel);
	}

	private void OnPlayerJoin(string[] data)
	{
		//Spawn a single new player. But do not update playerList
		string[] component = data[1].Split('%');
		SpawnPlayer(int.Parse(component[0]), component[1]);
	}

	private void OnPlayerLeft(string[] data)
	{
		string[] component = data[1].Split('%');
		DestroyPlayer(int.Parse(component[0]), component[1]);
	}

	private void OnGetPlayers(string[] data)
	{
		List<ServerClient> tempRegistry = new List<ServerClient>();
		for (int i = 1; i <= data.Length - 1; i++)
		{
			string[] component = data[i].Split('%');
			tempRegistry.Add(new ServerClient(int.Parse(component[0]), component[1]));
			//Debug.Log ("Player: " + component [0] + " " + component [1]);
		}

		PlayerRegistry = tempRegistry;

		if (PlayerList.Count < PlayerRegistry.Count)
		{
			Debug.Log("Spawning previously connected players: " + (PlayerRegistry.Count - PlayerList.Count));
			foreach (var client in PlayerRegistry)
			{
				bool isSpawned = false;
				foreach (var key in PlayerList.Keys)
				{

					if (client.ConnectionId == key)
					{ //TODO: This cast may cause problems accessing Player specific properties down the line
						isSpawned = true;
						break;
					}
					else
					{
						continue;
					}
				}
				if (!isSpawned)
				{
					SpawnPlayer(client.ConnectionId, client.PlayerName);
				}
			}
		}
		else if (PlayerList.Count > PlayerRegistry.Count)
		{
			Debug.LogError("Error-> There are more spawned players than registered server clients!");
		}

		tempRegistry = null;
	}

	private void DisplayPlayerRegistry()
	{
		
	}

	private void SpawnPlayer(int _id, string _name)
	{
		//TODO: Add functionality to spawn individual players, rather than all players at once
		//GameObject tempGo = Instantiate (PlayerPrefab) as GameObject;

		//I can either have two separate methods for updating the player list, and spawning players, giving new commands from server for each
		//or I can keep both Players and ServerClients in thew same list, and type-cast the ServerClient to a Player after they are spawned
		//Then applying the instance to the Avatar field of each object in PlayerRegistry OfType(Player) where Avatar == null
		//Or have it remove the old ServerClient object, and add the new Player object

		GameObject playerInstance = (GameObject)Instantiate(PlayerPrefab);

		if (_id == myConnectionId)
		{
			//this is me
			ConnectionUI.SetActive(false);
			playerInstance.GetComponent<PlayerMotor>().enabled = true;
			isStarted = true;
		}

		PlayerList.Add(_id, new Player(_id, _name, playerInstance));
		PlayerList[_id].Avatar.GetComponentInChildren<TextMesh>().text = _name;
	}

	private void DestroyPlayer(int _id, string _name)
	{
		Destroy(PlayerList[_id].Avatar);
		PlayerList.Remove(_id);
	}

	private void OnAskName(string[] data)
	{
		//Ask player for name and update players connectionId from server (only server should be calling this command
		myConnectionId = int.Parse(data[1]);
		string message = ("NAMEIS|" + playerName);
		Send(message, reliableChannel);
	}

	private void Send(string _message, int _channel)
	{
		Debug.Log(String.Format("Sending Data-> From: {0} to {1}", this.myConnectionId, connectionId));
		byte[] msg = Encoding.Unicode.GetBytes(_message);
		NetworkTransport.Send(hostId, connectionId, _channel, msg, (sizeof(char) * _message.Length), out error);
	}
}