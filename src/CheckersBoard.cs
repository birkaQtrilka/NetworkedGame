using shared;
using System;
using System.Drawing;
using System.Numerics;

namespace server
{
    /**
     * This class wraps the actual board we are playing on.
     * 
     * Normally both logic and data would be in a single class (e.g. something like TicTacToeModel).
     * In this case I chose to split them up: we have one class TicTacToeBoard that wraps an instance
     * of the actual board data. This way we have an object we can serialize to clients (the board data) 
     * and one class that actually manages the logic of manipulating the board.
     * 
     * In this specific instance that logic is almost non existent, because I tried to keep the demo
     * as simple as possible, so we can only make a move. In an actual game, this class would implement
     * methods such as GetValidMoves(), HasWon(), GetCurrentPlayer() etc.
     */
    public class CheckersBoard
    {
        struct Vector2Int(int x, int y)
        {
            public int X = x;
            public int Y = y;

            public static Vector2Int operator +(Vector2Int a, Vector2Int b) =>
                new(a.X + b.X, a.Y + b.Y);

            public static Vector2Int operator *(Vector2Int a, int scalar) =>
                new(a.X * scalar, a.Y * scalar);

            public override readonly string ToString() => $"({X}, {Y})";

            public static bool operator ==(Vector2Int a, Vector2Int b) =>
                a.X == b.X && a.Y == b.Y;

            public static bool operator !=(Vector2Int a, Vector2Int b) =>
                !(a == b);
            public override readonly bool Equals(object? obj) =>
                obj is Vector2Int v && this == v;

            public override readonly int GetHashCode() =>
                HashCode.Combine(X, Y);

        }

        CheckersBoardData _board = new CheckersBoardData();
        const byte EMPTY = 0;
        const byte PLAYER_1_PAWN = 1;
        const byte PLAYER_2_PAWN = 2;
        const byte PLAYER_1_QUEEN = 3;
        const byte PLAYER_2_QUEEN = 4;
        const byte BOARD_COUNT = 64;
        const byte BOARD_WIDTH = 8;

        readonly static Vector2Int[] directions =
        [
            new(-1, -1),//direction_tl
            new (1, -1),//direction_tr
            new (1, 1) ,//direction_br
            new (-1, 1) //direction_bl
        ];

        //I am selecting a piece
        //Server is sending all possible moves
        //I see all possible moves (you could take pieces in different ways so allow the player to choose)
        //I try to move the piece
        //Server is checking if I'm able to move the piece
        //Server is checking if the move ends the turn
        //repeat untill the turn ends
        //server checks if anyone won the game

        /**
         * Return the inner board data state so we can send it to a client.
         */
        public CheckersBoardData GetBoardData()
        {
            //it would be more academically correct if we would clone this object before returning it, but anyway.
            return _board;
        }

        public void SetStartState()
        {
            var board = _board.board;
            for (int i = 0; i < 24; i++)
                if ((i + ((i / 8) % 2)) % 2 == 0) board[i] = PLAYER_1_PAWN;
                else board[i] = EMPTY;

            for (int i = 24; i < 40; i++)
                _board.board[i] = EMPTY;

            for (int i = 40; i < BOARD_COUNT; i++)
                if ((i + ((i / 8) % 2)) % 2 == 0) board[i] = PLAYER_2_PAWN;
                else board[i] = EMPTY;
        }

        public void SetBoardData(byte[] data)
        {
            _board.board = data;
        }

        //go through all moves (all directions)
        //fill up a list of direction data
        //remove the illegal directions 
        class DirectionMoves(int directionIndex)
        {
            public readonly List<int> indexes = [];
            public readonly int directionIndex = directionIndex;
            public bool hasCapturablePiece = false;
        }

        public List<int> GetPossibleMoves(byte piece, int pieceIndex, byte playerIndex)
        {
            //converting to x and y because it's easier to check bounds and move through board
            Vector2Int p = GetXY(pieceIndex);

            List<DirectionMoves> moveSet = new(4);

            byte[] board = _board.board;
            bool capturablePieceExists = false;
            int directionIndex = 0;
            foreach (Vector2Int direction in directions)
            {
                var set = new DirectionMoves(directionIndex);
                moveSet.Add(set);
                bool pieceDetectedInDirection = false;

                //in case there are two pieces in a row, to break the possible moves
                int range = IsQueen(piece) ? 99 : 1;
                for (int i = 1; i <= range; i++)
                {
                    Vector2Int checkPos = p + direction * i;
                    if (!IsInBoardBounds(checkPos)) break;

                    int newPieceIndex = GetIndex(checkPos);
                    byte checkedPiece = board[newPieceIndex];
                    if (checkedPiece == EMPTY)
                    {
                        set.indexes.Add(newPieceIndex);
                        if (pieceDetectedInDirection)
                        {
                            set.hasCapturablePiece = true;
                            capturablePieceExists = true;
                        }
                        continue;
                    }

                    if (pieceDetectedInDirection || IsPieceOfPlayer(playerIndex, checkedPiece)) break;
                    pieceDetectedInDirection = true;
                    //need to remove previous empty spaces
                    set.indexes.Clear();
                    //increasing range, so I can check the next tile
                    range++;
                }
                //if I didn't detect a piece and I could capture one in a different direction,

                directionIndex++;
            }

            foreach (DirectionMoves set in moveSet)
            {
                if (capturablePieceExists)
                {
                    if (!set.hasCapturablePiece) set.indexes.Clear();
                }
                else
                {
                    if (!IsPieceDirection(playerIndex, piece, set.directionIndex)) set.indexes.Clear();
                }
            }

            return moveSet.SelectMany(m => m.indexes).ToList();
        }

        static bool IsPieceDirection(byte playerID, byte piece, int directionIndex)
        {
            return IsQueen(piece) ||
                (playerID == 1 && directionIndex > 1) ||
                (playerID == 2 && directionIndex < 2);
        }

        public bool HasRemovablePieces(byte piece, int pieceIndex, byte playerIndex)
        {
            Vector2Int p = GetXY(pieceIndex);

            int range = IsQueen(piece) ? 99 : 1;
            byte[] board = _board.board;
            //use getPieceMoves if the rule is to capture only forward
            foreach (Vector2Int direction in directions)
            {
                //in case there are two pieces in a row, to break the possible moves
                bool lastWasPiece = false;
                for (int i = 1; i <= range; i++)
                {
                    Vector2Int checkPos = p + direction * i;
                    if (!IsInBoardBounds(checkPos)) break;

                    int newPieceIndex = GetIndex(checkPos);
                    byte checkedPiece = board[newPieceIndex];
                    if (checkedPiece == EMPTY)
                    {
                        if (lastWasPiece) return true;
                        lastWasPiece = false;
                    }
                    else
                    {
                        if (lastWasPiece || IsPieceOfPlayer(playerIndex, checkedPiece)) break;
                        lastWasPiece = true;

                        //increasing range, so I can check the next tile
                        range++;
                    }
                }
            }
            return false;
        }

        //return if it deleted a piece
        //this method assumes that your move is valid,
        //including that the piece in between the move is the enemy piece
        public bool MakeMove(int pFromIndx, int pToIndx)
        {
            var board = _board.board;
            (board[pFromIndx], board[pToIndx]) = (board[pToIndx], board[pFromIndx]);

            //remove the piece in between, agnostic to piece type
            Vector2Int startPos = GetXY(pFromIndx);
            Vector2Int endPos = GetXY(pToIndx);
            Vector2Int dir = new(
                Math.Sign(endPos.X - startPos.X),
                Math.Sign(endPos.Y - startPos.Y)
            );
            bool removedPiece = false;

            Vector2Int checkedPos = startPos + dir;
            while (checkedPos != endPos)
            {
                int checkedIndex = GetIndex(checkedPos);

                if (board[checkedIndex] != EMPTY) removedPiece = true;

                board[checkedIndex] = EMPTY;
                checkedPos += dir;
            }
            return removedPiece;
        }

        public bool PlayerHasForcedCapture(byte playerID, int pieceToExclude = -1)
        {
            var board = _board.board;
            for (int i = 0; i < board.Length; i++)
            {
                if (pieceToExclude == i) continue;
                byte piece = board[i];
                if (IsPieceOfPlayer(playerID, piece) && HasRemovablePieces(piece, i, playerID)) return true;
            }
            return false;
        }

        public void PromotePiece(int pieceID)
        {
            _board.board[pieceID] += 2;
        }

        public bool IsPromoteable(int pieceIndex, byte playerId)
        {
            if(_board.board[pieceIndex] > 2) return false;
            if(playerId == 1)
            {
                return pieceIndex > 55 && pieceIndex < 64;
            }
            else
            {
                return pieceIndex > -1 && pieceIndex < 8;
            }
        }

        byte GetOtherPlayer(byte playerIndex)
        {
            return (byte)(playerIndex == 1 ? 2 : 1);
        }

        static Vector2Int GetXY(int index)
        {
            return new(index % BOARD_WIDTH, index / BOARD_WIDTH);
        }

        static int GetIndex(Vector2Int xy)
        {
            return xy.Y * BOARD_WIDTH + xy.X;
        }

        static bool IsQueen(byte piece)
        {
            return piece > 2;
        }

        static bool IsInBoardBounds(Vector2Int checkPos)
        {
            return checkPos.X >= 0 && checkPos.X < BOARD_WIDTH &&
                   checkPos.Y >= 0 && checkPos.Y < BOARD_WIDTH;
        }

        public static bool IsPieceOfPlayer(byte playerNum, byte piece)
        {
            return piece != EMPTY && !(playerNum % 2 == 0 ^ piece % 2 == 0);
        }

        public static bool IsPlayer2Piece(byte piece)
        {
            return piece != EMPTY && piece % 2 == 0;
        }
        public static bool IsPlayer1Piece(byte piece)
        {
            return piece != EMPTY && piece % 2 != 0;
        }

        #region Tests
        void ShowPossibleMoves(int pieceIndex)
        {
            List<int> possible = GetPossibleMoves(_board.board[pieceIndex], pieceIndex: pieceIndex, playerIndex: 1);
            foreach (var p in possible) _board.board[p] = 8;
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);
            Log.LogInfo("possible moves of: 18: " + string.Join(", ", possible), this, ConsoleColor.Magenta);
            foreach (var p in possible) _board.board[p] = 0;
        }
        public void SetTest1()
        {
            _board.board =
            [
                0,0,0,0,0,0,0,0,
                0,1,0,0,0,0,0,0,
                0,0,2,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(9);

        }

        public void SetTest2()
        {
            _board.board =
            [
                0,0,0,0,0,0,0,0,
                0,1,0,0,0,0,0,0,
                0,0,1,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(9);

        }

        public void SetTest3()
        {
            _board.board =
            [
                1,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,1,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,2,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(18);

        }

        public void SetTest4()
        {
            _board.board =
            [
                1,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(0);

        }

        public void SetTest5()
        {
            //piece index = 9

            _board.board =
            [
                1,0,0,0,0,0,0,0,
                0,2,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(0);

            _board.board =
            [
                0,0,0,0,0,0,0,0,
                0,2,0,0,0,0,0,0,
                0,0,1,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(18);

            _board.board =
            [
                1,0,1,0,1,0,1,0,
                0,1,0,1,0,1,0,1,
                1,0,0,0,0,0,1,0,
                0,0,0,0,0,1,0,0,
                0,0,0,0,2,0,0,0,
                0,0,0,2,0,0,0,2,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0
            ];
            ShowPossibleMoves(29);

            _board.board =
            [
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,1
            ];
            ShowPossibleMoves(63);

            _board.board =
            [
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,2,0,
                0,0,0,0,0,0,0,1
            ];
            ShowPossibleMoves(63);
        }

        public void SetTest6()
        {
            SetStartState();
            _board.board[27] = 2;
            _board.board[54] = 0;

            ShowPossibleMoves(18);

            //simulating move request
            //sender is player one
            byte playerID = 1;
            MakeMoveRequest makeMoveRequest = new MakeMoveRequest() { From = 18, To = 36 };
            int from = makeMoveRequest.From;
            int to = makeMoveRequest.To;

            byte pieceToMove = _board.board[from];
            //checking if the piece is of the sender
            if (!IsPieceOfPlayer(playerID, pieceToMove))
            {
                Log.LogInfo("Is not your Piece!", this, ConsoleColor.Red);
                return;
            }
            List<int> possibleMoves = GetPossibleMoves(pieceToMove, from, playerID);
            if (!possibleMoves.Any(m => m == to))
            {
                Log.LogInfo("Not a valid move!", this, ConsoleColor.Red);
                return;
            }
            Log.LogInfo("Can make move!", this, ConsoleColor.Green);

            bool destroyed = MakeMove(from, to);
            ShowPossibleMoves(to);
            Log.LogInfo("destroyed: " + destroyed, this, ConsoleColor.Yellow);
            if (!destroyed || !HasRemovablePieces(pieceToMove, to, playerID))
            {
                Log.LogInfo("Switching turn", this, ConsoleColor.Yellow);
            }
            else
            {
                Log.LogInfo("There are pieces left to remove", this, ConsoleColor.Yellow);
                //trying to move a different pawn while there is a required capture
                int otherPieceIndex = 16;
                Log.LogInfo("Trying to move pawn 16", this, ConsoleColor.Yellow);
                if (PlayerHasForcedCapture(playerID, pieceToExclude: otherPieceIndex))
                {
                    Log.LogInfo("Cannot, because some other piece should capture", this, ConsoleColor.Yellow);

                }
                else
                {
                    Log.LogInfo("Can Move", this, ConsoleColor.Yellow);

                }
            }

        }

        public void SetTest7(byte[] board, int moveFrom, int moveTo)
        {
            _board.board = board;

            ShowPossibleMoves(moveFrom);
            

            byte playerID = 1;

            byte pieceToMove = _board.board[moveFrom];
            //checking if the piece is of the sender
            if (!IsPieceOfPlayer(playerID, pieceToMove))
            {
                Log.LogInfo("Is not your Piece!", this, ConsoleColor.Red);
                return;
            }
            List<int> possibleMoves = GetPossibleMoves(pieceToMove, moveFrom, playerID);
            if (!possibleMoves.Any(m => m == moveTo))
            {
                Log.LogInfo("Not a valid move!", this, ConsoleColor.Red);
                return;
            }
            Log.LogInfo("Can make move!", this, ConsoleColor.Green);

            bool destroyed = MakeMove(moveFrom, moveTo);


            Log.LogInfo("destroyed: " + destroyed, this, ConsoleColor.Yellow);
            if (!destroyed || !HasRemovablePieces(pieceToMove, moveTo, playerID))
            {
                Log.LogInfo("Switching turn", this, ConsoleColor.Yellow);
                if(IsPromoteable(moveTo, playerID))
                {
                    Log.LogInfo("Promoting to queen", this, ConsoleColor.Green);
                    PromotePiece(moveTo);
                }
            }
            else
            {
                Log.LogInfo("There are pieces left to remove", this, ConsoleColor.Yellow);
            }
            ShowPossibleMoves(moveTo);

        }
        #endregion
    }
}
