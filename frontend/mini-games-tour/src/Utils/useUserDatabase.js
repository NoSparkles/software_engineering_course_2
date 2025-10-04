import useFetch from "./useFetch";

const useUserDatabase = () => {
    const {
        fetchData,
        data,
        } = useFetch();

    const getUsers = async () => {
        const result = await fetchData('http://localhost:5236/User')
        return result
    }

    const getUser = async (username) => {
        const result = await fetchData(`http://localhost:5236/User/${username}`);
        return result
    };

    const addFriend = async (myUsername, username) => {
        const result = await fetchData(
            `http://localhost:5236/User/${myUsername}/add-friend`,
            {
                method: 'PUT',
                body: JSON.stringify(username),
            },
            true
        );
        return result
    };

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
    }

    return {
        getUsers,
        getUser,
        addFriend,
        removeFriend
    }
}

export default useUserDatabase;