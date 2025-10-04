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
    const result = await getUser(username)
    if (result?.username) {
      setProfileUser(result)
    } else {
      navigate('/')
    }
    setLoading(false)
  }

  useEffect(() => {
    fetchProfile()
  }, [username])

  const handleSendRequest = async () => {
    if (!user || !profileUser) return
    const success = await sendFriendRequest(user.username, profileUser.username)
    if (success) await fetchProfile()
  }

  const handleAcceptRequest = async (friendUsername) => {
    if (!user) return
    const success = await acceptFriendRequest(user.username, friendUsername)
    if (success) {
      await fetchProfile()
      if (refreshUser) refreshUser()
    }
  }

  const handleDeclineRequest = async (friendUsername) => {
    if (!user) return
    const success = await declineFriendRequest(user.username, friendUsername)
    if (success) await fetchProfile()
  }

  const handleUnfriend = async (friendUsername) => {
    if (!user) return
    const success = await removeFriend(user.username, friendUsername)
    if (success) {
      await fetchProfile()
      if (refreshUser) refreshUser()
    }
  }

  if (loading) return <div className="profile">Loading...</div>

  const isOwnProfile = user?.username === profileUser.username
  const isFriend = profileUser.friends.includes(user?.username)
  const hasIncomingRequest = profileUser.incomingFriendRequests?.includes(user?.username)

  return (
    <div className='profile'>
      <h1>{profileUser.username}</h1>

      {!isOwnProfile && isFriend && (
        <button className='remove-friend-btn' onClick={() => handleUnfriend(profileUser.username)}>
          Unfriend
        </button>
      )}

      {!isOwnProfile && !isFriend && !hasIncomingRequest && (
        <button className='add-friend-btn' onClick={handleSendRequest}>
          Send Friend Request
        </button>
      )}

      {!isOwnProfile && !isFriend && hasIncomingRequest && (
        <>
          <button className='accept-friend-btn' onClick={() => handleAcceptRequest(profileUser.username)}>
            Accept Friend Request
          </button>
          <button className='decline-friend-btn' onClick={() => handleDeclineRequest(profileUser.username)}>
            Decline
          </button>
        </>
      )}

      {isOwnProfile && profileUser.incomingFriendRequests?.length > 0 && (
        <div className='incoming-requests'>
          <h2>Incoming Friend Requests</h2>
          {profileUser.incomingFriendRequests.map((requester, i) => (
            <div key={i} className='friend-request'>
              <span>{requester}</span>
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
        {profileUser.friends.map((item, i) => (
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
