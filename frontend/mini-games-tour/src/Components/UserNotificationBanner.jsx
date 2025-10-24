import React, { useEffect, useState } from 'react';
import { useAuth } from '../Utils/AuthProvider';
import useUserDatabase from '../Utils/useUserDatabase';
import { Link } from 'react-router-dom';
import './styles.css'; // optional for styling

export default function UserNotificationsBanner() {
  const { user } = useAuth();
  const [incomingFriendRequests, setIncomingFriendRequests] = useState([])
  const [incomingInviteToGameRequests, setIncomingInviteToGameRequests] = useState([])

  useEffect(() => {
    if (!user) return
    
    setIncomingFriendRequests(user.incomingFriendRequests)
    setIncomingInviteToGameRequests(user.incomingInviteToGameRequests)

  }, [user])

  if (!user) return;

  if (incomingFriendRequests.length === 0 && incomingInviteToGameRequests.length === 0) return null;

  return (
    <div className="notifications-banner">
      <h3>Notifications</h3>
      {incomingFriendRequests.length > 0 && (
        <div className="friend-requests-notification">
          <p>You have {incomingFriendRequests.length} friend request{incomingFriendRequests.length > 1 ? 's' : ''}.</p>
          <Link to={`/profile/${user.username}`}>Manage Friend Requests</Link>
        </div>
      )}
      {incomingInviteToGameRequests.length > 0 && (
        <div className="game-invites-notification">
          <p>You have {incomingInviteToGameRequests.length} game invite{incomingInviteToGameRequests.length > 1 ? 's' : ''}.</p>
          <Link to={`/profile/${user.username}`}>View Game Invites</Link>
        </div>
      )}
    </div>
  );
}
