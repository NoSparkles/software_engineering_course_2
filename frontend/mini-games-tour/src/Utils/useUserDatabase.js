import useFetch from "./useFetch";

const useUserDatabase = () => {
  const { fetchData } = useFetch();

  const getUsers = async () => {
    const result = await fetchData('http://localhost:5236/User');
    return result;
  };

  const getUser = async (username) => {
    const result = await fetchData(`http://localhost:5236/User/${username}`);
    return result;
  };

  // Send friend request
  const sendFriendRequest = async (myUsername, friendUsername) => {
    const result = await fetchData(
      `http://localhost:5236/User/${myUsername}/send-request`,
      {
        method: 'PUT',
        body: JSON.stringify(friendUsername),
      },
      true
    );
    return result;
  };

  const acceptFriendRequest = async (myUsername, friendUsername) => {
    const result = await fetchData(
      `http://localhost:5236/User/${myUsername}/accept-request`,
      {
        method: 'PUT',
        body: JSON.stringify(friendUsername),
      },
      true
    );
    return result;
  };

  const declineFriendRequest = async (myUsername, friendUsername) => {
    const result = await fetchData(
      `http://localhost:5236/User/${myUsername}/reject-request`,
      {
        method: 'PUT',
        body: JSON.stringify(friendUsername),
      },
      true
    );
    return result;
  };

  // Remove friend (mutual)
  const removeFriend = async (myUsername, username) => {
    const result = await fetchData(
      `http://localhost:5236/User/${myUsername}/remove-friend`,
      {
        method: 'PUT',
        body: JSON.stringify(username),
      },
      true
    );
    return result;
  };

  // Send an invitation
  const inviteFriendToGame = async (senderUsername, receiverUsername, gameType, code) => {
    console.log(senderUsername, receiverUsername, gameType, code);
    const result = await fetchData(
      `http://localhost:5236/User/${receiverUsername}/invite-friend-to-game`, // receiver in URL
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: senderUsername, // sender in body
          gameType,
          code,
        }),
      },
      true
    );
    return result;
  };

  // Accept an invitation
  const acceptInviteFriendToGame = async (receiverUsername, senderUsername, gameType, code) => {
    const result = await fetchData(
      `http://localhost:5236/User/${receiverUsername}/accept-invite-friend-to-game`, // receiver in URL
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: senderUsername, // sender in body
          gameType,
          code,
        }),
      },
      true
    );
    return result;
  };

  // Remove an invitation
  const removeInviteFriendToGame = async (receiverUsername, senderUsername, gameType, code) => {
    const result = await fetchData(
      `http://localhost:5236/User/${receiverUsername}/remove-invite-friend-to-game`, // receiver in URL
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: senderUsername, // sender in body
          gameType,
          code,
        }),
      },
      true
    );
    return result;
  };

  const searchUsers = async (query) => {
    if (!query || query.trim() === '') return [];

    const result = await fetchData(`http://localhost:5236/User/search?query=${encodeURIComponent(query)}`);
    return result;
  };


  return {
    getUsers,
    getUser,
    searchUsers,
    sendFriendRequest,
    acceptFriendRequest,
    declineFriendRequest,
    removeFriend,
    inviteFriendToGame,
    acceptInviteFriendToGame,
    removeInviteFriendToGame
  };
};

export default useUserDatabase;
