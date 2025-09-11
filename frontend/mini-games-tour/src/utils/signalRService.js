import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

let connection = null;

export function getConnection() {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl("http://localhost:5236/gamehub", {
        withCredentials: true,
        skipNegotiation: true,
        transport: 1
      })
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();
  }

  return connection;
}