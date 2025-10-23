import React, { useEffect, useState } from 'react'
import { useAuth } from '../../Utils/AuthProvider'
import { useParams, Link, useNavigate } from 'react-router-dom'
import useUserDatabase from '../../Utils/useUserDatabase'
import useFetch from '../../Utils/useFetch'
import { connectSpectator, joinSpectateByUsername, leaveSpectate, disconnectSpectator } from '../../Services/spectatorService'
import './styles.css'

const Profile = () => {
  const {
    getUser,
    sendFriendRequest,
    acceptFriendRequest,
    declineFriendRequest,
    removeFriend
  } = useUserDatabase()

  const { user, refreshUser } = useAuth()
  const { username } = useParams()
  const navigate = useNavigate()
  const [profileUser, setProfileUser] = useState(undefined)
  const [loading, setLoading] = useState(true)
  const [currentGameInfo, setCurrentGameInfo] = useState(null)
  const [gameInfoLoading, setGameInfoLoading] = useState(false)
  const [spectatorStatus, setSpectatorStatus] = useState('none')
  const [spectatorError, setSpectatorError] = useState(null)
  const { fetchData } = useFetch()

  const viewer = user?.username

  const fetchProfile = async () => {
    setLoading(true)
    const result = await getUser(username)
    if (result?.username) {
      setProfileUser(result)
    } else {
      navigate('/')
    }
    setLoading(false)
  }

  const fetchCurrentGameInfo = async (targetUsername) => {
    setGameInfoLoading(true)
    try {
      const backendHost = window.__BACKEND_HOST__ || 'http://localhost:5236'
      const result = await fetchData(`${backendHost}/User/${targetUsername}/current-game`)
      console.log('Current game info for', targetUsername, ':', result)
      setCurrentGameInfo(result)
    } catch (error) {
      console.error('Failed to fetch current game info:', error)
      setCurrentGameInfo(null)
    }
    setGameInfoLoading(false)
  }

  useEffect(() => {
    // Trigger banner check when arriving at profile page
    setTimeout(() => {
      window.dispatchEvent(new Event("localStorageUpdate"));
    }, 100);
  }, []);

  useEffect(() => {
    fetchProfile()
  }, [username])

  useEffect(() => {
    if (profileUser && viewer && profileUser.username !== viewer) {
      console.log('Fetching game info for', profileUser.username, 'viewer:', viewer)
      fetchCurrentGameInfo(profileUser.username)
    }
  }, [profileUser, viewer])

  const isOwnProfile = viewer === profileUser?.username

  /* Actions */

  // Send request (viewer -> profileUser)
  const handleSendRequest = async () => {
    if (!user || !profileUser) return
    const res = await sendFriendRequest(user.username, profileUser.username)
    if (res) await fetchProfile()
  }

  // Accept incoming request (viewer accepts requester)
  const handleAcceptRequest = async (requesterUsername) => {
    if (!user) return
    const res = await acceptFriendRequest(user.username, requesterUsername)
    if (res) {
      await fetchProfile()
      if (refreshUser) refreshUser()
    }
  }

  // Decline incoming request (viewer declines requester)
  const handleDeclineRequest = async (requesterUsername) => {
    if (!user) return
    const res = await declineFriendRequest(user.username, requesterUsername)
    if (res) await fetchProfile()
  }

  // Cancel a request that the viewer already sent to profileUser
  // (call decline on the profileUser's endpoint with viewer as requester)
  const handleCancelRequest = async () => {
    if (!user || !profileUser) return
    const res = await declineFriendRequest(profileUser.username, user.username)
    if (res) {
      await fetchProfile()
      if (refreshUser) refreshUser()
    }
  }

  // Unfriend
  const handleUnfriend = async () => {
    if (!user || !profileUser) return
    const res = await removeFriend(user.username, profileUser.username)
    if (res) {
      await fetchProfile()
      if (refreshUser) refreshUser()
    }
  }

  // Spectate functionality
  const handleSpectate = async () => {
    if (!currentGameInfo?.inGame) return
    
    setSpectatorStatus('connecting')
    setSpectatorError(null)
    
    try {
      const { gameType, roomCode, isMatchmaking } = currentGameInfo
      
      // Navigate directly to the session room with spectator parameter
      if (isMatchmaking) {
        navigate(`/${gameType}/matchmaking-session/${roomCode}?spectator=true`)
      } else {
        navigate(`/${gameType}/session/${roomCode}?spectator=true`)
      }
      
      setSpectatorStatus('connected')
      
    } catch (error) {
      console.error('Failed to join as spectator:', error)
      setSpectatorError(error?.message || 'Failed to join as spectator')
      setSpectatorStatus('error')
    }
  }

  console.log(user)
  const handleJoinInvite = async (invite) => {
    console.log('Join invite clicked:', invite);
    // TODO: Implement navigation or game join logic
  };

  if (loading) return <div className="profile">Loading...</div>

  const isFriend = profileUser.friends?.includes(viewer)
  // from viewer perspective:
  const iSentRequestToThem = profileUser.incomingFriendRequests?.includes(viewer) // profileUser received my request
  const theySentRequestToMe = profileUser.outgoingFriendRequests?.includes(viewer) // profileUser requested me

  return (
    <div className='profile'>
      <h1>{profileUser.username}</h1>

      {/* Friend / Request controls for other users */}
      {!isOwnProfile && (
        <>
          {/* Spectate button - show if user is in a game */}
          {!gameInfoLoading && currentGameInfo?.inGame && (
            <div className="spectate-section">
              <p> {profileUser.username} is currently playing {currentGameInfo.gameType.replace('-', ' ')}</p>
              <button 
                className='spectate-btn' 
                onClick={handleSpectate}
                disabled={spectatorStatus === 'connecting'}
              >
                {spectatorStatus === 'connecting' ? 'Connecting...' : 'Spectate Game'}
              </button>
              {spectatorError && (
                <p style={{ color: 'red', fontSize: '0.9em' }}>
                  Error: {spectatorError}
                </p>
              )}
            </div>
          )}

          {isFriend && (
            <button className='remove-friend-btn' onClick={handleUnfriend}>
              Unfriend
            </button>
          )}

          {!isFriend && iSentRequestToThem && (
            // I already sent them a request -> allow cancel
            <button className='cancel-request-btn' onClick={handleCancelRequest}>
              Cancel Request
            </button>
          )}

          {!isFriend && theySentRequestToMe && (
            // They sent me a request -> I can accept or decline
            <>
              <button className='accept-friend-btn' onClick={() => handleAcceptRequest(profileUser.username)}>
                Accept Friend Request
              </button>
              <button className='decline-friend-btn' onClick={() => handleDeclineRequest(profileUser.username)}>
                Decline
              </button>
            </>
          )}

          {!isFriend && !iSentRequestToThem && !theySentRequestToMe && (
            // No relation -> send request
            <button className='add-friend-btn' onClick={handleSendRequest}>
              Send Friend Request
            </button>
          )}
        </>
      )}

      {/* Own profile: show incoming requests list */}
      {isOwnProfile && profileUser.incomingFriendRequests?.length > 0 && (
        <div className='incoming-requests'>
          <h2>Incoming Friend Requests</h2>
          {profileUser.incomingFriendRequests.map((requester, i) => (
            <div key={i} className='friend-request'>
              <span className='requester-name'>{requester}</span>
              <button className='accept-friend-btn' onClick={() => handleAcceptRequest(requester)}>
                Accept
              </button>
              <button className='decline-friend-btn' onClick={() => handleDeclineRequest(requester)}>
                Decline
              </button>
            </div>
          ))}
        </div>
      )}

      {isOwnProfile && profileUser.incomingInviteToGameRequests?.length > 0 && (
        <div className='incoming-invites'>
          <h2>Incoming Game Invites</h2>
          {profileUser.incomingInviteToGameRequests.map((invite, i) => (
            <div key={i} className='game-invite'>
              <span className='invite-info'>
                From <strong>{invite.fromUsername}</strong> — Room: <code>{invite.roomKey}</code>
              </span>
              <button className='join-invite-btn' onClick={() => handleJoinInvite(invite)}>
                Join
              </button>
            </div>
          ))}
        </div>
      )}

      <div className='friends-container'>
        <h2>Friends</h2>
        {profileUser.friends?.map((item, i) => (
          <Link to={`/profile/${item}`} key={i}>{item}</Link>
        ))}
      </div>

      <div className='games-info'>
        <div className='game-info'>
          <h2>Four In a Row</h2>
          <span className='game-mmr'>MMR: {profileUser.fourInARowMMR}</span>
          <span className='game-streak'>Win Streak: {profileUser.fourInARowWinStreak}</span>
        </div>
        <div className='game-info'>
          <h2>Pair Matching</h2>
          <span className='game-mmr'>MMR: {profileUser.pairMatchingMMR}</span>
          <span className='game-streak'>Win Streak: {profileUser.pairMatchingWinStreak}</span>
        </div>
        <div className='game-info'>
          <h2>Rock Paper Scissors</h2>
          <span className='game-mmr'>MMR: {profileUser.rockPaperScissorsMMR}</span>
          <span className='game-streak'>Win Streak: {profileUser.rockPaperScissorsWinStreak}</span>
        </div>
      </div>
    </div>
  )
}

export default Profile
