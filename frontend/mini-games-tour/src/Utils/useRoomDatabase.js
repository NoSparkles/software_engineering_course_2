import useFetch from "./useFetch";

const useRoomDatabase = () => {
  const { fetchData } = useFetch();

  const roomExists = async (gameType, code) => {
    const result = await fetchData(
        `http://localhost:5236/Room/exists/${gameType}/${code}`
    )
    return result
  }

  return {
    roomExists
  };
};

export default useRoomDatabase;
