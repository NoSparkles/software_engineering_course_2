import useFetch from "./useFetch";

const useUserDatabase = () => {
    const {
        fetchData,
        data,
        } = useFetch();

    const getUser = async (username) => {
        const result = await fetchData(`http://localhost:5236/User/${username}`);
        return result
    };

    const addFriend = async (myUsername, username) => {
        const result = await fetchData(
        `http://localhost:5236/User/${myUsername}/add-friend`,
        {
            method: 'PUT',
            body: JSON.stringify({ username }),
        },
        true
        );
        return result
     };    



    return {
        getUser,
        addFriend
    }
}

export default useUserDatabase;