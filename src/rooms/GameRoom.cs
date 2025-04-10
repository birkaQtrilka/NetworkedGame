﻿using shared;

namespace server
{
	/**
	 * This room runs a single Game (at a time). 
	 * 
	 * The 'Game' is very simple at the moment:
	 *	- all client moves are broadcasted to all clients
	 *	
	 * The game has no end yet (that is up to you), in other words:
	 * all players that are added to this room, stay in here indefinitely.
	 */
	class GameRoom : Room
	{
		public bool IsGameInPlay { get; private set; }

		//wraps the board to play on...
		CheckersBoard _board = new CheckersBoard();

		int playerTurn = 1;

		public GameRoom(TCPGameServer pOwner) : base(pOwner)
		{
		}



		public void StartGame(TcpMessageChannel pPlayer1, TcpMessageChannel pPlayer2)
		{
			if (IsGameInPlay) throw new Exception("Programmer error duuuude.");

			IsGameInPlay = true;
			addMember(pPlayer1);
			addMember(pPlayer2);
			playerTurn = 1;
			var infoMessage = new ShowPlayerInfo()
			{
				Name1 = _server.GetPlayerInfo(pPlayer1).Name,
				Name2 = _server.GetPlayerInfo(pPlayer2).Name,
			};
			_board.SetBoardData([
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0,
					0,0,0,0,0,2,0,0,
					0,0,0,0,0,0,0,0,
					0,1,0,0,0,0,0,0,
					0,0,0,0,0,0,0,0
				]);
			//_board.SetStartState();

			var resetBoard = new ResetBoard()
			{
				boardData = _board.GetBoardData()
			};

			safeForEach(m =>
			{
				m.SendMessage(resetBoard);
			});

			safeForEach(m =>
			{
				m.SendMessage(infoMessage);
			});
		}

		protected override void addMember(TcpMessageChannel pMember)
		{
			base.addMember(pMember);

			//notify client he has joined a game room 
			RoomJoinedEvent roomJoinedEvent = new RoomJoinedEvent();
			roomJoinedEvent.room = RoomJoinedEvent.Room.GAME_ROOM;
			pMember.SendMessage(roomJoinedEvent);


		}

		public override void Update()
		{
			//demo of how we can tell people have left the game...
			int oldMemberCount = memberCount;
			base.Update();
			int newMemberCount = memberCount;

			if (oldMemberCount != newMemberCount)
			{
				Log.LogInfo("People left the game...", this);
			}
		}

		protected override void handleNetworkMessage(ASerializable pMessage, TcpMessageChannel pSender)
		{
			if (pMessage is MakeMoveRequest makeMoveRequest)
			{
				HandleMakeMoveRequest(makeMoveRequest, pSender);
			}
			else if (pMessage is SelectPieceRequest selectPieceRequest)
			{
				HandleSelectPieceRequest(selectPieceRequest, pSender);
			}
		}

		void HandleMakeMoveRequest(MakeMoveRequest pMessage, TcpMessageChannel pSender)
		{
			//we have two players, so index of sender is 0 or 1, which means playerID becomes 1 or 2
			byte senderID = (byte)(indexOfMember(pSender) + 1);
			if (senderID != playerTurn)
			{
				Log.LogInfo("It's not your turn!", this, ConsoleColor.Red);
				return;
			}
			int from = pMessage.From;
			int to = pMessage.To;

			byte pieceToMove = _board.GetBoardData().board[from];
			//checking if the piece is of the sender
			if 
			(
				!CheckersBoard.IsPieceOfPlayer(senderID, pieceToMove) ||
                (
					!_board.HasRemovablePieces(pieceToMove, from, senderID) &&
					_board.PlayerHasForcedCapture(senderID, pieceToExclude: from) 
				)
			)
            {
                Log.LogInfo("Is not your Piece!", this, ConsoleColor.Red);
                return;
            }

            List<int> possibleMoves = _board.GetPossibleMoves(pieceToMove, from, senderID);
            if (!possibleMoves.Any(m => m == to))
            {
                Log.LogInfo("Not a valid move!", this, ConsoleColor.Red);
                return;
            }
            Log.LogInfo("Can make move!", this, ConsoleColor.Green);

            bool removedPiece = _board.MakeMove(from, to);
			
			if(_board.IsPromoteable(to, senderID)) _board.PromotePiece(to);

            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

			if (!removedPiece || !_board.HasRemovablePieces(pieceToMove, to, senderID))
			{
                SwitchPlayerTurn();
			}

            //and send the result of the boardstate back to all clients
            MakeMoveResult makeMoveResult = new();
			makeMoveResult.whoMadeTheMove = senderID;
			makeMoveResult.boardData = _board.GetBoardData();
			sendToAll(makeMoveResult);
		}

		void SwitchPlayerTurn()
		{
			if (playerTurn == 1) playerTurn = 2;
			else playerTurn = 1;
		}

		void HandleSelectPieceRequest(SelectPieceRequest pMessage, TcpMessageChannel pSender)
		{
			//respond with confirmation and possible moves
			byte playerID = (byte)(indexOfMember(pSender) + 1);
			
			int pieceIndex = pMessage.TileIndex;
            byte piece = _board.GetBoardData().board[pieceIndex];
			if
			(
			    playerTurn == playerID &&
				CheckersBoard.IsPieceOfPlayer(playerID, piece) &&
				(
					_board.HasRemovablePieces(piece, pieceIndex, playerID) ||
					!_board.PlayerHasForcedCapture(playerID, pieceToExclude: pieceIndex) 
                )
			)
			{
				pSender.SendMessage(new SelectPieceResponse()
				{
					MoveIndexes = _board.GetPossibleMoves(piece, pieceIndex, playerID),
					SelectedPieceIndex = pieceIndex
				});
				
			}
			else
			{
				Log.LogInfo("Not your piece", this, ConsoleColor.Red);
			}
		}
	}
}
