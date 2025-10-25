import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Home from './Pages/Home/Home';
import GameEntry from './Components/GameEntry';
import WaitingRoom from './Pages/WaitingRoom/WaitingRoom';
import SessionRoom from './Pages/SessionRoom/SessionRoom';
import MatchmakingWaitingRoom from './Pages/WaitingRoom/MatchmakingWaitingRoom';
import MatchmakingSessionRoom from './Pages/SessionRoom/MatchmakingSessionRoom';
import SpectatePage from './Pages/Spectate/SpectatePage';
import { ReturnToGameBanner } from './Utils/ReturnToGameBanner';
import Navbar from './Components/Navbar';
import Login from './Pages/Login/Login';
import Register from './Pages/Register/Register';
import Profile from './Pages/Profile/Profile';
import Users from './Pages/Users/Users'
import { AuthProvider } from './Utils/AuthProvider';

function App() {
  return (
    <Router>
      <AuthProvider>
        <Navbar />
        <ReturnToGameBanner />
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path='/login' element={<Login />} />
          <Route path='/register' element={<Register />} />
          <Route path='/profile/:username' element={<Profile/>} />
          <Route path='/users' element={<Users/>} />
          <Route path="/rock-paper-scissors" element={<GameEntry />} />
          <Route path="/four-in-a-row" element={<GameEntry />} />
          <Route path="/pair-matching" element={<GameEntry />} />
          <Route path="/:gameType/waiting/:code" element={<WaitingRoom />} />
          <Route path="/:gameType/session/:code" element={<SessionRoom />} />
          <Route path="/:gameType/matchmaking-waiting/:code" element={<MatchmakingWaitingRoom />} />
          <Route path="/:gameType/matchmaking-session/:code" element={<MatchmakingSessionRoom />} />
          <Route path="/:gameType/spectate/:code" element={<SpectatePage />} />
        </Routes>
      </AuthProvider>
    </Router>
  );
}

export default App;
