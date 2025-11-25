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

  const visibleUsers = user ? users.filter(item => item.username !== user.username) : [];

  return (
    <div className='users page-shell'>
      <div className='users-card card'>
        <div className="users-header">
          <div>
            <p className="eyebrow">Community</p>
            <h1>Browse players</h1>
          </div>
          <input
            type="text"
            placeholder="Search by username..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="search-input"
          />
        </div>
        <div className='user-list'>
          {visibleUsers.length ? (
            visibleUsers.map((item, i) => (
              <Link className='user-card' key={i} to={`/profile/${item.username}`}>
                {item.username}
              </Link>
            ))
          ) : (
            <p className="users-empty">No players found.</p>
          )}
        </div>
      </div>
    </div>
  )
}

export default Users
