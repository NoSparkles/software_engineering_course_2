import React, { useEffect, useState } from 'react'
import { useAuth } from '../../Utils/AuthProvider'
import { Link } from 'react-router-dom'
import useUserDatabase from '../../Utils/useUserDatabase'
import { useDebounce } from '../../Utils/useDebounce'
import './styles.css'

const Users = () => {
  const { getUsers, searchUsers } = useUserDatabase()
  const { user } = useAuth()
  const [users, setUsers] = useState([])
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300) // 300ms delay

  useEffect(() => {
    if (debouncedSearch.trim() === '') {
      // No search query: get all users
      getUsers().then(result => setUsers(result))
    } else {
      // Search query: call searchUsers
      searchUsers(debouncedSearch).then(result => setUsers(result))
    }
  }, [debouncedSearch])

  return (
    <div className='users'>
      <h1>Users</h1>
      <input
        type="text"
        placeholder="Search by username..."
        value={search}
        onChange={e => setSearch(e.target.value)}
        className="search-input"
      />
      <div className='user-list'>
        {user && users.map((item, i) => (
          item.username !== user.username && (
            <Link className='user-card' key={i} to={`/profile/${item.username}`}>
              {item.username}
            </Link>
          )
        ))}
      </div>
    </div>
  )
}

export default Users
