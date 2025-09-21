import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Home from './Pages/Home/Home';
import GameEntry from './Components/GameEntry';
import WaitingRoom from './Pages/WaitingRoom/WaitingRoom';
import SessionRoom from './Pages/SessionRoom/SessionRoom';
import ReturnToGameBanner from './Utils/ReturnToGameBanner';
import Navbar from './Components/Navbar';
import Login from './Pages/Login/Login';
import Register from './Pages/Register/Register';

function App() {
  return (
    <Router>
      <Navbar />
      <ReturnToGameBanner />
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path='/login' element={<Login />} />
        <Route path='/register' element={<Register />} />
        <Route path="/rock-paper-scissors" element={<GameEntry />} />
        <Route path="/four-in-a-row" element={<GameEntry />} />
        <Route path="/pair-matching" element={<GameEntry />} />
        <Route path="/:gameType/waiting/:code" element={<WaitingRoom />} />
        <Route path="/:gameType/session/:code" element={<SessionRoom />} />
      </Routes>
    </Router>
  );
}

export default App;
