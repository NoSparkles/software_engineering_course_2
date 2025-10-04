import React, { useEffect, useState } from 'react'
import { useAuth } from '../../Utils/AuthProvider'
import { useParams, Link, useNavigate } from 'react-router-dom'
import useUserDatabase from '../../Utils/useUserDatabase'
import './styles.css'

const Profile = () => {
  const { getUser, addFriend } = useUserDatabase()
  const { user } = useAuth()
  const { username } = useParams()
  const navigate = useNavigate()
  const [profileUser, setProfileUser] = useState(undefined)

  useEffect(() => {
    getUser(username).then( result => {
        if (result.username) {
            setProfileUser(result)
        } else {
            navigate('/') // redirect if user not found
        }
    })
  }, [username])

  const handleAddFriend = async () => {
    if (!user || !profileUser) return
    const { result, error } = await addFriend(user.username, profileUser.username)
    if (!error && result) {
      setProfileUser(result) // refresh profileUser after adding friend
    }
  }

  const showAddButton =
    user &&
    profileUser &&
    user.username !== profileUser.username &&
    !user.friends.includes(profileUser.username)

  return (
    <div className='profile'>
      {profileUser && (
        <>
          <h1>{profileUser.username}</h1>

          {showAddButton && (
            <button className='add-friend-btn' onClick={handleAddFriend}>
              Add Friend
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
        </>
      )}
    </div>
  )
}

export default Profile
