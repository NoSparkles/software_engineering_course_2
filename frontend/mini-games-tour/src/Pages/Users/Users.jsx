import React, { useEffect, useState } from 'react'
import { useAuth } from '../../Utils/AuthProvider'
import { Link } from 'react-router-dom'
import useUserDatabase from '../../Utils/useUserDatabase'
import './styles.css'

const Users = () => {
  const { getUsers } = useUserDatabase()
  const { user } = useAuth()
  const [users, setUsers] = useState([])

  useEffect(() => {
    getUsers().then(result => {
      setUsers(result)
    })
  }, [])

  return (
    <div className='users'>
      <h1>Users</h1>
      <div className='user-list'>
        {user && users.map((item, i) => {
          if (item.username !== user.username) {
            return (
              <Link className='user-card' key={i} to={`/profile/${item.username}`}>
                {item.username}
              </Link>
            )
          }
          return null
        })}
      </div>
    </div>
  )
}

export default Users
