using System;
using System.Net.Sockets;
using System.Net;
using shared;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace server {

	/**
	 * Basic TCPGameServer that runs our game.
	 * 
	 * Server is made up out of different rooms that can hold different members.
	 * Each member is identified by a TcpMessageChannel, which can also be used for communication.
	 * In this setup each client is only member of ONE room, but you could change that of course.
	 * 
	 * Each room is responsible for cleaning up faulty clients (since it might involve gameplay, status changes etc).
	 * 
	 * As you can see this setup is limited/lacking:
	 * - only 1 game can be played at a time
	 */
	class TCPGameServer
	{

		public static void Main(string[] args)
		{
			TCPGameServer tcpGameServer = new TCPGameServer();
			tcpGameServer.run();
		}

		//we have 3 different rooms at the moment (aka simple but limited)

		readonly LoginRoom _loginRoom;	//this is the room every new user joins
		readonly LobbyRoom _lobbyRoom;	//this is the room a user moves to after a successful 'login'
		readonly List<GameRoom> _gameRooms = new();		//this is the room a user moves to when a game is succesfully started

		//stores additional info for a player
		readonly Dictionary<TcpMessageChannel, PlayerInfo> _playerInfo = new();
        TcpListener listener;
		

        TCPGameServer()
		{
			//we have only one instance of each room, this is especially limiting for the game room (since this means you can only have one game at a time).
			_loginRoom = new LoginRoom(this);
			_lobbyRoom = new LobbyRoom(this);
			_gameRooms.Add(new GameRoom(this));
		}

		void run()
		{
            #region Tests
            CheckersBoard testBoard = new();
			testBoard.SetTest2();
			testBoard.SetTest3();
			testBoard.SetTest4();
			testBoard.SetTest5();
            testBoard.SetTest6();
            testBoard.SetTest7
			(
                [
					3,0,0,0,0,0,0,0,
					0,2,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0
				],
				moveFrom: 0,
				moveTo: 63
			);

            testBoard.SetTest7
            (
                [
                    0,0,0,0,0,0,0,0,
                    0,0,0,0,0,2,0,0,
                    0,0,0,0,0,0,0,0,
                    0,0,0,3,0,0,0,0,
                    0,0,0,0,0,0,0,0,
                    0,0,0,0,0,2,0,0,
                    0,0,0,0,0,0,0,0,
                    0,0,0,0,0,0,0,0
                ],
                moveFrom: 27,
                moveTo: 6
            );
            testBoard.SetTest7
            (
                [
                    1,0,1,0,0,0,0,0,
                    0,0,0,2,0,0,0,0,
                    2,0,0,0,0,0,0,0,
                    0,0,0,0,0,0,0,0,
                    0,0,0,0,0,0,0,0,
                    2,0,2,0,0,0,0,0,
                    0,0,0,0,0,0,0,0,
                    0,0,1,0,0,0,0,0
                ],
                moveFrom: 49,
                moveTo: 58
            );
			testBoard.SetTest7
            (
                [
                    0,0,0,0,0,0,4,0,
                    0,1,0,0,0,0,0,0,
                    1,0,0,0,0,0,0,0,
                    0,0,0,0,0,0,0,0,
                    0,0,0,0,3,0,0,0,
                    0,3,0,0,0,0,0,0,
                    2,0,0,0,0,0,0,0,
                    0,0,0,0,0,0,0,0
                ],
                moveFrom: 41,
                moveTo: 33,
				false
            );
			//testBoard.SetStartState();

			//ResetBoard b = new ResetBoard() { boardData = testBoard.GetBoardData() };
			//Packet p = new Packet();
			//p.Write(b);
			//Packet pb = new Packet(p.GetBytes());
			//var result = pb.ReadObject();


			//b = new ResetBoard() { boardData = testBoard.GetBoardData() };
			//p = new Packet();
			//p.Write(b);
			//pb = new Packet(p.GetBytes());
			//result = pb.ReadObject();
			//var board = result as ResetBoard;
			//var strin = board.boardData.ToString();
			//Log.LogInfo("Starting server on port 55555", this, ConsoleColor.Gray);

			//start listening for incoming connections (with max 50 in the queue)
			//we allow for a lot of incoming connections, so we can handle them
			//and tell them whether we will accept them or not instead of bluntly declining them
			#endregion
			listener = new TcpListener(IPAddress.Any, 55555);
            listener.Start(50);

            while (true)
			{
				ProcessPendingClients();
				//now update every single room
				_loginRoom.Update();
				_lobbyRoom.Update();
                foreach (GameRoom gameRoom in _gameRooms)
					gameRoom.Update();

				for (int i = _gameRooms.Count-1; i >=0 ; i--)
				{
					var gameRoom = _gameRooms[i];
					if (gameRoom.GetMemberCount() > 0) gameRoom.Update();
					else _gameRooms.RemoveAt(i);
                }
                Thread.Sleep(100);
			}
		}
		
		void ProcessPendingClients()
		{
            //check for new members	
            if (listener.Pending())
            {
                //get the waiting client
                Log.LogInfo("Accepting new client...", this, ConsoleColor.White);
                TcpClient client = listener.AcceptTcpClient();
                //and wrap the client in an easier to use communication channel
                TcpMessageChannel channel = new TcpMessageChannel(client);
                //and add it to the login room for further 'processing'
                _playerInfo.Add(channel, new());
                _loginRoom.AddMember(channel);
            }
        }

		//provide access to the different rooms on the server 
		public LoginRoom GetLoginRoom() { return _loginRoom; }
		public LobbyRoom GetLobbyRoom() { return _lobbyRoom; }
		public GameRoom CreateGameRoom()
		{
			var newGR = new GameRoom(this);
            _gameRooms.Add(newGR);
			return newGR;
		}
		/**
		 * Returns a handle to the player info for the given client 
		 * (will create new player info if there was no info for the given client yet)
		 */
		public PlayerInfo GetPlayerInfo (TcpMessageChannel pClient)
		{
			if (!_playerInfo.TryGetValue(pClient, out PlayerInfo value))
			{
                value = new PlayerInfo();
                _playerInfo[pClient] = value;
			}

			return value;
		}

		/**
		 * Returns a list of all players that match the predicate, e.g. to get a list of 
		 * all players named bob, you would do:
		 *	GetPlayerInfo((playerInfo) => playerInfo.name == "bob");
		 */
		public List<PlayerInfo> GetPlayerInfo(Predicate<PlayerInfo> pPredicate)
		{
			return _playerInfo.Values.ToList<PlayerInfo>().FindAll(pPredicate);
		}

		/**
		 * Should be called by a room when a member is closed and removed.
		 */
		public void RemovePlayerInfo (TcpMessageChannel pClient)
		{
			_playerInfo.Remove(pClient);
		}

		

        
    }

}


