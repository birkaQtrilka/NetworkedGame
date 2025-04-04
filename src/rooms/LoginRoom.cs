﻿using shared;
using shared.src.protocol;

namespace server
{
	/**
	 * The LoginRoom is the first room clients 'enter' until the client identifies himself with a PlayerJoinRequest. 
	 * If the client sends the wrong type of request, it will be kicked.
	 *
	 * A connected client that never sends anything will be stuck in here for life,
	 * unless the client disconnects (that will be detected in due time).
	 */ 
	class LoginRoom : SimpleRoom
	{
		//arbitrary max amount just to demo the concept
		private const int MAX_MEMBERS = 50;

		public LoginRoom(TCPGameServer pOwner) : base(pOwner)
		{
		}

		protected override void addMember(TcpMessageChannel pMember)
		{
			base.addMember(pMember);
			UpdateMemberPing(pMember);
			//notify the client that (s)he is now in the login room, clients can wait for that before doing anything else
			RoomJoinedEvent roomJoinedEvent = new RoomJoinedEvent();
			roomJoinedEvent.room = RoomJoinedEvent.Room.LOGIN_ROOM;
			pMember.SendMessage(roomJoinedEvent);
			pMember.SendMessage(new Ping());
		}

		protected override void handleNetworkMessage(ASerializable pMessage, TcpMessageChannel pSender)
		{
			if (pMessage is PlayerJoinRequest)
			{
				handlePlayerJoinRequest(pMessage as PlayerJoinRequest, pSender);
			}
			else //if member sends something else than a PlayerJoinRequest
			{
				Log.LogInfo("Declining client, auth request not understood", this);

				//don't provide info back to the member on what it is we expect, just close and remove
				removeAndCloseMember(pSender);
			}
		}

		/**
		 * Tell the client he is accepted and move the client to the lobby room.
		 */
		private void handlePlayerJoinRequest (PlayerJoinRequest pMessage, TcpMessageChannel pSender)
		{
			Log.LogInfo("Moving new client to accepted...", this);

			PlayerJoinResponse playerJoinResponse = new();
			if(_server.GetPlayerInfo(p=> p.Name == pMessage.name).Count == 0)
			{
                playerJoinResponse.result = PlayerJoinResponse.RequestResult.ACCEPTED;
                pSender.SendMessage(playerJoinResponse);

				_server.GetPlayerInfo(pSender).Name = pMessage.name;
                removeMember(pSender);
                _server.GetLobbyRoom().AddMember(pSender);
            }
			else
			{
				playerJoinResponse.result= PlayerJoinResponse.RequestResult.DENIED;
				playerJoinResponse.reason = "Name already exists";
                pSender.SendMessage(playerJoinResponse);

            }

        }

	}
}
