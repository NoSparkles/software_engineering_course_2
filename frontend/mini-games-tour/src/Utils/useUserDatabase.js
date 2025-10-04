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
      `http://localhost:5236/User/${myUsername}/send-friend-request`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'text/plain' },
        body: friendUsername,
      },
      true
    );
    return result;
  };

  const acceptFriendRequest = async (myUsername, friendUsername) => {
    const result = await fetchData(
      `http://localhost:5236/User/${myUsername}/accept-friend-request`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'text/plain' },
        body: friendUsername,
      },
      true
    );
    return result;
  };

  const declineFriendRequest = async (myUsername, friendUsername) => {
    const result = await fetchData(
      `http://localhost:5236/User/${myUsername}/decline-friend-request`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'text/plain' },
        body: friendUsername,
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

  return {
    getUsers,
    getUser,
    sendFriendRequest,
    acceptFriendRequest,
    declineFriendRequest,
    removeFriend,
  };
};

export default useUserDatabase;
