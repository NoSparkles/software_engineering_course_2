import * as signalR from '@microsoft/signalr';

let connection = null;

export async function connectSpectator(hubUrl, onGameState, onSpectatorJoined, onSpectatorLeft) {
  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .build();

  connection.on('GameStateUpdate', (state) => onGameState && onGameState(state));
  connection.on('SpectatorJoined', (id, username) => onSpectatorJoined && onSpectatorJoined({ id, username }));
  connection.on('SpectatorLeft', (id) => onSpectatorLeft && onSpectatorLeft(id));
  // Also listen for game-specific events produced by existing hubs
  connection.on('ReceiveMove', (state) => onGameState && onGameState(state));
  connection.on('ReceiveBoard', (state) => onGameState && onGameState(state));
  connection.on('ReceiveRpsState', (state) => onGameState && onGameState(state));
  connection.on('SetPlayerColor', (payload) => {
    // player color mapping isn't the full game state, but forward for debugging/awareness
    onGameState && onGameState({ type: 'SetPlayerColor', payload });
  });

  await connection.start();
  // attach onclose to notify UI
  connection.onclose(error => {
    console.warn('Spectator connection closed', error);
  });
  return connection;
}

export function joinSpectate(gameType, code, spectatorId, username) {
  if (!connection) throw new Error('Not connected');
  return connection.invoke('JoinSpectate', gameType, code, spectatorId, username);
}

export function leaveSpectate(gameType, code, spectatorId) {
  if (!connection) return;
  return connection.invoke('LeaveSpectate', gameType, code, spectatorId);
}

export function disconnectSpectator() {
  if (!connection) return;
  connection.stop();
  connection = null;
}
