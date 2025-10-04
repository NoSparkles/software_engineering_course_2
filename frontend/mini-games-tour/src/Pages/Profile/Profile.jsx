import React, { useEffect, useState } from 'react'
import { useAuth } from '../../Utils/AuthProvider'
import { useParams, Link, useNavigate } from 'react-router-dom'
import useUserDatabase from '../../Utils/useUserDatabase'
import './styles.css'

const Profile = () => {
  const { getUser, addFriend, removeFriend } = useUserDatabase()
  const { user, refreshUser } = useAuth() // <-- add refreshUser if your auth context has it
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

  const handleAddFriend = async () => {
    if (!user || !profileUser) return
    const success = await addFriend(user.username, profileUser.username)
    if (success) {
      await fetchProfile() // refresh profileUser
      if (refreshUser) refreshUser() // update your own friends list if available
    }
  }

  const handleRemoveFriend = async () => {
    if (!user || !profileUser) return
    const success = await removeFriend(user.username, profileUser.username)
    if (success) {
      await fetchProfile() // refresh profileUser
      if (refreshUser) refreshUser() // update your own friends list if available
    }
  }

  if (loading) return <div className="profile">Loading...</div>

  const showAddButton =
    user &&
    profileUser &&
    user.username !== profileUser.username &&
    !profileUser.friends.includes(user.username)

  const showRemoveButton =
    user &&
    profileUser &&
    user.username !== profileUser.username &&
    profileUser.friends.includes(user.username)

  return (
    <div className='profile'>
      <h1>{profileUser.username}</h1>

      {showAddButton && (
        <button className='add-friend-btn' onClick={handleAddFriend}>
          Add Friend
        </button>
      )}

      {showRemoveButton && (
        <button className='remove-friend-btn' onClick={handleRemoveFriend}>
          Unfriend
        </button>
      )}

      <div className='friends-container'>
        <h2>Friends</h2>
        {profileUser.friends.map((item, i) => (
          <Link to={`/profile/${item}`} key={i}>
            {item}
          </Link>
        ))}
      </div>

      <div className='games-info'>
        <div className='game-info'>
          <h2>Four In a Row</h2>
          <span className='game-mmr'>
            MMR: {profileUser.fourInARowMMR}
          </span>
          <span className='game-streak'>
            Win Streak: {profileUser.fourInARowWinStreak}
          </span>
        </div>

        <div className='game-info'>
          <h2>Pair Matching</h2>
          <span className='game-mmr'>
            MMR: {profileUser.pairMatchingMMR}
          </span>
          <span className='game-streak'>
            Win Streak: {profileUser.pairMatchingWinStreak}
          </span>
        </div>

        <div className='game-info'>
          <h2>Rock Paper Scissors</h2>
          <span className='game-mmr'>
            MMR: {profileUser.rockPaperScissorsMMR}
          </span>
          <span className='game-streak'>
            Win Streak: {profileUser.rockPaperScissorsWinStreak}
          </span>
        </div>
      </div>
    </div>
  )
}

export default Profile
