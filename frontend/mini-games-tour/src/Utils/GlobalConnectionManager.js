class GlobalConnectionManager {
  constructor() {
    this.connections = new Map(); // Map of connection type to connection
  }

  // Register a connection
  registerConnection(type, connection, metadata = {}) {
    // Store metadata with the connection
    connection.connectionData = metadata;
    this.connections.set(type, connection);
  }

  // Unregister a connection
  unregisterConnection(type) {
    this.connections.delete(type);
  }

  // Get a connection
  getConnection(type) {
    return this.connections.get(type);
  }

  // Call LeaveRoom on all active connections
  async leaveAllRooms({ showUiDelay = true } = {}) {
    const promises = [];
    for (const [type, connection] of this.connections) {
      if (connection && connection.state === "Connected") {
        // Get connection metadata to extract parameters
        const connectionData = connection.connectionData || {};
        const gameType = connectionData.gameType;
        const roomCode = connectionData.roomCode;
        const playerId = connectionData.playerId;
       
        if (!gameType || !roomCode || !playerId) {
          console.error(`Missing parameters for LeaveRoom - gameType: ${gameType}, roomCode: ${roomCode}, playerId: ${playerId}`);
          continue;
        }
        
        const promise = connection.invoke("LeaveRoom", gameType, roomCode, playerId).catch(err => {
          console.warn(`LeaveRoom failed on ${type} connection:`, err);
        });
        promises.push(promise);
      }
    }
    await Promise.all(promises);
    // Always delay after leaving rooms if showUiDelay is true
    if (showUiDelay) {
      await new Promise(resolve => setTimeout(resolve, 1700));
    }
  }

  // Check if any connections are active
  hasActiveConnections() {
    return Array.from(this.connections.values()).some(
      connection => connection && connection.state === "Connected"
    );
  }
}

export const globalConnectionManager = new GlobalConnectionManager();

