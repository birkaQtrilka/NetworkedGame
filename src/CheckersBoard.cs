using shared;
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
                new (a.X + b.X, a.Y + b.Y);

            public static Vector2Int operator *(Vector2Int a, int scalar) =>
                new (a.X * scalar, a.Y * scalar);

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
                if ( (i + ((i / 8) % 2)) % 2 == 0) board[i] = PLAYER_1_PAWN;
                else board[i] = EMPTY;

            for (int i = 24; i < 40; i++)
                _board.board[i] = EMPTY;

            for (int i = 40; i < BOARD_COUNT; i++)
                if ((i + ((i / 8) % 2)) % 2 == 0) board[i] = PLAYER_1_PAWN;
                else board[i] = EMPTY;
        }
        
        #region Tests
        public void SetTest1()
        {
            //piece index = 9

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
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

            List<int> possibleMoves = GetPossibleMoves(PLAYER_1_PAWN, pieceIndex: 9, playerIndex: 1);
            Log.LogInfo(string.Join(", ", possibleMoves), this, ConsoleColor.Magenta);
        }

        public void SetTest2()
        {
            //piece index = 9

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
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

            List<int> possibleMoves = GetPossibleMoves(PLAYER_1_PAWN, pieceIndex: 9, playerIndex: 1);
            Log.LogInfo(string.Join(", ", possibleMoves), this, ConsoleColor.Magenta);
        }

        public void SetTest3()
        {
            //piece index = 9

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
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

            List<int> possibleMoves = GetPossibleMoves(PLAYER_1_PAWN, pieceIndex: 18, playerIndex: 1);
            Log.LogInfo(string.Join(", ", possibleMoves), this, ConsoleColor.Magenta);
        }
        public void SetTest4()
        {
            //piece index = 9

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
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

            List<int> possibleMoves = GetPossibleMoves(PLAYER_1_PAWN, pieceIndex: 0, playerIndex: 1);
            Log.LogInfo(string.Join(", ", possibleMoves), this, ConsoleColor.Magenta);
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
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

            List<int> possibleMoves = GetPossibleMoves(PLAYER_1_PAWN, pieceIndex: 0, playerIndex: 1);
            Log.LogInfo(string.Join(", ", possibleMoves), this, ConsoleColor.Magenta);
        }

        public void SetTest6()
        {
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
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

            List<int> possible = GetPossibleMoves(PLAYER_1_PAWN, pieceIndex: 0, playerIndex: 1);
            Log.LogInfo(string.Join(", ", possible), this, ConsoleColor.Magenta);

            //simulating move request
            //sender is player one
            byte senderID = 1;
            MakeMoveRequest makeMoveRequest = new MakeMoveRequest() { From= 0, To = 18};
            int from = makeMoveRequest.From;
            int to = makeMoveRequest.To;

            byte pieceToMove = _board.board[from];
            //checking if the piece is of the sender
            if (!IsPieceOfPlayer(senderID, pieceToMove))
            {
                Log.LogInfo("Is not your Piece!", this, ConsoleColor.Red);
                return;
            }
            List<int> possibleMoves = GetPossibleMoves(pieceToMove, from, senderID);
            if (!possibleMoves.Any(m => m == to))
            {
                Log.LogInfo("Not a valid move!", this, ConsoleColor.Red);
                return;
            }
            Log.LogInfo("Can make move!", this, ConsoleColor.Green);

            MakeMove(from, to);
            Log.LogInfo(_board.ToString(), this, ConsoleColor.Yellow);

        }
        #endregion

        public List<int> GetPossibleMoves(byte piece, int pieceIndex, byte playerIndex)
        {
            //converting to x and y because it's easier to check bounds and move through board
            Vector2Int p = GetXY(pieceIndex);

            int range = IsQueen(piece) ? 99 : 1;
            List<int> moves = [];
            byte[] board = _board.board;

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
                        lastWasPiece = false;
                        moves.Add(newPieceIndex);
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
            return moves;
        }

        //this method assumes that your move is valid,
        //including that the piece in between the move is the enemy piece
        public void MakeMove(int pFromIndx, int pToIndx)
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
            Vector2Int checkedPos = startPos + dir;
            while(checkedPos != endPos)
            {
                int checkedIndex = GetIndex(checkedPos);
                board[checkedIndex] = EMPTY;
                checkedPos += dir;
            }
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
            return checkPos.X >= 0 && checkPos.X <= BOARD_WIDTH &&
                   checkPos.Y >= 0 && checkPos.Y <= BOARD_WIDTH;
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
    }
}
