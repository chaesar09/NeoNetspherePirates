﻿using BlubLib.DotNetty.Handlers.MessageHandling;
using NeoNetsphere.Game.GameRules;
using NeoNetsphere.Network.Data.Game;
using NeoNetsphere.Network.Message.Game;
using NeoNetsphere.Network.Message.GameRule;
using Netsphere;
using Netsphere.Game.GameRules;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NeoNetsphere.Network.Services
{
    internal class RoomService : ProudMessageHandler
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(RoomService));

        [MessageHandler(typeof(GameAvatarDurabilityDecreaseReqMessage))]
        public void GameAvatarDurabilityDecreaseReq(GameSession session, GameAvatarDurabilityDecreaseReqMessage message)
        {
        }

        [MessageHandler(typeof(RoomInfoRequestReqMessage))]
        public void RoomInfoRequestReq(GameSession session, RoomInfoRequestReqMessage message)
        {
            var plr = session.Player;
            var room = plr.Channel.RoomManager[message.RoomId];
            if (room == null)
                return;
            session.SendAsync(new RoomInfoRequestAck2Message()
            {
                Info = new RoomInfoRequestDto()
                {
                    MasterName = room.Master.Account.Nickname,
                    MasterLevel = room.Master.Level,
                    ScoreLimit = room.Options.ScoreLimit,
                    TimeLimit = room.Options.TimeLimit,
                    State = room.GameState,
                    IsMasterInClan = false, //Todo
                    Unk8 = 1,
                    Unk9 = 1
                }
            });
        }

        [MessageHandler(typeof(RoomEnterPlayerReqMessage))]
        public void CEnterPlayerReq(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
                return;
            }

            plr.Room.ChangeMasterIfNeeded(plr);
            plr.Room.ChangeHostIfNeeded(plr);

            session.SendAsync(new GameChangeStateAckMessage(plr.Room.GameState));
            session.SendAsync(new GameChangeSubStateAckMessage(plr.Room.SubGameState));
            
            plr.Room.Broadcast(new RoomEnterPlayerForBookNameTagsAckMessage()
            {
                AccountId = (long)plr.Account.Id,
                Team = plr.RoomInfo.Team.Team,
                PlayerGameMode = plr.RoomInfo.Mode,
                Exp = plr.TotalExperience,
                Nickname = plr.Account.Nickname,
                Unk1 = 1,
                Unk2 = 1
            });
            
            plr.Room.Broadcast(new RoomEnterPlayerInfoListForNameTagAckMessage(0));

            if (plr != plr.Room.Master)
                plr.Session.SendAsync(new RoomChangeMasterAckMessage(plr.Room.Master.Account.Id));
            if (plr != plr.Room.Host)
                plr.Session.SendAsync(new RoomChangeRefereeAckMessage(plr.Room.Host.Account.Id));
            
            plr.Room.BroadcastBriefing();
            plr.Room.GameRuleManager.GameRule.UpdateTime(plr);
        }

        [MessageHandler(typeof(RoomMakeReqMessage))]
        public void CMakeRoomReq(GameSession session, RoomMakeReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room != null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                return;
            }
            Logger.Information("CreateRoom || Nickname:{nick}({accid}) Room: {mode}, {mapid}", plr.Account.Nickname,
                plr.Account.Id, (GameRule)message.GameRule, message.Map_ID);
            if (!plr.Channel.RoomManager.GameRuleFactory.Contains((GameRule)message.GameRule))
            {
                Logger.ForAccount(plr)
                    .Error("Game rule {gameRule} does not exist", (GameRule)message.GameRule);
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            
            //toDo
            message.Player_Limit = 1;

            var isfriendly = true;
            var isbalanced = false;
            var isburning = false;
            var isWithoutStats = false;

            var room = plr.Channel.RoomManager.Create_2(new RoomCreationOptions
            {
                Name = message.rName,
                GameRule = (GameRule)message.GameRule,
                PlayerLimit = message.Player_Limit,
                TimeLimit = TimeSpan.FromMinutes(message.Time),
                ScoreLimit = (ushort)message.Points,
                Password = message.rPassword,
                IsFriendly = isfriendly,
                IsBalanced = isbalanced,
                IsBurning = isburning,
                IsWithoutStats = isWithoutStats,
                MapID = message.Map_ID,
                MinLevel = 0,
                MaxLevel = 100,
                ItemLimit = (byte)message.Weapon_Limit,
                IsNoIntrusion = false,
                Spectator = message.SpectatorCount,
                hasSpectator = message.SpectatorCount > 1 ? true : false,
                UniqueID = message.Unk3,
                ServerEndPoint =
                    new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.RelayListener.Port),
                Creator = plr
            }, RelayServer.Instance.P2PGroupManager.Create(true));
            try
            {
                room.Join(plr);
            }
            catch (RoomAccessDeniedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
        }

        [MessageHandler(typeof(RoomMakeReq2Message))]
        public void CMakeRoomReq2(GameSession session, RoomMakeReq2Message message)
        {
            var plr = session.Player;
            if (plr.Room != null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                return;
            }
            Logger.Information("CreateRoom || Nickname:{nick}({accid}) Room: {mode}, {mapid}", plr.Account.Nickname,
                plr.Account.Id, (GameRule) message.GameRule, message.Map_ID);
            if (!plr.Channel.RoomManager.GameRuleFactory.Contains((GameRule) message.GameRule))
            {
                Logger.ForAccount(plr)
                    .Error("Game rule {gameRule} does not exist", (GameRule) message.GameRule);
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }

            var israndom = false;
            if ((GameRule) message.GameRule != GameRule.Practice &&
                (GameRule) message.GameRule != GameRule.CombatTrainingTD &&
                (GameRule) message.GameRule != GameRule.CombatTrainingDM)
            {
                if (message.Map_ID == 228 && (GameRule) message.GameRule == GameRule.BattleRoyal) //random br
                {
                    israndom = true;
                    message.Map_ID = 112;
                }
                else if (message.Map_ID == 229 && (GameRule) message.GameRule == GameRule.Chaser) //random chaser
                {
                    israndom = true;
                    message.Map_ID = 225;
                }
                else if (message.Map_ID == 231 && (GameRule) message.GameRule == GameRule.Deathmatch
                ) //random deathmatch
                {
                    israndom = true;
                    message.Map_ID = 20;
                }
                else if (message.Map_ID == 230 && (GameRule) message.GameRule == GameRule.Touchdown) //random touchdown
                {
                    israndom = true;
                    message.Map_ID = 66;
                }
                var map = GameServer.Instance.ResourceCache.GetMaps().GetValueOrDefault(message.Map_ID);
                if (map == null)
                {
                    Logger.ForAccount(plr)
                        .Error("Map {map} does not exist", message.Map_ID);
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                if (!map.GameRules.Contains((GameRule) message.GameRule))
                {
                    Logger.ForAccount(plr)
                        .Error("Map {mapId}({mapName}) is not available for game rule {gameRule}", map.Id, map.Name,
                            (GameRule) message.GameRule);

                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
            }
            if (message.Player_Limit < 1)
                message.Player_Limit = 1;

            if ((GameRule) message.GameRule == GameRule.CombatTrainingTD ||
                (GameRule) message.GameRule == GameRule.CombatTrainingDM)
                message.Player_Limit = 12;
            
            if ((GameRule) message.GameRule == GameRule.CombatTrainingDM ||
                (GameRule) message.GameRule == GameRule.CombatTrainingTD ||
                (GameRule) message.GameRule == GameRule.Practice)
                message.FMBURNMode = 1;

            var isfriendly = false;
            var isbalanced = true;
            var isburning = false;
            var isWithoutStats = false;
            switch (message.FMBURNMode)
            {
                case 0:
                    isbalanced = true;
                    isfriendly = false;
                    break;
                case 1:
                    isbalanced = isfriendly = true;
                    break;
                case 2:
                    isbalanced = true;
                    isfriendly = false;
                    isburning = true;
                    break;
                case 3:
                    isburning = true;
                    isbalanced = isfriendly = true;
                    break;
                case 4:
                    isWithoutStats = true;
                    break;
                case 5:
                    isWithoutStats = isfriendly = true;
                    break;
            }

            var room = plr.Channel.RoomManager.Create_2(new RoomCreationOptions
            {
                Name = message.rName,
                GameRule = (GameRule) message.GameRule,
                PlayerLimit = message.Player_Limit,
                TimeLimit = TimeSpan.FromMinutes(message.Time),
                ScoreLimit = (ushort) message.Points,
                Password = message.rPassword,
                IsFriendly = isfriendly,
                IsBalanced = isbalanced,
                IsBurning = isburning,
                IsWithoutStats = isWithoutStats,
                MapID = message.Map_ID,
                MinLevel = 0,
                MaxLevel = 100,
                ItemLimit = (byte) message.Weapon_Limit,
                IsNoIntrusion = false,
                Spectator = message.SpectatorCount,
                IsRandom = israndom,
                hasSpectator = message.SpectatorCount > 1 ? true : false,
                UniqueID = message.Unk3,
                ServerEndPoint =
                    new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.RelayListener.Port),
                Creator = plr
            }, RelayServer.Instance.P2PGroupManager.Create(true));
            try
            {
                room.Join(plr);
            }
            catch (RoomAccessDeniedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
        }

        [MessageHandler(typeof(RoomChoiceMasterChangeReqMessage))]
        public void RoomChoiceMasterChangeReq(GameSession session, RoomChoiceMasterChangeReqMessage message)
        {
            var plr = session.Player;
            if (plr != plr.Room.Master)
                return;

            var targetplayer = GameServer.Instance.PlayerManager.FirstOrDefault(target =>
                target.Room == plr.Room && target.Account.Id == message.AccountId);
            if (targetplayer == null)
            {
                plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }

            plr.Room.ChangeMasterIfNeeded(targetplayer, true);
            plr.Room.ChangeHostIfNeeded(targetplayer, true);
        }

        [MessageHandler(typeof(RoomChoiceTeamChangeReqMessage))]
        public void CMixChangeTeamReq(GameSession session, RoomChoiceTeamChangeReqMessage message)
        {
            var plr = session.Player;
            if (plr != plr.Room.Master || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;

            var plrToMove = plr.Room.Players.GetValueOrDefault(message.PlayerToMove);
            var plrToReplace = plr.Room.Players.GetValueOrDefault(message.PlayerToReplace);
            var fromTeam = plr.Room.TeamManager[message.FromTeam];
            var toTeam = plr.Room.TeamManager[message.ToTeam];

            if (fromTeam == null || toTeam == null || plrToMove == null ||
                fromTeam != plrToMove.RoomInfo.Team ||
                plrToReplace != null && toTeam != plrToReplace.RoomInfo.Team)
            {
                session.SendAsync(new RoomMixedTeamBriefingInfoAckMessage());
                return;
            }

            if (plrToReplace == null)
            {
                try
                {
                    toTeam.Join(plrToMove);
                }
                catch (TeamLimitReachedException)
                {
                    session.SendAsync(new RoomChoiceTeamChangeFailAckMessage());
                }
            }
            else
            {
                fromTeam.Leave(plrToMove);
                toTeam.Leave(plrToReplace);
                fromTeam.Join(plrToReplace);
                toTeam.Join(plrToMove);

                plr.Room.Broadcast(new RoomChoiceTeamChangeAckMessage(plrToMove.Account.Id, plrToReplace.Account.Id,
                    fromTeam.Team, toTeam.Team));

                // SMixChangeTeamAckMessage alone doesn't seem to change the player list
                plr.Room.BroadcastBriefing();
            }
        }

        [MessageHandler(typeof(InGamePlayerResponseReqMessage))]
        public void InGamePlayerResponseReq(GameSession session, InGamePlayerResponseReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.RoomInfo?.State == PlayerState.Lobby) return;
            if (plr.RoomInfo != null)
                plr.RoomInfo.State = PlayerState.Alive;
            //Todo
        }
        
        [MessageHandler(typeof(RoomEnterReqMessage))]
        public void CGameRoomEnterReq(GameSession session, RoomEnterReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room != null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                return;
            }
            var room = plr.Channel.RoomManager[message.RoomId];
            if (room == null)
            {
                Logger.ForAccount(plr)
                    .Error("Room {roomId} in channel {channelId} not found", message.RoomId, plr.Channel.Id);
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                return;
            }

            if (room.IsChangingRules)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.RoomChangingRules));
                return;
            }

            if (!string.IsNullOrEmpty(room.Options.Password) && !room.Options.Password.Equals(message.Password))
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.PasswordError));
                return;
            }

            try
            {
                room.Join(plr);
            }
            catch (RoomAccessDeniedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
        }

        [MessageHandler(typeof(RoomLeaveReqMessage))]
        public void CJoinTunnelInfoReq(GameSession session)
        {
            var plr = session.Player;
            plr?.Room?.Leave(plr);
        }

        [MessageHandler(typeof(RoomTeamChangeReqMessage))]
        public void CChangeTeamReq(GameSession session, RoomTeamChangeReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;

            try
            {
                plr.Room.TeamManager.ChangeTeam(plr, message.Team);
            }
            catch (RoomException ex)
            {
                Logger.ForAccount(plr)
                    .Error(ex, "Failed to change team to {team}", message.Team);
            }
        }

        [MessageHandler(typeof(RoomPlayModeChangeReqMessage))]
        public void CPlayerGameModeChangeReq(GameSession session, RoomPlayModeChangeReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;
            try
            {
                plr.Room.TeamManager.ChangeMode(plr, message.Mode);
            }
            catch (RoomException ex)
            {
                Logger.ForAccount(plr)
                    .Error(ex, "Failed to change mode to {mode}", message.Mode);
            }
        }
        
        [MessageHandler(typeof(GameLoadingSuccessReqMessage))]
        public void CLoadingSucceeded(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            plr.RoomInfo.HasLoaded = true;
            plr.RoomInfo.State = PlayerState.Waiting;
            plr.Room.Broadcast(new RoomGameEndLoadingAckMessage(plr.Account.Id));

            if (!plr.Room.hasStarted)
            {
                plr.Room.BroadcastBriefing();
                return;
            }

            foreach(var member in plr.Room.Players.Where(x => x.Value.RoomInfo.HasLoaded))
            {
                plr.Session.SendAsync(new RoomGameEndLoadingAckMessage(member.Value.Account.Id));
            }
            
            plr.RoomInfo.State = plr.RoomInfo.Mode == PlayerGameMode.Spectate ? PlayerState.Spectating : PlayerState.Alive;
            plr.Room.GameRuleManager.GameRule.IntrudeCompleted(plr);
        }

        [MessageHandler(typeof(RoomIntrudeRoundReqMessage))]
        public void CIntrudeRoundReq(GameSession session)
        {
            var plr = session.Player;
            var room = plr?.Room;
            if (room != null && room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                if (room.IsChangingRules)
                {
                    session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                    return;
                }

                if (room.GameState == GameState.Result || room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.EnteringResult))
                {
                    //Todo, find proper result Id
                    session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                    return;
                }

                if (room.isPreparing || !room.hasStarted)
                {
                    session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
                    return;
                }

                plr.Session.SendAsync(new RoomGameLoadingAckMessage());
            }
        }

        [MessageHandler(typeof(RoomIntrudeRoundReq2Message))]
        public void CIntrudeRoundReq2(GameSession session)
        {
            var plr = session.Player;
            var room = plr?.Room;
            if (room != null && room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                if (room.IsChangingRules)
                {
                    session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                    return;
                }

                if (room.GameState == GameState.Result || room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.EnteringResult))
                {
                    //Todo, find proper result Id
                    session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                    return;
                }

                if (room.isPreparing || !room.hasStarted)
                {
                    session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
                    return;
                }

                plr.Session.SendAsync(new RoomGameLoadingAckMessage());
            }
        }

        [MessageHandler(typeof(RoomBeginRoundReq2Message))]
        public void CBeginRoundReq2(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;

            if (plr.Room.IsChangingRules)
            {
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                return;
            }

            var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;

            if (stateMachine.CanFire(GameRuleStateTrigger.StartPrepare))
                stateMachine.Fire(GameRuleStateTrigger.StartPrepare);
            else
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
        }

        [MessageHandler(typeof(RoomReadyRoundReq2Message))]
        public void CReadyRoundReq2(GameSession session)
        {
            var plr = session.Player;

            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;

            if (plr.Room.IsChangingRules)
            {
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                return;
            }
            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
            }
            else
            {
                plr.RoomInfo.IsReady = !plr.RoomInfo.IsReady;
                plr.Room.Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
            }
            if(plr.Room.hasStarted)
                plr.Session.SendAsync(new RoomGameLoadingAckMessage());
        }


        [MessageHandler(typeof(RoomBeginRoundReqMessage))]
        public void CBeginRoundReq(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;
            if (plr.Room.IsChangingRules)
            {
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                return;
            }
            var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;

            if (stateMachine.CanFire(GameRuleStateTrigger.StartPrepare))
                stateMachine.Fire(GameRuleStateTrigger.StartPrepare);
            else
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
        }

        [MessageHandler(typeof(RoomReadyRoundReqMessage))]
        public void CReadyRoundReq(GameSession session)
        {
            var plr = session.Player;

            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;

            if (plr.Room.IsChangingRules)
            {
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                return;
            }
            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
            }
            else
            {
                plr.RoomInfo.IsReady = !plr.RoomInfo.IsReady;
                plr.Room.Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
            }
            if (plr.Room.hasStarted)
                plr.Session.SendAsync(new RoomGameLoadingAckMessage());
        }
        
        [MessageHandler(typeof(GameEventMessageReqMessage))]
        public void CEventMessageReq(GameSession session, GameEventMessageReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            plr.Room.Broadcast(new GameEventMessageAckMessage(message.Event, session.Player.Account.Id, message.Unk1,
                message.Value, ""));

            if (!plr.Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing))
                return;

            if (plr.RoomInfo.State != PlayerState.Lobby)
                return;

            if (!plr.Room.hasStarted || plr.RoomInfo.HasLoaded)
                return;

            plr.Session?.SendAsync(new RoomGameLoadingAckMessage());
        }


        [MessageHandler(typeof(RoomItemChangeReqMessage))]
        public void CItemsChangeReq(GameSession session, RoomItemChangeReqMessage message)
        {
            var plr = session.Player;
            plr.Room?.Broadcast(new RoomChangeItemAckMessage(message.Unk1, message.Unk2));
        }

        [MessageHandler(typeof(GameAvatarChangeReqMessage))]
        public void CAvatarChangeReq(GameSession session, GameAvatarChangeReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.HalfTime && 
                plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }

            plr.Room.Broadcast(new GameAvatarChangeAckMessage(message.Unk1, message.Unk2));
        }

        [MessageHandler(typeof(RoomChangeRuleNotifyReqMessage))]
        public void CChangeRuleNotifyReq(GameSession session, RoomChangeRuleNotifyReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            try
            {
                session.Player.Room.ChangeRules(message.Settings);
            }
            catch (Exception)
            {
                session.SendAsync(new RoomChangeRuleFailAckMessage {Result = 1});
            }
        }

        [MessageHandler(typeof(RoomChangeRuleNotifyReq2Message))]
        public void CChangeRuleNotifyReq2(GameSession session, RoomChangeRuleNotifyReq2Message message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            try
            {
                session.Player.Room.ChangeRules(message.Settings);
            }
            catch (Exception)
            {
                session.SendAsync(new RoomChangeRuleFailAckMessage {Result = 1});
            }
        }

        [MessageHandler(typeof(RoomLeaveReguestReqMessage))]
        public void CLeavePlayerRequestReq(GameSession session, RoomLeaveReguestReqMessage message)
        {
            var plr = session.Player;
            var room = plr.Room;

            if (room == null)
                return;

            if (message.Reason == RoomLeaveReason.ModeratorKick || message.Reason == RoomLeaveReason.Kicked)
            {
                // Only the master can kick people and kick is only allowed in the lobby
                if ((room.Master != plr || plr.Account.SecurityLevel < SecurityLevel.User) &&
                    !room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting))
                {
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
            }
            else
            {
                // Dont allow any other reasons for now
                return;
            }

            var targetPlr = room.Players.GetValueOrDefault(message.AccountId);
            if (targetPlr == null)
                return;

            room.Leave(targetPlr, message.Reason);
        }


        [MessageHandler(typeof(RoomQuickJoinReqMessage))]
        public async Task QuickJoinReq(GameSession session, RoomQuickJoinReqMessage message)
        {
            try
            {
                var rooms = new Dictionary<Room, int>();

                foreach (var room in session.Player.Channel.RoomManager)
                {
                    if (room.Options.Password == "")
                    {
                        if (!room.Options.GameRule.Equals((GameRule)message.GameRule))
                            continue;

                        if (!room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting) &&
                            (!room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing) ||
                             room.Options.IsNoIntrusion))
                            continue;

                        var priority = 0;
                        priority += System.Math.Abs(room.TeamManager[Team.Alpha].Players.Count() -
                                                    room.TeamManager[Team.Beta].Players
                                                        .Count()); // Calculating team balance

                        if (room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.SecondHalf))
                        {
                            if (room.Options.TimeLimit.TotalSeconds / 2 -
                                room.GameRuleManager.GameRule.RoundTime.TotalSeconds <=
                                15) // If only 15 seconds are left...
                            {
                                priority -= 3; // ...lower the room priority
                            }
                        }

                        rooms.Add(room, priority);
                    }
                }
                var roomList = rooms.ToList();

                if (roomList.Any())
                {
                    roomList.Sort((room1, room2) => room2.Value.CompareTo(room1.Value));
                    roomList.First().Key.Join(session.Player);

                    return; // We don't message the Client here, because "Room.Join(...)" already does it.
                }
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
            }
            catch (Exception)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ServerError));
            }
        }

        [MessageHandler(typeof(TutorialCompletedReqMessage))]
        public void TutorialCompletedReq(GameSession session, TutorialCompletedReqMessage message)
        {
            session.Player.TutorialState = 1;
            session.SendAsync(new TutorialCompletedAckMessage {Unk = 0});
        }

        #region Scores

        [MessageHandler(typeof(ScoreKillReqMessage))]
        public void CScoreKillReq(GameSession session, ScoreKillReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;
            
            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Score.Target;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            room.GameRuleManager.GameRule.OnScoreKill(killer, null, plr, message.Score.Weapon, message.Score.Target,
                message.Score.Killer, null);
        }

        [MessageHandler(typeof(ScoreKillAssistReqMessage))]
        public void CScoreKillAssistReq(GameSession session, ScoreKillAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;

            var room = plr.Room;
            var assist = room.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            if (assist != null)
                assist.RoomInfo.PeerId = message.Score.Assist;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            room.GameRuleManager.GameRule.OnScoreKill(killer, assist, plr, message.Score.Weapon, message.Score.Target,
                message.Score.Killer, message.Score.Assist);
        }

        [MessageHandler(typeof(ScoreOffenseReqMessage))]
        public void CScoreOffenseReq(GameSession session, ScoreOffenseReqMessage message)
        {
            var plr = session.Player;
            var room = plr.Room;

            if (plr.Room == null || !plr.Room.hasStarted)
                return;

            var target = room.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Score.Target;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            switch (room.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule) room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr,
                        message.Score.Weapon /*, message.Score.Target, message.Score.Killer, null*/);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule) room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr,
                        message.Score.Weapon /*, message.Score.Target, message.Score.Killer, null*/);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule) room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
            }
        }

        [MessageHandler(typeof(ScoreOffenseAssistReqMessage))]
        public void CScoreOffenseAssistReq(GameSession session, ScoreOffenseAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;

            var room = plr.Room;

            var assist = room.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            if (assist != null)
                assist.RoomInfo.PeerId = message.Score.Assist;

            var target = room.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Score.Target;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            switch (room.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule) room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr,
                        message.Score.Weapon /*, message.Score.Target, message.Score.Killer, message.Score.Assist*/);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule) room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr,
                        message.Score.Weapon /*, message.Score.Target, message.Score.Killer, message.Score.Assist*/);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule) room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
            }
        }

        [MessageHandler(typeof(ScoreDefenseReqMessage))]
        public void CScoreDefenseReq(GameSession session, ScoreDefenseReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;
            
            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Score.Target;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            switch (room.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule) room.GameRuleManager.GameRule).OnScoreDefense(killer, null, plr,
                        message.Score.Weapon /*, message.Score.Weapon, message.Score.Target, message.Score.Killer, null*/);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule) room.GameRuleManager.GameRule).OnScoreDefense(killer, null, plr,
                        message.Score.Weapon /*, message.Score.Weapon, message.Score.Target, message.Score.Killer, null*/);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule) room.GameRuleManager.GameRule).OnScoreDefense(killer, null, plr,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
            }
        }

        [MessageHandler(typeof(ScoreDefenseAssistReqMessage))]
        public void CScoreDefenseAssistReq(GameSession session, ScoreDefenseAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;

            var room = plr.Room;

            var assist = room.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            if (assist != null)
                assist.RoomInfo.PeerId = message.Score.Assist;

            var target = room.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Score.Target;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            switch (room.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule) room.GameRuleManager.GameRule).OnScoreDefense(killer, assist, plr,
                        message.Score
                            .Weapon /*, message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist*/);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule) room.GameRuleManager.GameRule).OnScoreDefense(killer, assist, plr,
                        message.Score
                            .Weapon /*, message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist*/);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule) room.GameRuleManager.GameRule).OnScoreDefense(killer, assist, plr,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
            }
        }

        [MessageHandler(typeof(ScoreTeamKillReqMessage))]
        public void CScoreTeamKillReq(GameSession session, ScoreTeamKillReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;

            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Score.Target;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            room.GameRuleManager.GameRule.OnScoreKill(killer, null, plr, message.Score.Weapon, message.Score.Target,
                message.Score.Killer, null);
        }

        [MessageHandler(typeof(ScoreHealAssistReqMessage))]
        public void CScoreHealAssistReq(GameSession session, ScoreHealAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;
            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.Id.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Id;

            room.GameRuleManager.GameRule.OnScoreHeal(plr, message.Id);
        }

        [MessageHandler(typeof(ScoreSuicideReqMessage))]
        public void CScoreSuicideReq(GameSession session, ScoreSuicideReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;
            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.Id.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.Id;

            plr.RoomInfo.PeerId = message.Id;

            room.GameRuleManager.GameRule.OnScoreSuicide(plr, message.Id);
        }

        [MessageHandler(typeof(ScoreReboundReqMessage))]
        public void CScoreReboundReq(GameSession session, ScoreReboundReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;

            var room = plr.Room;
            
            var oldPlr = room.Players.GetValueOrDefault(message.OldId.AccountId);
            if (oldPlr != null)
                oldPlr.RoomInfo.PeerId = message.OldId;

            var newPlr = room.Players.GetValueOrDefault(message.NewId.AccountId);
            if (newPlr != null)
                newPlr.RoomInfo.PeerId = message.NewId;


            switch (room.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule) room.GameRuleManager.GameRule).OnScoreRebound(newPlr, oldPlr, message.NewId, message.OldId);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule) room.GameRuleManager.GameRule).OnScoreRebound(newPlr, oldPlr, message.NewId, message.OldId);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule) room.GameRuleManager.GameRule).OnScoreRebound(newPlr, oldPlr,
                        message.NewId, message.OldId);
                    break;
            }
        }

        [MessageHandler(typeof(ScoreGoalReqMessage))]
        public void CScoreGoalReq(GameSession session, ScoreGoalReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || !plr.Room.hasStarted)
                return;
            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.PeerId.AccountId);
            if (target != null)
                target.RoomInfo.PeerId = message.PeerId;

            switch (room.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule) room.GameRuleManager.GameRule).OnScoreGoal(target);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule) room.GameRuleManager.GameRule).OnScoreGoal(target);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule) room.GameRuleManager.GameRule).OnScoreGoal(target, message.PeerId);
                    break;
            }
        }

        [MessageHandler(typeof(SlaughterAttackPointReqMessage))]
        public void SlaughterAttackPointReq(GameSession session, SlaughterAttackPointReqMessage message)
        {
            //session.SendAsync(new SlaughterAttackPointAckMessage()
            //{
            //    Unk1 = message.Unk1,
            //    Unk2 = message.Unk2,
            //    AccountId = message.AccountId,
            //});
            return;
        }
        [MessageHandler(typeof(SlaughterHealPointReqMessage))]
        public void SlaughterHealPointReq(GameSession session, SlaughterHealPointReqMessage message)
        {
            //session.SendAsync(new SlaughterHealPointReqMessage()
            //{
            //    Unk = message.Unk,
            //});
            return;
        }
        
        [MessageHandler(typeof(ScoreMissionScoreReqMessage))]
        public void ScoreMissionScoreReq(GameSession session, ScoreMissionScoreReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.GameRule != GameRule.Practice)
                return;
            
            session.SendAsync(new ScoreMissionScoreAckMessage() {AccountId = session.Player.Account.Id, Score = message.Score});
            
            return;
        }
        #endregion

        [MessageHandler(typeof(ArcadeStageFailedReqMessage))]
        public void ArcadeStageFailedReq(GameSession session, ArcadeStageFailedReqMessage message)
        {
            var plr = session.Player;
            if(plr.Room == null || plr.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde)
                return;

            plr.Room.GameRuleManager.GameRule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
        }

        [MessageHandler(typeof(InGameItemGetReqMessage))]
        public void InGameItemGetReq(GameSession session, InGameItemGetReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde)
                return;
            session.SendAsync(new InGameItemGetAckMessage()
            {
                Unk1 = (long)plr.Account.Id,
                Unk2 = message.Unk1,
                Unk3 = message.Unk2
            });
        }
        [MessageHandler(typeof(InGameItemDropReqMessage))]
        public void InGameItemDropReq(GameSession session, InGameItemDropReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null || plr.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde)
                return;

            //session.SendAsync(new InGameItemDropAckMessage()
            //{
            //    //Todo
            //});
        }
        
    }
}
