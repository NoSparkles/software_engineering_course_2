import React, { useEffect, useState } from 'react'
import { useAuth } from '../../Utils/AuthProvider'
import { useParams, Link, useNavigate } from 'react-router-dom'
import useUserDatabase from '../../Utils/useUserDatabase'
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

  useEffect(() => {
    // Trigger banner check when arriving at profile page
    setTimeout(() => {
      window.dispatchEvent(new Event("localStorageUpdate"));
    }, 100);
  }, []);

  useEffect(() => {
    fetchProfile()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [username])

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

  if (loading) return <div className="profile">Loading...</div>

  const viewer = user?.username
  const isOwnProfile = viewer === profileUser.username
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
