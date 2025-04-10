using shared;

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
	class GameRoom(TCPGameServer pOwner) : Room(pOwner)
	{
		public bool IsGameInPlay { get; private set; }

		//wraps the board to play on...
		CheckersBoard _board = new();

		int playerTurn = 1;

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
			//_board.SetBoardData([
			//		0,0,0,0,0,0,1,0,
			//		0,0,0,0,0,1,0,1,
			//		1,0,1,0,2,0,1,0,
			//		0,1,0,0,0,0,0,2,
			//		2,0,1,0,2,0,2,0,
			//		0,0,0,0,0,2,0,0,
			//		2,0,0,0,0,0,0,0,
			//		0,0,0,0,0,0,0,0
			//	]);
			_board.SetStartState();

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
			RoomJoinedEvent roomJoinedEvent = new();
			roomJoinedEvent.room = RoomJoinedEvent.Room.GAME_ROOM;
			pMember.SendMessage(roomJoinedEvent);


		}

        protected override void removeMember(TcpMessageChannel pMember)
        {
            base.removeMember(pMember);
			if (!IsGameInPlay) return;

            GameEnd gameEnd = new();
            gameEnd.GameEndState = GameEnd.EndState.Win;
            safeForEach(m => {
				m.SendMessage(gameEnd);
			});
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
			switch(pMessage)
			{
				case MakeMoveRequest makeMoveRequest:
					HandleMakeMoveRequest(makeMoveRequest, pSender);
					break;
                case SelectPieceRequest selectPieceRequest:
					HandleSelectPieceRequest(selectPieceRequest, pSender);
                    break;
				case ResignRequest resignRequest: 
					HandleResignRequest(resignRequest, pSender); 
					break;
				case JoinRoomRequest joinRoomRequest:
					HandleJoinRoom(joinRoomRequest, pSender);
					break;
            }
		}

        void HandleJoinRoom(JoinRoomRequest joinRoomRequest, TcpMessageChannel pSender)
        {
			if(joinRoomRequest.room == RoomJoinedEvent.Room.LOBBY_ROOM)
			{
				pSender.SendMessage(new RoomJoinedEvent() { room = RoomJoinedEvent.Room.LOBBY_ROOM});
				IsGameInPlay = false;
				removeMember(pSender);
				_server.GetLobbyRoom().AddMember(pSender);
			}
        }

        void HandleResignRequest(ResignRequest resignRequest, TcpMessageChannel pSender)
		{
            safeForEach(m =>
            {
                GameEnd gameEnd = new();
                if (m == pSender)
                {
                    gameEnd.GameEndState = GameEnd.EndState.Lose;
                    m.SendMessage(gameEnd);
                }
                else
                {
                    gameEnd.GameEndState = GameEnd.EndState.Win;
                    m.SendMessage(gameEnd);
                }
            });

        }

		void HandleMakeMoveRequest(MakeMoveRequest pMessage, TcpMessageChannel pSender)
		{
			//we have two players, so index of sender is 0 or 1, which means playerID becomes 1 or 2
			byte playerID = (byte)(indexOfMember(pSender) + 1);
			if (playerID != playerTurn)
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
				!CheckersBoard.IsPieceOfPlayer(playerID, pieceToMove) ||
                (
					!_board.HasRemovablePieces(pieceToMove, from, playerID) &&
					_board.PlayerHasForcedCapture(playerID, pieceToExclude: from) 
				)
			)
            {
                Log.LogInfo("Is not your Piece!", this, ConsoleColor.Red);
                return;
            }

            List<int> possibleMoves = _board.GetPossibleMoves(pieceToMove, from, playerID);
            if (!possibleMoves.Any(m => m == to))
            {
                Log.LogInfo("Not a valid move!", this, ConsoleColor.Red);
                return;
            }
            Log.LogInfo("Can make move!", this, ConsoleColor.Green);

            bool removedPiece = _board.MakeMove(from, to);
			
			if(_board.IsPromoteable(to, playerID)) _board.PromotePiece(to);

            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

			if (!removedPiece || !_board.HasRemovablePieces(pieceToMove, to, playerID))
			{
                SwitchPlayerTurn();
			}

            //and send the result of the boardstate back to all clients
            MakeMoveResult makeMoveResult = new();
			makeMoveResult.whoMadeTheMove = playerID;
			makeMoveResult.boardData = _board.GetBoardData();
			SendToAll(makeMoveResult);

			CheckAndSendWinState(playerID, pSender);
		}

		void CheckAndSendWinState(byte playerID, TcpMessageChannel pSender)
		{
			byte otherPlayerID = _board.GetOtherPlayer(playerID);
            if (!_board.PlayerHasPawns(otherPlayerID))
            {
				safeForEach(m =>
				{
                    GameEnd gameEnd = new();
                    if (m == pSender)
					{
						gameEnd.GameEndState = GameEnd.EndState.Win;
						m.SendMessage(gameEnd);
                    }
					else
					{
                        gameEnd.GameEndState = GameEnd.EndState.Lose;
                        m.SendMessage(gameEnd);
                    }
				});
            }
			//else if (!_board.PlayerHasMoves(otherPlayerID)) //staleMate
			//{
			//	GameEnd gameEnd = new()
			//	{
			//		GameEndState = GameEnd.EndState.Draw
			//	};
			//	SendToAll(gameEnd);
			//}
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
